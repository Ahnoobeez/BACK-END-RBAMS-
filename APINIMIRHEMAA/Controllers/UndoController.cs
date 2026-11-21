using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace APINIMIRHEMAA.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]

public sealed class UndoController : ControllerBase
{
    private static readonly MethodInfo SetMethod = typeof(DbContext)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method =>
            method.Name == nameof(DbContext.Set) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 0);

    private static readonly MethodInfo IgnoreQueryFiltersMethod =
        typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(method =>
                method.Name == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters) &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 1);


    private readonly LocalDbContext _db;

    private readonly UndoOperationStore _store;

    private readonly ILogger<UndoController> _logger;


    public UndoController(
        LocalDbContext db,
        UndoOperationStore store,
        ILogger<UndoController> logger)
    {
        _db = db;
        _store = store;
        _logger = logger;
    }


    [HttpGet("availability/{batchId}")]
    public IActionResult Availability(string batchId)
    {

        if (!Guid.TryParse(batchId, out _))
        {
            return BadRequest(new { message = "The undo batch ID is invalid." });
        }


        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return _store.IsAvailable(batchId, userId)
            ? Ok(new { available = true })
            : NotFound(new { available = false });
    }


    [HttpPost("{batchId}")]
    public async Task<IActionResult> Undo(
        string batchId,
        CancellationToken cancellationToken)
    {

        if (!Guid.TryParse(batchId, out _))
        {
            return BadRequest(new { message = "The undo batch ID is invalid." });
        }


        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }


        if (!_store.TryReserve(batchId, userId, out var reservation) || reservation == null)
        {
            return NotFound(new { message = "This save can no longer be undone." });
        }

        IDbContextTransaction? transaction = null;
        var completed = false;
        try
        {
            transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var change in reservation.Changes.Reverse())
            {
                await ApplyChangeAsync(change, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _store.Complete(reservation);
            completed = true;
            return Ok(new { undone = true });
        }

        catch (UndoConflictException exception)
        {

            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return Conflict(new { message = exception.Message });
        }

        catch (DbUpdateException exception)
        {

            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogWarning(exception, "Undo batch {BatchId} could not be applied.", reservation.BatchId);
            return Conflict(new { message = "The saved data changed or conflicts with another record, so it was not undone." });
        }
        finally
        {

            if (!completed)
            {
                _store.Release(reservation);
            }

            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }


    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);


    private async Task ApplyChangeAsync(
        UndoEntityChange change,
        CancellationToken cancellationToken)
    {
        var entityType = _db.Model.FindEntityType(change.EntityType);

        if (entityType == null || entityType.FindPrimaryKey() == null)
        {

            throw new UndoConflictException("The saved record type can no longer be undone.");
        }

        var entity = FindEntity(entityType, change.KeyValues);

        switch (change.State)
        {

            case EntityState.Added:

                if (entity == null)
                {

                    throw new UndoConflictException("The created record no longer exists.");
                }

                _db.Remove(entity);

                await _db.SaveChangesAsync(cancellationToken);
                break;


            case EntityState.Modified:

                if (entity == null)
                {

                    throw new UndoConflictException("The edited record no longer exists.");
                }

                EnsureExpectedValues(entity, change.ExpectedValues);
                SetPropertyValues(entity, change.PreviousValues);

                await _db.SaveChangesAsync(cancellationToken);
                break;


            case EntityState.Deleted:

                if (entity != null)
                {

                    throw new UndoConflictException("A record with the deleted record's ID already exists.");
                }

                var restoredEntity = Activator.CreateInstance(change.EntityType)

                    ?? throw new UndoConflictException("The deleted record cannot be recreated.");
                SetPropertyValues(restoredEntity, change.PreviousValues);
                SetPropertyValues(restoredEntity, change.KeyValues);
                await AddRestoredEntityAsync(entityType, restoredEntity, cancellationToken);
                break;


            default:
                throw new UndoConflictException("The saved operation type is not supported.");
        }
    }


    private object? FindEntity(
        IEntityType entityType,
        IReadOnlyDictionary<string, object?> keyValues)
    {
        var key = entityType.FindPrimaryKey()

            ?? throw new UndoConflictException("The saved record has no primary key.");

        if (key.Properties.Any(property => !keyValues.ContainsKey(property.Name)))
        {
            throw new UndoConflictException("The saved record's key is incomplete.");
        }

        var query = (IQueryable)SetMethod
            .MakeGenericMethod(entityType.ClrType)
            .Invoke(_db, null)!;
        query = (IQueryable)IgnoreQueryFiltersMethod
            .MakeGenericMethod(entityType.ClrType)
            .Invoke(null, new object[] { query })!;

        var parameter = Expression.Parameter(entityType.ClrType, "entity");
        Expression? predicate = null;

        foreach (var property in key.Properties)
        {
            var valueExpression = GetPropertyExpression(parameter, property);
            var keyExpression = CreateTypedValue(keyValues[property.Name], property.ClrType);
            var comparison = Expression.Equal(valueExpression, keyExpression);
            predicate = predicate == null
                ? comparison
                : Expression.AndAlso(predicate, comparison);
        }

        var lambda = Expression.Lambda(predicate!, parameter);
        var queryExpression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.SingleOrDefault),
            new[] { entityType.ClrType },
            query.Expression,
            Expression.Quote(lambda));
        return query.Provider.Execute(queryExpression);
    }


    private static Expression GetPropertyExpression(
        ParameterExpression parameter,
        IProperty property)
    {

        if (property.PropertyInfo != null)
        {
            return Expression.Property(parameter, property.PropertyInfo);
        }

        var efPropertyMethod = typeof(EF)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(EF.Property) &&
                method.IsGenericMethodDefinition &&
                method.GetParameters().Length == 2)
            .MakeGenericMethod(property.ClrType);
        return Expression.Call(
            efPropertyMethod,
            parameter,
            Expression.Constant(property.Name));
    }


    private static Expression CreateTypedValue(object? value, Type propertyType)
    {

        if (value == null)
        {
            return Expression.Constant(null, propertyType);
        }

        var underlyingType = Nullable.GetUnderlyingType(propertyType);

        if (underlyingType != null && underlyingType == value.GetType())
        {
            return Expression.Convert(Expression.Constant(value, underlyingType), propertyType);
        }

        return Expression.Constant(value, propertyType);
    }


    private void EnsureExpectedValues(
        object entity,
        IReadOnlyDictionary<string, object?> expectedValues)
    {
        var entry = _db.Entry(entity);

        foreach (var expected in expectedValues)
        {
            var actual = entry.Property(expected.Key).CurrentValue;

            if (!ValuesEqual(actual, expected.Value))
            {

                throw new UndoConflictException("The record changed after it was saved, so it was not undone.");
            }
        }
    }


    private void SetPropertyValues(
        object entity,
        IReadOnlyDictionary<string, object?> values)
    {
        var entry = _db.Entry(entity);

        foreach (var value in values)
        {

            if (entry.Metadata.FindProperty(value.Key) == null)
            {
                throw new UndoConflictException("The saved record's fields no longer match the current schema.");
            }

            entry.Property(value.Key).CurrentValue = CloneValue(value.Value);
        }
    }


    private async Task AddRestoredEntityAsync(
        IEntityType entityType,
        object entity,
        CancellationToken cancellationToken)
    {
        var identityProperty = entityType.FindPrimaryKey()!.Properties
            .FirstOrDefault(IsSqlServerIdentity);
        var tableName = entityType.GetTableName();

        if (identityProperty == null || string.IsNullOrWhiteSpace(tableName))
        {
            _db.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var schema = entityType.GetSchema() ?? "dbo";
        var tableIdentifier = $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}";
        await _db.Database.ExecuteSqlRawAsync(
            $"SET IDENTITY_INSERT {tableIdentifier} ON",
            cancellationToken);
        try
        {
            _db.Add(entity);

            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"SET IDENTITY_INSERT {tableIdentifier} OFF",
                cancellationToken);
        }
    }


    private static bool IsSqlServerIdentity(IProperty property) =>
        property.ValueGenerated == ValueGenerated.OnAdd &&
        property.GetAnnotations().Any(annotation =>
            annotation.Name == "SqlServer:ValueGenerationStrategy" &&
            string.Equals(
                annotation.Value?.ToString(),
                "IdentityColumn",
                StringComparison.Ordinal));


    private static string QuoteIdentifier(string value) =>
        $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";


    private static object? CloneValue(object? value) =>
        value is byte[] bytes ? bytes.ToArray() : value;


    private static bool ValuesEqual(object? left, object? right)
    {

        if (left is byte[] leftBytes && right is byte[] rightBytes)
        {
            return leftBytes.AsSpan().SequenceEqual(rightBytes);
        }

        return Equals(left, right);
    }


    private sealed class UndoConflictException : Exception
    {

        public UndoConflictException(string message) : base(message)
        {
        }
    }
}
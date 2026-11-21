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
// Coordinates the undo controller API endpoints, including request handling and response generation.
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

    // Database context used by this class to query and persist application records.
    private readonly LocalDbContext _db;
    // Injected or cached store dependency/state used by this class.
    private readonly UndoOperationStore _store;
    // Logger used to record this class’s operational events and failures.
    private readonly ILogger<UndoController> _logger;

    // Initializes a new undo controller instance.
    public UndoController(
        LocalDbContext db,
        UndoOperationStore store,
        ILogger<UndoController> logger)
    {
        _db = db;
        _store = store;
        _logger = logger;
    }

    /// <summary>Checks whether an undo batch is available to the current user.</summary>
    [HttpGet("availability/{batchId}")]
    public IActionResult Availability(string batchId)
    {
        // Return HTTP 400 Bad Request when `!Guid.TryParse(batchId, out _)` is true.
        if (!Guid.TryParse(batchId, out _))
        {
            return BadRequest(new { message = "The undo batch ID is invalid." });
        }

        // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return _store.IsAvailable(batchId, userId)
            ? Ok(new { available = true })
            : NotFound(new { available = false });
    }

    /// <summary>Restores the changes stored in the specified undo batch.</summary>
    [HttpPost("{batchId}")]
    public async Task<IActionResult> Undo(
        string batchId,
        CancellationToken cancellationToken)
    {
        // Return HTTP 400 Bad Request when `!Guid.TryParse(batchId, out _)` is true.
        if (!Guid.TryParse(batchId, out _))
        {
            return BadRequest(new { message = "The undo batch ID is invalid." });
        }

        // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        // Return HTTP 404 Not Found when `!_store.TryReserve(batchId, userId, out var reservation) || reservation == null` is true.
        if (!_store.TryReserve(batchId, userId, out var reservation) || reservation == null)
        {
            return NotFound(new { message = "This save can no longer be undone." });
        }

        IDbContextTransaction? transaction = null;
        var completed = false;
        try
        {
            transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            // Process each `change` returned by the collection expression in the loop header.
            foreach (var change in reservation.Changes.Reverse())
            {
                await ApplyChangeAsync(change, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _store.Complete(reservation);
            completed = true;
            return Ok(new { undone = true });
        }
        // Handle `UndoConflictException exception` raised by the preceding operation.
        catch (UndoConflictException exception)
        {
            // Enter this branch when `transaction != null` is true.
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return Conflict(new { message = exception.Message });
        }
        // Handle `DbUpdateException exception` raised by the preceding operation.
        catch (DbUpdateException exception)
        {
            // Enter this branch when `transaction != null` is true.
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            _logger.LogWarning(exception, "Undo batch {BatchId} could not be applied.", reservation.BatchId);
            return Conflict(new { message = "The saved data changed or conflicts with another record, so it was not undone." });
        }
        finally
        {
            // Enter this branch when `!completed` is true.
            if (!completed)
            {
                _store.Release(reservation);
            }
            // Enter this branch when `transaction != null` is true.
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    // Reads the authenticated user ID from the claims principal.
    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    // Applies the change.
    private async Task ApplyChangeAsync(
        UndoEntityChange change,
        CancellationToken cancellationToken)
    {
        var entityType = _db.Model.FindEntityType(change.EntityType);
        // Enter this branch when `entityType == null || entityType.FindPrimaryKey() == null` is true.
        if (entityType == null || entityType.FindPrimaryKey() == null)
        {
            // Coordinates the type API endpoints, including request handling and response generation.
            throw new UndoConflictException("The saved record type can no longer be undone.");
        }

        var entity = FindEntity(entityType, change.KeyValues);
        // Select behavior based on `change.State`.
        switch (change.State)
        {
            // Handle the `EntityState.Added` scenario.
            case EntityState.Added:
                // Enter this branch when `entity == null` is true.
                if (entity == null)
                {
                    // Coordinates the no API endpoints, including request handling and response generation.
                    throw new UndoConflictException("The created record no longer exists.");
                }

                _db.Remove(entity);
                // Persist the pending entity changes to the database at this point.
                await _db.SaveChangesAsync(cancellationToken);
                break;

            // Handle the `EntityState.Modified` scenario.
            case EntityState.Modified:
                // Enter this branch when `entity == null` is true.
                if (entity == null)
                {
                    // Coordinates the no API endpoints, including request handling and response generation.
                    throw new UndoConflictException("The edited record no longer exists.");
                }

                EnsureExpectedValues(entity, change.ExpectedValues);
                SetPropertyValues(entity, change.PreviousValues);
                // Persist the pending entity changes to the database at this point.
                await _db.SaveChangesAsync(cancellationToken);
                break;

            // Handle the `EntityState.Deleted` scenario.
            case EntityState.Deleted:
                // Enter this branch when `entity != null` is true.
                if (entity != null)
                {
                    // Coordinates the with API endpoints, including request handling and response generation.
                    throw new UndoConflictException("A record with the deleted record's ID already exists.");
                }

                var restoredEntity = Activator.CreateInstance(change.EntityType)
                    // Coordinates the cannot API endpoints, including request handling and response generation.
                    ?? throw new UndoConflictException("The deleted record cannot be recreated.");
                SetPropertyValues(restoredEntity, change.PreviousValues);
                SetPropertyValues(restoredEntity, change.KeyValues);
                await AddRestoredEntityAsync(entityType, restoredEntity, cancellationToken);
                break;

            // Handle values not covered by the explicit switch cases.
            default:
                throw new UndoConflictException("The saved operation type is not supported.");
        }
    }

    // Finds the entity that matches the supplied criteria.
    private object? FindEntity(
        IEntityType entityType,
        IReadOnlyDictionary<string, object?> keyValues)
    {
        var key = entityType.FindPrimaryKey()
            // Coordinates the has API endpoints, including request handling and response generation.
            ?? throw new UndoConflictException("The saved record has no primary key.");
        // Return HTTP 409 Conflict when `key.Properties.Any(property => !keyValues.ContainsKey(property.Name))` is true.
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
        // Process each `property` returned by the collection expression in the loop header.
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

    // Retrieves the property expression.
    private static Expression GetPropertyExpression(
        ParameterExpression parameter,
        IProperty property)
    {
        // Exit the current operation when `property.PropertyInfo != null` is true.
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

    // Creates the typed value.
    private static Expression CreateTypedValue(object? value, Type propertyType)
    {
        // Exit the current operation when `value == null` is true.
        if (value == null)
        {
            return Expression.Constant(null, propertyType);
        }

        var underlyingType = Nullable.GetUnderlyingType(propertyType);
        // Exit the current operation when `underlyingType != null && underlyingType == value.GetType()` is true.
        if (underlyingType != null && underlyingType == value.GetType())
        {
            return Expression.Convert(Expression.Constant(value, underlyingType), propertyType);
        }

        return Expression.Constant(value, propertyType);
    }

    // Ensures the expected values is available or in the required state.
    private void EnsureExpectedValues(
        object entity,
        IReadOnlyDictionary<string, object?> expectedValues)
    {
        var entry = _db.Entry(entity);
        // Process each `expected` returned by the collection expression in the loop header.
        foreach (var expected in expectedValues)
        {
            var actual = entry.Property(expected.Key).CurrentValue;
            // Enter this branch when `!ValuesEqual(actual, expected.Value)` is true.
            if (!ValuesEqual(actual, expected.Value))
            {
                // Coordinates the changed API endpoints, including request handling and response generation.
                throw new UndoConflictException("The record changed after it was saved, so it was not undone.");
            }
        }
    }

    // Sets the property values.
    private void SetPropertyValues(
        object entity,
        IReadOnlyDictionary<string, object?> values)
    {
        var entry = _db.Entry(entity);
        // Process each `value` returned by the collection expression in the loop header.
        foreach (var value in values)
        {
            // Return HTTP 409 Conflict when `entry.Metadata.FindProperty(value.Key) == null` is true.
            if (entry.Metadata.FindProperty(value.Key) == null)
            {
                throw new UndoConflictException("The saved record's fields no longer match the current schema.");
            }

            entry.Property(value.Key).CurrentValue = CloneValue(value.Value);
        }
    }

    // Adds the restored entity.
    private async Task AddRestoredEntityAsync(
        IEntityType entityType,
        object entity,
        CancellationToken cancellationToken)
    {
        var identityProperty = entityType.FindPrimaryKey()!.Properties
            .FirstOrDefault(IsSqlServerIdentity);
        var tableName = entityType.GetTableName();
        // Enter this branch when `identityProperty == null || string.IsNullOrWhiteSpace(tableName)` is true.
        if (identityProperty == null || string.IsNullOrWhiteSpace(tableName))
        {
            _db.Add(entity);
            // Persist the pending entity changes to the database at this point.
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
            // Persist the pending entity changes to the database at this point.
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"SET IDENTITY_INSERT {tableIdentifier} OFF",
                cancellationToken);
        }
    }

    // Checks whether the property uses SQL Server identity-value generation.
    private static bool IsSqlServerIdentity(IProperty property) =>
        property.ValueGenerated == ValueGenerated.OnAdd &&
        property.GetAnnotations().Any(annotation =>
            annotation.Name == "SqlServer:ValueGenerationStrategy" &&
            string.Equals(
                annotation.Value?.ToString(),
                "IdentityColumn",
                StringComparison.Ordinal));

    // Escapes a SQL Server identifier for safe use in a generated query.
    private static string QuoteIdentifier(string value) =>
        $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    // Copies supported value types so the undo snapshot is independent of later changes.
    private static object? CloneValue(object? value) =>
        value is byte[] bytes ? bytes.ToArray() : value;

    // Compares stored values, including byte arrays, for equality.
    private static bool ValuesEqual(object? left, object? right)
    {
        // Exit the current operation when `left is byte[] leftBytes && right is byte[] rightBytes` is true.
        if (left is byte[] leftBytes && right is byte[] rightBytes)
        {
            return leftBytes.AsSpan().SequenceEqual(rightBytes);
        }

        return Equals(left, right);
    }

    // Coordinates the undo conflict exception API endpoints, including request handling and response generation.
    private sealed class UndoConflictException : Exception
    {
        // Initializes a new undo conflict exception instance.
        public UndoConflictException(string message) : base(message)
        {
        }
    }
}
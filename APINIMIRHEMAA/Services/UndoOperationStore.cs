using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services;

// Stores entity state and values needed to reverse a change.
public sealed record UndoEntityChange(
    Type EntityType,
    EntityState State,
    IReadOnlyDictionary<string, object?> KeyValues,
    IReadOnlyDictionary<string, object?> PreviousValues,
    IReadOnlyDictionary<string, object?> ExpectedValues);

// Groups entity changes into an undo batch owned by a user.
public sealed record UndoOperationBatch(
    string BatchId,
    int UserId,
    IReadOnlyList<UndoEntityChange> Changes);

// Provides reusable application operations for undo operation store workflows.
public sealed class UndoOperationStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<string, StoredBatch> _batches = new();

    // Stores entity-change snapshots for the user so they can be undone.
    public void Record(string batchId, int userId, IReadOnlyCollection<UndoEntityChange> changes)
    {
        // Exit the current operation when `changes.Count == 0` is true.
        if (changes.Count == 0)
        {
            return;
        }

        var key = NormalizeBatchId(batchId);
        RemoveExpiredBatches();
        var batch = _batches.GetOrAdd(key, _ => new StoredBatch(userId));
        lock (batch.Sync)
        {
            // Raise an exception when `batch.UserId != userId` is true.
            if (batch.UserId != userId)
            {
                throw new InvalidOperationException("The undo batch belongs to another user.");
            }
            // Raise an exception when `batch.Reserved` is true.
            if (batch.Reserved)
            {
                throw new InvalidOperationException("The undo batch is already being processed.");
            }

            batch.Changes.AddRange(changes);
            batch.ExpiresAtUtc = DateTimeOffset.UtcNow.Add(Retention);
        }
    }

    // Checks whether this user has a non-expired undo batch that is not already reserved.
    public bool IsAvailable(string batchId, int userId)
    {
        var key = NormalizeBatchId(batchId);
        // Exit the current operation when `!_batches.TryGetValue(key, out var batch)` is true.
        if (!_batches.TryGetValue(key, out var batch))
        {
            return false;
        }

        lock (batch.Sync)
        {
            // Enter this branch when `IsExpired(batch)` is true.
            if (IsExpired(batch))
            {
                _batches.TryRemove(key, out _);
                return false;
            }

            return batch.UserId == userId &&
                !batch.Reserved &&
                batch.Changes.Count > 0;
        }
    }

    // Reserves an available undo batch for this user and returns its changes.
    public bool TryReserve(string batchId, int userId, out UndoOperationBatch? reservation)
    {
        var key = NormalizeBatchId(batchId);
        reservation = null;
        // Exit the current operation when `!_batches.TryGetValue(key, out var batch)` is true.
        if (!_batches.TryGetValue(key, out var batch))
        {
            return false;
        }

        lock (batch.Sync)
        {
            // Enter this branch when `IsExpired(batch)` is true.
            if (IsExpired(batch))
            {
                _batches.TryRemove(key, out _);
                return false;
            }
            // Exit the current operation when `batch.UserId != userId || batch.Reserved || batch.Changes.Count == 0` is true.
            if (batch.UserId != userId || batch.Reserved || batch.Changes.Count == 0)
            {
                return false;
            }

            batch.Reserved = true;
            reservation = new UndoOperationBatch(key, userId, batch.Changes.ToArray());
            return true;
        }
    }

    // Removes the undo batch after its changes have been applied.
    public void Complete(UndoOperationBatch reservation)
    {
        _batches.TryRemove(reservation.BatchId, out _);
    }

    // Makes the reserved undo batch available again after an unsuccessful undo.
    public void Release(UndoOperationBatch reservation)
    {
        // Exit the current operation when `!_batches.TryGetValue(reservation.BatchId, out var batch)` is true.
        if (!_batches.TryGetValue(reservation.BatchId, out var batch))
        {
            return;
        }

        lock (batch.Sync)
        {
            // Enter this branch when `batch.UserId == reservation.UserId` is true.
            if (batch.UserId == reservation.UserId)
            {
                batch.Reserved = false;
            }
        }
    }

    // Normalizes the batch id for consistent use.
    public static string NormalizeBatchId(string batchId)
    {
        // Raise an exception when `!Guid.TryParse(batchId, out var parsedBatchId)` is true.
        if (!Guid.TryParse(batchId, out var parsedBatchId))
        {
            throw new ArgumentException("The undo batch ID is invalid.", nameof(batchId));
        }

        return parsedBatchId.ToString("N");
    }

    // Removes the expired batches.
    private void RemoveExpiredBatches()
    {
        // Process each `pair` returned by the collection expression in the loop header.
        foreach (var pair in _batches)
        {
            lock (pair.Value.Sync)
            {
                // Enter this branch when `!pair.Value.Reserved && IsExpired(pair.Value)` is true.
                if (!pair.Value.Reserved && IsExpired(pair.Value))
                {
                    _batches.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    // Checks whether the undo batch has passed its expiration time.
    private static bool IsExpired(StoredBatch batch) =>
        DateTimeOffset.UtcNow >= batch.ExpiresAtUtc;

    // Provides reusable application operations for stored batch workflows.
    private sealed class StoredBatch
    {
        // Initializes a new stored batch instance.
        public StoredBatch(int userId)
        {
            UserId = userId;
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(Retention);
        }

        public object Sync { get; } = new();
        public int UserId { get; }
        public List<UndoEntityChange> Changes { get; } = new();
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public bool Reserved { get; set; }
    }
}
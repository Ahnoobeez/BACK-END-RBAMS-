using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services;


public sealed record UndoEntityChange(
    Type EntityType,
    EntityState State,
    IReadOnlyDictionary<string, object?> KeyValues,
    IReadOnlyDictionary<string, object?> PreviousValues,
    IReadOnlyDictionary<string, object?> ExpectedValues);


public sealed record UndoOperationBatch(
    string BatchId,
    int UserId,
    IReadOnlyList<UndoEntityChange> Changes);


public sealed class UndoOperationStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<string, StoredBatch> _batches = new();


    public void Record(string batchId, int userId, IReadOnlyCollection<UndoEntityChange> changes)
    {

        if (changes.Count == 0)
        {
            return;
        }

        var key = NormalizeBatchId(batchId);
        RemoveExpiredBatches();
        var batch = _batches.GetOrAdd(key, _ => new StoredBatch(userId));
        lock (batch.Sync)
        {

            if (batch.UserId != userId)
            {
                throw new InvalidOperationException("The undo batch belongs to another user.");
            }

            if (batch.Reserved)
            {
                throw new InvalidOperationException("The undo batch is already being processed.");
            }

            batch.Changes.AddRange(changes);
            batch.ExpiresAtUtc = DateTimeOffset.UtcNow.Add(Retention);
        }
    }


    public bool IsAvailable(string batchId, int userId)
    {
        var key = NormalizeBatchId(batchId);

        if (!_batches.TryGetValue(key, out var batch))
        {
            return false;
        }

        lock (batch.Sync)
        {

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


    public bool TryReserve(string batchId, int userId, out UndoOperationBatch? reservation)
    {
        var key = NormalizeBatchId(batchId);
        reservation = null;

        if (!_batches.TryGetValue(key, out var batch))
        {
            return false;
        }

        lock (batch.Sync)
        {

            if (IsExpired(batch))
            {
                _batches.TryRemove(key, out _);
                return false;
            }

            if (batch.UserId != userId || batch.Reserved || batch.Changes.Count == 0)
            {
                return false;
            }

            batch.Reserved = true;
            reservation = new UndoOperationBatch(key, userId, batch.Changes.ToArray());
            return true;
        }
    }


    public void Complete(UndoOperationBatch reservation)
    {
        _batches.TryRemove(reservation.BatchId, out _);
    }


    public void Release(UndoOperationBatch reservation)
    {

        if (!_batches.TryGetValue(reservation.BatchId, out var batch))
        {
            return;
        }

        lock (batch.Sync)
        {

            if (batch.UserId == reservation.UserId)
            {
                batch.Reserved = false;
            }
        }
    }


    public static string NormalizeBatchId(string batchId)
    {

        if (!Guid.TryParse(batchId, out var parsedBatchId))
        {
            throw new ArgumentException("The undo batch ID is invalid.", nameof(batchId));
        }

        return parsedBatchId.ToString("N");
    }


    private void RemoveExpiredBatches()
    {

        foreach (var pair in _batches)
        {
            lock (pair.Value.Sync)
            {

                if (!pair.Value.Reserved && IsExpired(pair.Value))
                {
                    _batches.TryRemove(pair.Key, out _);
                }
            }
        }
    }


    private static bool IsExpired(StoredBatch batch) =>
        DateTimeOffset.UtcNow >= batch.ExpiresAtUtc;


    private sealed class StoredBatch
    {

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
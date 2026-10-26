using Ardan.Core.Layout;

namespace Ardan.Core.Stock;

/// <summary>A deep storage lane. Pallets go in and come out on the aisle side, so the last pallet in is the first out.</summary>
public sealed class Lane(LaneId id, int depth)
{
    private readonly List<Pallet> _pallets = [];
    private readonly List<string> _incoming = [];
    private (string Sku, string Batch)? _incomingBatch;

    public LaneId Id { get; } = id;
    public int Depth { get; } = depth;
    public int Count => _pallets.Count;
    public bool IsEmpty => _pallets.Count == 0;
    public bool IsFull => _pallets.Count >= Depth;
    public Pallet? Front => IsEmpty ? null : _pallets[^1];
    public IReadOnlyList<Pallet> Pallets => _pallets;
    public IReadOnlyList<string> Incoming => _incoming;
    public string? LockedBy { get; private set; }
    public bool IsLocked => LockedBy is not null;
    public bool IsBusy => LockedBy is not null || _incoming.Count > 0;
    public int Version { get; private set; }

    /// <summary>How many pallet places a Taxi travels into the lane to reach the front pallet.</summary>
    public int FrontReach => Depth - Count + 1;

    /// <summary>How many pallet places a Taxi travels into the lane to set down the next pallet.</summary>
    public int DropReach => Depth - Count;

    /// <summary>True when the pallet's batch could be stored here. Pallets already on their way count as taken places.</summary>
    public bool CanReceive(Pallet pallet) =>
        LockedBy is null && Count + _incoming.Count < Depth && BatchFits(pallet);

    public void Reserve(string jobId, Pallet pallet)
    {
        if (!CanReceive(pallet))
            throw new InvalidOperationException($"Lane {Id} cannot take pallet {pallet.Id}.");
        _incoming.Add(jobId);
        _incomingBatch ??= (pallet.SkuCode, pallet.Batch);
        Version++;
    }

    public void Lock(string jobId)
    {
        if (IsBusy && LockedBy != jobId)
            throw new InvalidOperationException($"Lane {Id} is already in use.");
        LockedBy = jobId;
        Version++;
    }

    public void Release(string jobId)
    {
        var changed = _incoming.Remove(jobId);
        if (_incoming.Count == 0) _incomingBatch = null;
        if (LockedBy == jobId)
        {
            LockedBy = null;
            changed = true;
        }
        if (changed) Version++;
    }

    public void Push(Pallet pallet)
    {
        if (IsFull || (Front is { } front && (front.SkuCode, front.Batch) != (pallet.SkuCode, pallet.Batch)))
            throw new InvalidOperationException($"Lane {Id} cannot take pallet {pallet.Id}.");
        pallet.Slot = _pallets.Count;
        pallet.Lane = Id;
        pallet.Status = PalletStatus.Stored;
        _pallets.Add(pallet);
        Version++;
    }

    public Pallet Pop()
    {
        var pallet = Front ?? throw new InvalidOperationException($"Lane {Id} is empty.");
        _pallets.RemoveAt(_pallets.Count - 1);
        pallet.Lane = null;
        Version++;
        return pallet;
    }

    private bool BatchFits(Pallet pallet)
    {
        var batch = Front is { } front ? (front.SkuCode, front.Batch) : _incomingBatch;
        return batch is null || batch == (pallet.SkuCode, pallet.Batch);
    }
}

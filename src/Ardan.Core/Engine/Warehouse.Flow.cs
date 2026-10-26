using System.Globalization;
using Ardan.Core.Fleet;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

public sealed partial class Warehouse
{
    private void RunInfeed(double seconds)
    {
        Infeed.Advance(seconds);
        foreach (var item in Infeed.Items.Where(item => !item.Checked && item.Position >= Motion.ProfileCheckAt).ToList())
            CheckProfile(item);

        _nextArrivalIn -= seconds;
        if (_nextArrivalIn > 0 || !Infeed.EntryClear) return;

        var order = _orders
            .Where(o => o.Type == OrderType.Inbound && o.IsOpen && o.Arrived < o.Quantity)
            .OrderByDescending(o => o.Priority)
            .ThenBy(o => o.CreatedAt)
            .FirstOrDefault();
        if (order is null) return;

        Arrive(order);
        _nextArrivalIn = 10 + _random.NextDouble() * 10;
    }

    private void Arrive(Order order)
    {
        var sku = SkuOf(order.SkuCode);
        var pallet = new Pallet
        {
            Id = NextPalletId(),
            SkuCode = sku.Code,
            Batch = order.Batch ?? NewBatch(Now),
            Expiry = order.Expiry ?? DateOnly.FromDateTime(Now.AddDays(sku.ShelfLifeDays).Date),
            WeightKg = 380 + _random.Next(750),
            HeightMm = 1050 + _random.Next(600),
            Status = PalletStatus.Arriving,
            OrderId = order.Id,
            ReceivedAt = Now,
        };
        SpoilProfile(pallet);

        _pallets[pallet.Id] = pallet;
        Infeed.Add(pallet);
        order.Arrived++;
        if (order.Status != OrderStatus.Queued) return;
        order.Status = OrderStatus.InProgress;
        _journal.OrderChanged(order);
    }

    private void SpoilProfile(Pallet pallet)
    {
        if (_random.NextDouble() >= ProfileFaultRate) return;
        switch (_random.Next(3))
        {
            case 0:
                pallet.HeightMm = 1840 + _random.Next(120);
                break;
            case 1:
                pallet.WeightKg = 1520 + _random.Next(160);
                break;
            default:
                pallet.LabelReadable = false;
                break;
        }
    }

    private void CheckProfile(ConveyorItem item)
    {
        var pallet = item.Pallet;
        var fault = pallet.CheckProfile();
        if (fault == ProfileFault.None)
        {
            item.Checked = true;
            _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Received, "Dock", "Infeed", "Profile check"));
            Events.Add(Now, EventKind.Received, $"{pallet.Id} passed the profile check", pallet.Id);
            return;
        }

        Infeed.Remove(item);
        pallet.Status = PalletStatus.Rejected;
        pallet.RejectedAt = Now;

        if (OrderOf(pallet) is { } order)
        {
            order.Rejected++;
            order.Status = OrderStatus.Exception;
            order.Note = "A pallet failed the profile check. Fix it and re-induct it from Alarms.";
            _journal.OrderChanged(order);
        }

        var (code, value) = fault switch
        {
            ProfileFault.TooTall => ("PRF-HGT", pallet.HeightMm.ToString("N0", CultureInfo.InvariantCulture)),
            ProfileFault.TooHeavy => ("PRF-WGT", pallet.WeightKg.ToString("N0", CultureInfo.InvariantCulture)),
            _ => ("PRF-LBL", ""),
        };
        Alarms.Raise(code, Infeed.Name, Now, Values(("pallet", pallet.Id), ("value", value)), subject: pallet.Id);
        _journal.PalletChanged(pallet);
        _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Rejected, "Infeed", "Reject lane", "Profile check"));
        Events.Add(Now, EventKind.Rejected, $"{pallet.Id} sent to the reject lane by the profile check", pallet.Id);
    }

    private void RunOutfeed(double seconds)
    {
        Outfeed.Advance(seconds);
        if (Outfeed.State != ConveyorState.Running || Outfeed.AtEnd is not { } item) return;

        Outfeed.Remove(item);
        var pallet = item.Pallet;
        pallet.Status = PalletStatus.Dispatched;
        _pallets.Remove(pallet.Id);
        Throughput.CountOut(Now);

        var order = OrderOf(pallet);
        var dock = order?.Dock ?? 1;
        _journal.PalletChanged(pallet);
        _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Dispatched, "Outfeed", $"Dock {dock}", null));
        Events.Add(Now, EventKind.Dispatched, $"{pallet.Id} reached dock {dock}{(order is null ? "" : $" for {order.Id}")}", pallet.Id);

        if (order is null) return;
        order.InFlight = Math.Max(0, order.InFlight - 1);
        order.Done++;
        CompleteIfDone(order);
    }

    private void CreateJobs()
    {
        CreatePutaway();
        CreateRetrievals();
    }

    private void CreatePutaway()
    {
        var item = Infeed.AtEnd;
        if (item is null || !item.Checked || item.JobId is not null) return;

        var pallet = item.Pallet;
        var lane = Slotting.LaneFor(Grid, pallet, SkuOf(pallet.SkuCode).Velocity);
        if (lane is null)
        {
            Alarms.Raise("STR-FULL", Infeed.Name, Now, Values(("pallet", pallet.Id), ("sku", NameOf(pallet.SkuCode))));
            return;
        }

        Alarms.Clear("STR-FULL", Infeed.Name, Now, "System");
        var job = NewJob(JobKind.Putaway, pallet, Place.Infeed, Place.At(lane.Id), OrderOf(pallet));
        lane.Reserve(job.Id, pallet);
        item.JobId = job.Id;
    }

    private void CreateRetrievals()
    {
        var outbound = _orders
            .Where(order => order.Type == OrderType.Outbound && order.IsOpen)
            .OrderByDescending(order => order.Priority)
            .ThenBy(order => order.DueAt)
            .ToList();

        foreach (var order in outbound)
        {
            while (order.StillToStart > 0 && order.InFlight < Limits.MaxJobsPerOrder)
            {
                var lane = Slotting.NextToRetrieve(Grid, order.SkuCode);
                if (lane is null) break;

                var pallet = lane.Front!;
                var job = NewJob(JobKind.Retrieval, pallet, Place.At(lane.Id), Place.Outfeed, order);
                lane.Lock(job.Id);
                pallet.OrderId = order.Id;
                order.InFlight++;
            }
            UpdateOutboundStatus(order);
        }
    }

    private void UpdateOutboundStatus(Order order)
    {
        var before = (order.Status, order.Note);
        var waitingForStock = order.StillToStart > 0 && Grid.StoredOf(order.SkuCode) == 0 && order.InFlight == 0;

        if (waitingForStock)
        {
            order.Status = OrderStatus.Short;
            order.Note = $"No {NameOf(order.SkuCode)} left in store. It starts again when stock arrives.";
        }
        else if (order.InFlight > 0 || order.Done > 0)
        {
            order.Status = OrderStatus.InProgress;
            order.Note = null;
        }

        if ((order.Status, order.Note) != before) _journal.OrderChanged(order);
    }

    private void CompleteIfDone(Order order)
    {
        if (order.Done + order.Returned < order.Quantity || order.Status == OrderStatus.Cancelled)
        {
            _journal.OrderChanged(order);
            return;
        }

        order.Status = OrderStatus.Completed;
        order.CompletedAt = Now;
        order.Note = order.Returned == 0 ? null : $"Completed with {PalletCount(order.Returned)} returned to the supplier.";
        _journal.OrderChanged(order);
        var result = order.Type == OrderType.Inbound ? "all stored" : $"all at dock {order.Dock}";
        Events.Add(Now, EventKind.Order, $"{order.Id} completed: {order.Quantity} × {NameOf(order.SkuCode)}, {result}", order.Id);
    }

    private Job NewJob(JobKind kind, Pallet pallet, Place from, Place to, Order? order)
    {
        var job = new Job
        {
            Id = NextJobId(),
            Kind = kind,
            Pallet = pallet,
            From = from,
            To = to,
            Order = order,
            CreatedAt = Now,
        };
        _jobs.Add(job);
        return job;
    }

    private static Dictionary<string, string> Values(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(pair => pair.Key, pair => pair.Value);
}

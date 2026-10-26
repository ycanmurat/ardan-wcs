using System.Globalization;
using Ardan.Core.Fleet;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

public sealed partial class Warehouse
{
    private static readonly TimeSpan RemoteResetAfter = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan ReturnRejectedAfter = TimeSpan.FromMinutes(10);

    private void InjectFaults(double seconds)
    {
        if (Happens(seconds, 1 / 720.0))
            StopTaxi(PickRandom(_taxis.Where(t => t.State == TaxiState.Driving && t.FaultCode is null)));

        if (Happens(seconds, 1 / 1800.0))
            StopLift(PickRandom(_lifts.Where(l => l.FaultCode is null && l.State is LiftState.Moving or LiftState.Carrying)));

        if (Happens(seconds, 1 / 2400.0) && Outfeed.Items.Count > 0 && Outfeed.FaultCode is null)
            JamOutfeed();

        if (Happens(seconds, 1 / 900.0))
            WeakenSignal(PickRandom(_taxis.Where(t => t.Signal == 4 && t.FaultCode is null)));

        if (Happens(seconds, 1 / 2400.0))
            Climate.DoorOpened(_random);
    }

    internal void StopTaxi(Taxi? taxi)
    {
        if (taxi is null) return;
        taxi.StateBeforeFault = taxi.State;
        taxi.State = TaxiState.Fault;
        taxi.FaultCode = "TX-OBS";
        taxi.FaultSince = Now;

        var bay = Math.Max(1, (int)Math.Round(taxi.X));
        Alarms.Raise("TX-OBS", taxi.Id, Now, Values(("level", Text(taxi.Level)), ("bay", Text(bay))));
        Events.Add(Now, EventKind.Alarm, $"{taxi.Id} stopped by its obstacle sensor on level {taxi.Level}", taxi.Id);
    }

    private void StopLift(Lift? lift)
    {
        if (lift is null) return;
        lift.StateBeforeFault = lift.State;
        lift.State = LiftState.Fault;
        lift.FaultCode = "VTU-INV";
        lift.FaultSince = Now;

        Alarms.Raise("VTU-INV", lift.Id, Now);
        Events.Add(Now, EventKind.Alarm, $"{lift.Id} stopped with a drive fault", lift.Id);
    }

    private void JamOutfeed()
    {
        var pallet = Outfeed.Items.OrderByDescending(item => item.Position).First().Pallet;
        Outfeed.State = ConveyorState.Jammed;
        Outfeed.FaultCode = "CNV-JAM";
        Outfeed.FaultSince = Now;

        Alarms.Raise("CNV-JAM", Outfeed.Name, Now, Values(("pallet", pallet.Id)));
        Events.Add(Now, EventKind.Alarm, $"Outfeed stopped: {pallet.Id} did not reach the next sensor", Outfeed.Id);
    }

    private void WeakenSignal(Taxi? taxi)
    {
        if (taxi is null) return;
        taxi.Signal = 1;
        taxi.SignalBackIn = 20 + _random.NextDouble() * 25;
        Alarms.Raise("TX-NET", taxi.Id, Now, Values(("level", Text(taxi.Level))));
    }

    private void WatchConditions(double seconds)
    {
        foreach (var taxi in _taxis)
        {
            if (taxi.Signal < 4)
            {
                taxi.SignalBackIn -= seconds;
                if (taxi.SignalBackIn <= 0)
                {
                    taxi.Signal = 4;
                    Alarms.Clear("TX-NET", taxi.Id, Now, "System");
                }
            }

            if (taxi.Battery < Limits.LowBattery)
                Alarms.Raise("TX-BAT", taxi.Id, Now, Values(("value", Text((int)taxi.Battery)), ("level", Text(taxi.Level))));
            else if (taxi.Battery >= Limits.WorkBattery)
                Alarms.Clear("TX-BAT", taxi.Id, Now, "System");
        }

        var temperature = Climate.TemperatureC;
        if (temperature > Limits.StoreMaxC)
            Alarms.Raise("ENV-TMP", "Chilled store", Now, Values(("value", temperature.ToString("0.0", CultureInfo.InvariantCulture))));
        else if (temperature < Limits.StoreMaxC - 0.4)
            Alarms.Clear("ENV-TMP", "Chilled store", Now, "System");

        ResetRemotely();
        ReturnOldRejects();
    }

    private void ResetRemotely()
    {
        foreach (var taxi in _taxis.Where(t => t.FaultSince is { } since && Now - since > RemoteResetAfter).ToList())
            ResetTaxiCore(taxi, RemoteSupport);

        foreach (var lift in _lifts.Where(l => l.FaultSince is { } since && Now - since > RemoteResetAfter).ToList())
            ResetLiftCore(lift, RemoteSupport);

        if (Outfeed.FaultSince is { } jammed && Now - jammed > RemoteResetAfter)
            ResetOutfeedCore(RemoteSupport);
    }

    private void ReturnOldRejects()
    {
        var old = _pallets.Values
            .Where(p => p.Status == PalletStatus.Rejected && p.RejectedAt is { } at && Now - at > ReturnRejectedAfter)
            .ToList();

        foreach (var pallet in old)
        {
            pallet.Status = PalletStatus.Returned;
            _pallets.Remove(pallet.Id);
            _journal.PalletChanged(pallet);
            foreach (var alarm in Alarms.Active.Where(alarm => alarm.Subject == pallet.Id).ToList())
                Alarms.Clear(alarm, Now, "Supervisor");

            _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Returned, "Reject lane", "Supplier", "Supervisor"));
            Events.Add(Now, EventKind.Info, $"{pallet.Id} returned to the supplier by the supervisor", pallet.Id);

            if (OrderOf(pallet) is not { } order) continue;
            order.Rejected = Math.Max(0, order.Rejected - 1);
            order.Returned++;
            if (order.Rejected == 0 && order.Status == OrderStatus.Exception)
            {
                order.Status = OrderStatus.InProgress;
                order.Note = null;
            }
            CompleteIfDone(order);
        }
    }

    private bool Happens(double seconds, double perSecond) => _random.NextDouble() < perSecond * seconds;

    private T? PickRandom<T>(IEnumerable<T> items) where T : class
    {
        var list = items.ToList();
        return list.Count == 0 ? null : list[_random.Next(list.Count)];
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}

using Ardan.Core.Alarms;
using Ardan.Core.Contracts;
using Ardan.Core.Fleet;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

public sealed partial class Warehouse
{
    public const string Operator = "Operator";
    public const string RemoteSupport = "Remote support";
    public const string Supervisor = "Supervisor";

    public static IReadOnlyList<double> Speeds { get; } = [1, 2, 5];

    public CommandResult CreateOrder(NewOrder request)
    {
        if (!_skus.TryGetValue(request.SkuCode, out var sku))
            return CommandResult.Failure("That product code is not in the item master.");
        if (request.Quantity is < 1 or > Limits.MaxPalletsPerOrder)
            return CommandResult.Failure($"Choose between 1 and {Limits.MaxPalletsPerOrder} pallets.");
        if (request.Type == OrderType.Outbound && request.Dock is < 1 or > Limits.Docks)
            return CommandResult.Failure($"Choose a dock from 1 to {Limits.Docks}.");

        var dueInMinutes = Math.Clamp(request.DueInMinutes, 5, 24 * 60);
        Order order;
        if (request.Type == OrderType.Outbound)
        {
            var free = FreeStock(sku.Code);
            if (free < request.Quantity)
            {
                return CommandResult.Failure(free == 0
                    ? $"There is no free stock of {sku.Name} right now."
                    : $"Only {PalletCount(free)} of {sku.Name} {(free == 1 ? "is" : "are")} free to send.");
            }
            order = NewOutbound(sku, request.Quantity, request.Dock, request.Priority, Operator, dueInMinutes);
        }
        else
        {
            order = NewInbound(sku, request.Quantity, request.Priority, Operator, dueInMinutes);
        }

        AddOrder(order);
        var next = order.Type == OrderType.Outbound
            ? "Taxis start on it within a few seconds."
            : "Pallets arrive on the infeed one at a time.";
        return CommandResult.Success($"{order.Id} created. {next}", order.Id);
    }

    public CommandResult CancelOrder(string id)
    {
        var order = FindOrder(id);
        if (order is null) return CommandResult.Failure($"Order {id} was not found.");
        if (!order.IsOpen) return CommandResult.Failure($"{order.Id} is already closed.");

        var waiting = _jobs
            .Where(job => job.Order == order && job.Kind == JobKind.Retrieval && job.Status == JobStatus.Waiting)
            .ToList();
        foreach (var job in waiting)
        {
            job.Status = JobStatus.Cancelled;
            _jobs.Remove(job);
            Grid[job.From.Lane].Release(job.Id);
            job.Pallet.OrderId = null;
            order.InFlight--;
        }

        var moving = _jobs.Count(job => job.Order == order);
        order.Status = OrderStatus.Cancelled;
        order.CompletedAt = Now;
        order.Note = moving == 0 ? null : $"{PalletCount(moving)} already on the move finish their trip.";
        _journal.OrderChanged(order);
        Events.Add(Now, EventKind.Order, $"{order.Id} cancelled by the operator", order.Id);

        return CommandResult.Success(moving == 0
            ? $"{order.Id} cancelled."
            : $"{order.Id} cancelled. {PalletCount(moving)} already on the move finish their trip.");
    }

    public CommandResult SetPriority(string id, Priority priority)
    {
        var order = FindOrder(id);
        if (order is null) return CommandResult.Failure($"Order {id} was not found.");
        if (!order.IsOpen) return CommandResult.Failure($"{order.Id} is closed.");

        order.Priority = priority;
        _journal.OrderChanged(order);
        return CommandResult.Success($"{order.Id} is now {priority.ToString().ToLowerInvariant()} priority.");
    }

    public CommandResult SendToCharge(string id)
    {
        if (FindTaxi(id) is not { } taxi) return CommandResult.Failure($"Taxi {id} was not found.");
        if (taxi.FaultCode is not null) return CommandResult.Failure($"{taxi.Id} is stopped. Reset it first.");
        if (!taxi.InService) return CommandResult.Failure($"{taxi.Id} is out of service.");

        taxi.ChargeHold = true;
        return CommandResult.Success(taxi.Job is null
            ? $"{taxi.Id} goes to the dock and charges to {Limits.ChargedBattery:0}% before it takes new jobs."
            : $"{taxi.Id} finishes {taxi.Job.Id}, then charges to {Limits.ChargedBattery:0}%.");
    }

    public CommandResult SetInService(string id, bool inService)
    {
        if (FindTaxi(id) is not { } taxi) return CommandResult.Failure($"Taxi {id} was not found.");
        if (taxi.InService == inService)
            return CommandResult.Failure($"{taxi.Id} is already {(inService ? "in service" : "out of service")}.");
        if (!inService && taxi.Job is not null)
            return CommandResult.Failure($"{taxi.Id} is working on {taxi.Job.Id}. Try again when the job is done.");

        taxi.InService = inService;
        if (inService && taxi.State == TaxiState.OutOfService) taxi.State = TaxiState.Idle;
        Events.Add(Now, EventKind.Info, $"{taxi.Id} {(inService ? "returned to service" : "taken out of service")} by the operator", taxi.Id);
        return CommandResult.Success(inService
            ? $"{taxi.Id} is back in service and can take jobs."
            : $"{taxi.Id} parks at the dock on level {taxi.Level} and takes no new jobs.");
    }

    public CommandResult ResetTaxi(string id)
    {
        if (FindTaxi(id) is not { } taxi) return CommandResult.Failure($"Taxi {id} was not found.");
        if (taxi.FaultCode is null) return CommandResult.Failure($"{taxi.Id} has no fault to reset.");

        ResetTaxiCore(taxi, Operator);
        return CommandResult.Success(taxi.Job is null
            ? $"{taxi.Id} reset."
            : $"{taxi.Id} reset. It carries on with {taxi.Job.Id}.");
    }

    public CommandResult ResetLift(string id)
    {
        if (FindLift(id) is not { } lift) return CommandResult.Failure($"VTU {id} was not found.");
        if (lift.FaultCode is null) return CommandResult.Failure($"{lift.Id} has no fault to reset.");

        ResetLiftCore(lift, Operator);
        return CommandResult.Success($"{lift.Id} reset. Taxis waiting for it carry on.");
    }

    public CommandResult ResetConveyor(string id)
    {
        if (!string.Equals(id, Outfeed.Id, StringComparison.OrdinalIgnoreCase))
            return CommandResult.Failure($"Conveyor {id} has no fault to reset.");
        if (Outfeed.FaultCode is null)
            return CommandResult.Failure("The outfeed is running. There is nothing to reset.");

        ResetOutfeedCore(Operator);
        return CommandResult.Success("Outfeed reset and running.");
    }

    public CommandResult Acknowledge(string alarmId) =>
        Alarms.Acknowledge(alarmId, Now)
            ? CommandResult.Success("Acknowledged. The alarm stays on the list until the cause is fixed.")
            : CommandResult.Failure("That alarm is closed or already acknowledged.");

    public CommandResult RunAlarmAction(string alarmId)
    {
        var alarm = Alarms.Get(alarmId);
        if (alarm is null) return CommandResult.Failure("That alarm is already closed.");

        return alarm.Action switch
        {
            AlarmAction.ResetTaxi => ResetTaxi(alarm.Source),
            AlarmAction.ResetLift => ResetLift(alarm.Source),
            AlarmAction.ResetConveyor => ResetConveyor(Outfeed.Id),
            AlarmAction.Reinduct when alarm.Subject is not null => Reinduct(alarm.Subject),
            _ => CommandResult.Failure("This alarm has no action. It clears by itself when the cause goes away."),
        };
    }

    public CommandResult Reinduct(string palletId)
    {
        if (!_pallets.TryGetValue(palletId, out var pallet) || pallet.Status != PalletStatus.Rejected)
            return CommandResult.Failure($"Pallet {palletId} is not waiting in the reject lane.");
        if (!Infeed.EntryClear)
            return CommandResult.Failure("The infeed is busy. Try again in a few seconds.");

        pallet.HeightMm = Math.Min(pallet.HeightMm, 1700);
        pallet.WeightKg = Math.Min(pallet.WeightKg, 1450);
        pallet.LabelReadable = true;
        pallet.Status = PalletStatus.Arriving;
        pallet.RejectedAt = null;
        Infeed.Add(pallet);
        _journal.PalletChanged(pallet);

        foreach (var alarm in Alarms.Active.Where(alarm => alarm.Subject == pallet.Id).ToList())
            Alarms.Clear(alarm, Now, Operator);

        if (OrderOf(pallet) is { } order)
        {
            order.Rejected = Math.Max(0, order.Rejected - 1);
            if (order.Rejected == 0 && order.Status == OrderStatus.Exception)
            {
                order.Status = OrderStatus.InProgress;
                order.Note = null;
            }
            _journal.OrderChanged(order);
        }

        _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Reinducted, "Reject lane", "Infeed", Operator));
        Events.Add(Now, EventKind.Info, $"{pallet.Id} re-inducted after a fix", pallet.Id);
        return CommandResult.Success($"{pallet.Id} is back on the infeed. It is checked again before it goes into store.");
    }

    public CommandResult SetSpeed(double speed)
    {
        if (!Speeds.Contains(speed)) return CommandResult.Failure("Choose 1×, 2× or 5×.");
        Speed = speed;
        return CommandResult.Success(speed == 1 ? "Running in real time." : $"Demo running {speed:0}× faster than real time.");
    }

    private void ResetTaxiCore(Taxi taxi, string by)
    {
        Alarms.Clear(taxi.FaultCode!, taxi.Id, Now, by);
        taxi.FaultCode = null;
        taxi.FaultSince = null;
        taxi.State = taxi.StateBeforeFault;
        Events.Add(Now, EventKind.Reset, by == Operator
            ? $"{taxi.Id} reset by the operator"
            : $"{taxi.Id} reset by remote support after a camera check", taxi.Id);
    }

    private void ResetLiftCore(Lift lift, string by)
    {
        Alarms.Clear(lift.FaultCode!, lift.Id, Now, by);
        lift.FaultCode = null;
        lift.FaultSince = null;
        lift.State = lift.StateBeforeFault;
        Events.Add(Now, EventKind.Reset, by == Operator
            ? $"{lift.Id} reset by the operator"
            : $"{lift.Id} reset by remote support", lift.Id);
    }

    private void ResetOutfeedCore(string by)
    {
        Alarms.Clear(Outfeed.FaultCode!, Outfeed.Name, Now, by);
        Outfeed.State = ConveyorState.Running;
        Outfeed.FaultCode = null;
        Outfeed.FaultSince = null;
        Events.Add(Now, EventKind.Reset, by == Operator
            ? "Outfeed reset by the operator"
            : "Outfeed reset by remote support", Outfeed.Id);
    }

    private static string PalletCount(int count) => count == 1 ? "1 pallet" : $"{count} pallets";
}

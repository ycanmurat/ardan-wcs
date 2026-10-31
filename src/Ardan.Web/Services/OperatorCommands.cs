using Ardan.Core.Alarms;
using Ardan.Core.Contracts;
using Ardan.Core.Orders;
using Ardan.Web.Components.Ui;

namespace Ardan.Web.Services;

public enum TaxiCommand { Charge, Park, Resume, Reset }

/// <summary>
/// Operator commands shared by every screen. Machine commands come back as a confirmation that says what will happen;
/// the rest run at once. Each result is shown as a toast.
/// </summary>
public sealed class OperatorCommands(WcsApi api, UiState ui)
{
    public static string ChargeHelp(TaxiView taxi) => taxi switch
    {
        { InService: false } => "It is out of service. Return it to service first.",
        { ChargeHold: true } => "Already going to charge to 95%.",
        { JobId: { } job } => $"It finishes {job}, then charges to 95%.",
        _ => "It drives to the dock and charges to 95%.",
    };

    public static string ParkHelp(TaxiView taxi) => taxi.JobId is { } job
        ? $"Busy with {job}. Try again when the job is done."
        : "It parks at the dock and gets no new jobs.";

    public Confirmation Taxi(TaxiView taxi, TaxiCommand command) => command switch
    {
        TaxiCommand.Charge => new(
            $"Send {taxi.Id} to charge?",
            taxi.JobId is null
                ? $"{taxi.Id} drives to the dock and charges to 95% before it takes new jobs."
                : $"{taxi.Id} finishes {taxi.JobId} first, then charges to 95%.",
            "Send to charge",
            () => ShowAsync(api.TaxiAsync(taxi.Id, "charge"))),
        TaxiCommand.Park => new(
            $"Take {taxi.Id} out of service?",
            $"{taxi.Id} parks at the dock on level {taxi.Level} and gets no new jobs. The other Taxis share its work.",
            "Take out of service",
            () => ShowAsync(api.TaxiAsync(taxi.Id, "park"))),
        TaxiCommand.Resume => new(
            $"Return {taxi.Id} to service?",
            $"{taxi.Id} can take jobs again straight away.",
            "Return to service",
            () => ShowAsync(api.TaxiAsync(taxi.Id, "resume"))),
        _ => new(
            $"Reset {taxi.Id}?",
            $"Only reset when the aisle on level {taxi.Level} is clear of people and objects. {taxi.Id} carries on with its job.",
            "Area is clear, reset",
            () => ShowAsync(api.TaxiAsync(taxi.Id, "reset")),
            "danger"),
    };

    public Confirmation ResetLift(LiftView lift) => new(
        $"Reset {lift.Id}?",
        $"Only reset when shaft {lift.Shaft + 1} is clear. Taxis waiting for {lift.Id} carry on.",
        "Shaft is clear, reset",
        () => ShowAsync(api.ResetLiftAsync(lift.Id)),
        "danger");

    public Confirmation ResetConveyor(ConveyorView belt) => new(
        $"Reset the {belt.Name.ToLowerInvariant()}?",
        "Only reset once the jammed pallet has been cleared and nobody is near the rollers.",
        "Jam cleared, reset",
        () => ShowAsync(api.ResetConveyorAsync(belt.Id)),
        "danger");

    public Confirmation AlarmFix(AlarmView alarm)
    {
        var text = alarm.Action switch
        {
            AlarmAction.ResetTaxi => $"Only reset {alarm.Source} once the aisle is clear of people and objects. It carries on with its job.",
            AlarmAction.ResetLift => $"Only reset {alarm.Source} once the shaft is clear. Taxis waiting for it carry on.",
            AlarmAction.ResetConveyor => "Only reset once the jammed pallet is cleared and nobody is near the rollers.",
            _ => "Confirm the pallet has been fixed: restacked, re-wrapped or relabelled. It goes back on the infeed and is checked again.",
        };
        return new(
            $"{alarm.ActionLabel}?",
            text,
            alarm.ActionLabel ?? "Confirm",
            () => ShowAsync(api.AlarmActionAsync(alarm.Id)),
            alarm.Action == AlarmAction.Reinduct ? "primary" : "danger");
    }

    public Confirmation CancelOrder(OrderView order, Func<Task> then) => new(
        $"Cancel {order.Id}?",
        order.InFlight > 0
            ? $"No new work starts. {order.InFlight} {(order.InFlight == 1 ? "pallet is" : "pallets are")} already on the move and finish their trip."
            : "No new work starts and the order closes now.",
        "Cancel order",
        async () =>
        {
            await ShowAsync(api.CancelOrderAsync(order.Id));
            await then();
        },
        "danger");

    public Task AcknowledgeAsync(AlarmView alarm) => ShowAsync(api.AcknowledgeAsync(alarm.Id));

    public Task<CommandResult> SetPriorityAsync(string orderId, Priority priority) => ShowAsync(api.SetPriorityAsync(orderId, priority));

    public Task<CommandResult> AcceptAsync(SuggestionView suggestion) => suggestion switch
    {
        { Order: { } order } => ShowAsync(api.CreateOrderAsync(order)),
        { OrderId: { } orderId } => SetPriorityAsync(orderId, Priority.Urgent),
        _ => Task.FromResult(CommandResult.Failure("This suggestion has nothing to do.")),
    };

    private async Task<CommandResult> ShowAsync(Task<CommandResult> command)
    {
        var result = await command;
        ui.Show(result);
        return result;
    }
}

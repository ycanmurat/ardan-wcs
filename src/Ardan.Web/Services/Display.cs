using System.Globalization;
using Ardan.Core.Alarms;
using Ardan.Core.Engine;
using Ardan.Core.Fleet;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Web.Services;

/// <summary>A state as the screen shows it: a short label, a colour tone and a one-line explanation.</summary>
public sealed record StateLook(string Label, string Tone, string Help);

/// <summary>Formats numbers, times and states the same way on every screen.</summary>
public static class Display
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly TimeZoneInfo SiteZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Dublin");

    public static string Num(double value, string format = "0.##") => value.ToString(format, Invariant);

    public static string Count(int value) => value.ToString("N0", Invariant);

    public static string Percent(double share) => $"{Math.Round(share * 100).ToString("0", Invariant)}%";

    public static DateTimeOffset Local(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, SiteZone);

    public static string Clock(DateTimeOffset time) => Local(time).ToString("HH:mm:ss", Invariant);

    public static string Time(DateTimeOffset time) => Local(time).ToString("HH:mm", Invariant);

    public static string Date(DateTimeOffset time) => Local(time).ToString("d MMM, HH:mm", Invariant);

    public static string Day(DateOnly date) => date.ToString("d MMM yyyy", Invariant);

    public static string Ago(DateTimeOffset time, DateTimeOffset now)
    {
        var span = now - time;
        if (span < TimeSpan.FromSeconds(45)) return "just now";
        if (span < TimeSpan.FromHours(1)) return $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} min ago";
        if (span < TimeSpan.FromDays(1)) return $"{(int)span.TotalHours} h ago";
        return Date(time);
    }

    public static string Due(DateTimeOffset due, DateTimeOffset now)
    {
        var span = due - now;
        if (span.Duration() < TimeSpan.FromMinutes(1)) return "due now";
        var minutes = (int)Math.Round(span.Duration().TotalMinutes);
        var text = minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60:00} min";
        return span > TimeSpan.Zero ? $"in {text}" : $"{text} late";
    }

    public static int DaysLeft(DateOnly expiry, DateTimeOffset now) =>
        expiry.DayNumber - DateOnly.FromDateTime(Local(now).Date).DayNumber;

    public static string Expiry(DateOnly expiry, DateTimeOffset now) => DaysLeft(expiry, now) switch
    {
        < 0 => "expired",
        0 => "today",
        1 => "tomorrow",
        var days and < 14 => $"in {days} days",
        _ => Day(expiry),
    };

    public static string ExpiryTone(DateOnly expiry, DateTimeOffset now) => DaysLeft(expiry, now) switch
    {
        < 0 => "crit",
        <= 3 => "warn",
        _ => "neutral",
    };

    public static StateLook Look(TaxiState state) => state switch
    {
        TaxiState.Idle => new("Ready", "neutral", "Parked at the dock with a full battery. It takes the next job it is given."),
        TaxiState.Charging => new("Charging", "ok", "Topping up at the dock. Taxis charge whenever they stop there and take jobs again above 30%."),
        TaxiState.Driving => new("Driving", "info", "Moving along its level: 1.8 m/s empty, 1.4 m/s with a pallet."),
        TaxiState.WaitingForAisle => new("Waiting", "warn", "Waiting at the dock for the aisle to clear. Traffic rule: one Taxi per aisle at a time."),
        TaxiState.WaitingForLift => new("Waiting for VTU", "warn", "Waiting at the dock for a VTU to take it to another level."),
        TaxiState.OnLift => new("On VTU", "info", "Riding a VTU between levels, with or without a pallet."),
        TaxiState.Loading => new("Picking up", "brand", "Reaching into the lane to lift a pallet. Deeper places take longer."),
        TaxiState.Unloading => new("Setting down", "brand", "Putting a pallet into a lane or onto the outfeed."),
        TaxiState.Fault => new("Stopped", "crit", "Stopped by a safety sensor. It needs a check and a reset before it moves again."),
        _ => new("Out of service", "neutral", "Taken out of service by an operator. It gets no jobs until it is returned to service."),
    };

    public static StateLook Look(LiftState state) => state switch
    {
        LiftState.Idle => new("Idle", "neutral", "Waiting at a level for the next ride."),
        LiftState.Moving => new("Moving", "info", "Travelling empty to collect a Taxi."),
        LiftState.Boarding => new("Boarding", "brand", "A Taxi is driving on."),
        LiftState.Carrying => new("Carrying", "info", "Carrying a Taxi between levels at up to 1.2 m/s."),
        LiftState.Leaving => new("Unloading", "brand", "A Taxi is driving off."),
        _ => new("Stopped", "crit", "Stopped by a drive fault. Reset it once the area is clear."),
    };

    public static StateLook Look(OrderStatus status) => status switch
    {
        OrderStatus.Queued => new("Queued", "neutral", "Waiting for a Taxi, or for the first pallet to arrive."),
        OrderStatus.InProgress => new("In progress", "info", "Taxis are working on it."),
        OrderStatus.Short => new("Short of stock", "warn", "Not enough stock in store to finish. It carries on when stock arrives."),
        OrderStatus.Exception => new("Needs attention", "warn", "A pallet failed the profile check. Re-induct it from Alarms."),
        OrderStatus.Completed => new("Completed", "ok", "Every pallet reached the dock or was stored."),
        _ => new("Cancelled", "neutral", "Cancelled by an operator."),
    };

    public static StateLook Look(Priority priority) => priority switch
    {
        Priority.Urgent => new("Urgent", "crit", "Served before everything else."),
        Priority.High => new("High", "warn", "Served before normal orders."),
        _ => new("Normal", "neutral", "Served in due-time order."),
    };

    public static StateLook Look(Severity severity) => severity switch
    {
        Severity.Critical => new("Critical", "crit", "Something has stopped. It needs a person."),
        Severity.Warning => new("Warning", "warn", "Worth a look. The store keeps running."),
        _ => new("Info", "info", "For your information."),
    };

    public static StateLook Look(Velocity velocity) => velocity switch
    {
        Velocity.A => new("A · fast", "brand", "Moves often. Stored close to the VTUs so Taxis reach it quickly."),
        Velocity.B => new("B · medium", "info", "Moves now and then. Stored in the middle of the rack."),
        _ => new("C · slow", "neutral", "Moves rarely. Stored at the back, leaving the best places for fast movers."),
    };

    public static (string Icon, string Tone) Look(EventKind kind) => kind switch
    {
        EventKind.Stored => ("arrow-down", "ok"),
        EventKind.Retrieved => ("arrow-up", "info"),
        EventKind.Dispatched => ("dock", "info"),
        EventKind.Received => ("check", "ok"),
        EventKind.Rejected => ("warning", "warn"),
        EventKind.Order => ("orders", "neutral"),
        EventKind.Alarm => ("alarm", "crit"),
        EventKind.Reset => ("reset", "brand"),
        _ => ("info", "neutral"),
    };

    public static string Label(MovementKind kind) => kind switch
    {
        MovementKind.Received => "Passed the profile check",
        MovementKind.Rejected => "Rejected at the profile check",
        MovementKind.Reinducted => "Re-inducted after a fix",
        MovementKind.Returned => "Returned to the supplier",
        MovementKind.Stored => "Stored",
        MovementKind.Retrieved => "Picked from its lane",
        _ => "Sent to the dock",
    };

    public static string Label(PalletStatus status) => status switch
    {
        PalletStatus.Arriving => "Arriving",
        PalletStatus.Stored => "In store",
        PalletStatus.Moving => "Moving",
        PalletStatus.Dispatched => "Dispatched",
        PalletStatus.Rejected => "Rejected",
        _ => "Returned",
    };
}

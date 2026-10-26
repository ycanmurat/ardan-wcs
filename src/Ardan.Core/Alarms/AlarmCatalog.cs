namespace Ardan.Core.Alarms;

/// <summary>Alarm texts written for the person on shift: what happened, why, and what to do next.</summary>
public static class AlarmCatalog
{
    private sealed record Template(
        Severity Severity,
        string Title,
        string What,
        string Cause,
        string[] Steps,
        AlarmAction Action = AlarmAction.None,
        string? ActionLabel = null);

    private static readonly Dictionary<string, Template> Templates = new()
    {
        ["TX-OBS"] = new(
            Severity.Critical,
            "Taxi stopped by its obstacle sensor",
            "{source} stopped on level {level} near bay {bay}. Its front sensor saw something in the aisle.",
            "Loose stretch wrap or a broken pallet board hanging into the aisle, or debris on the rail.",
            [
                "Check the camera for level {level}, near bay {bay}.",
                "Clear the object only if it is safe. Follow the lock-out procedure before anyone enters the rack.",
                "Press Reset {source}. It carries on with the job it was doing.",
            ],
            AlarmAction.ResetTaxi,
            "Reset {source}"),

        ["TX-BAT"] = new(
            Severity.Warning,
            "Taxi battery low",
            "{source} is at {value}%. It finishes its current job and then charges at the dock.",
            "A long run of jobs without a break at the dock.",
            [
                "No action needed. The alarm clears itself once the battery is back above 30%.",
                "If it keeps coming back, check the charging contacts at the dock on level {level}.",
            ]),

        ["TX-NET"] = new(
            Severity.Warning,
            "Weak Wi-Fi signal",
            "{source} missed two heartbeats on the mesh Wi-Fi network on level {level}.",
            "Interference, or an access point restarting.",
            [
                "No action needed if it clears within a minute.",
                "If it happens again on the same level, ask IT to check the access point there.",
            ]),

        ["VTU-INV"] = new(
            Severity.Critical,
            "VTU drive fault",
            "{source} stopped after its drive reported an overcurrent fault (F0071). Taxis waiting for it are paused.",
            "A short power dip, or a carriage that is overloaded or out of line.",
            [
                "Check that the VTU area is clear and that no damaged pallet sits on the carriage.",
                "Press Reset {source}.",
                "If the fault comes back within an hour, call the 24/7 support line.",
            ],
            AlarmAction.ResetLift,
            "Reset {source}"),

        ["CNV-JAM"] = new(
            Severity.Warning,
            "Pallet stuck on the outfeed",
            "The outfeed stopped because pallet {pallet} did not reach the next sensor in time.",
            "A pallet board caught on a roller, or a dirty photo-eye.",
            [
                "Go to the dock end of the outfeed and check the pallet.",
                "Free it only if it is safe to do so.",
                "Press Reset outfeed.",
            ],
            AlarmAction.ResetConveyor,
            "Reset outfeed"),

        ["PRF-HGT"] = new(
            Severity.Warning,
            "Pallet too tall",
            "Pallet {pallet} measured {value} mm at the profile check. The limit is 1,800 mm, so it went to the reject lane.",
            "Load stacked too high, or wrap loose at the top.",
            [
                "Collect the pallet from the reject lane.",
                "Re-stack or re-wrap it under 1,800 mm.",
                "Press Re-induct pallet to send it back in.",
            ],
            AlarmAction.Reinduct,
            "Re-induct pallet"),

        ["PRF-WGT"] = new(
            Severity.Warning,
            "Pallet too heavy",
            "Pallet {pallet} weighed {value} kg at the profile check. A Taxi carries up to 1,500 kg, so it went to the reject lane.",
            "A mixed or double-stacked pallet, or the wrong product on the delivery note.",
            [
                "Collect the pallet from the reject lane.",
                "Split the load or check it against the delivery note.",
                "Press Re-induct pallet to send it back in.",
            ],
            AlarmAction.Reinduct,
            "Re-induct pallet"),

        ["PRF-LBL"] = new(
            Severity.Warning,
            "Pallet label unreadable",
            "The scanner could not read the SSCC label on pallet {pallet}, so it went to the reject lane.",
            "Label on the wrong side, torn, or covered by wrap.",
            [
                "Collect the pallet from the reject lane.",
                "Put a new label on the scanner side.",
                "Press Re-induct pallet to send it back in.",
            ],
            AlarmAction.Reinduct,
            "Re-induct pallet"),

        ["ENV-TMP"] = new(
            Severity.Warning,
            "Store temperature above range",
            "The chilled store is at {value} °C. The target range is 0 to 5 °C.",
            "A dock door left open, or a defrost cycle.",
            [
                "Check that the dock doors are closed.",
                "If it stays above 5 °C for 15 minutes, follow the cold-chain procedure.",
            ]),

        ["STR-FULL"] = new(
            Severity.Warning,
            "No free lane for a pallet",
            "Pallet {pallet} ({sku}) is waiting at the infeed. There is no free lane that fits the storage rules.",
            "The store is nearly full, or the lanes that fit are busy.",
            [
                "Send out stock that is due, or merge part-full lanes of the same batch.",
                "The pallet waits at the infeed and is stored as soon as a lane frees up.",
            ]),
    };

    public static Alarm Create(
        string id,
        string code,
        string source,
        DateTimeOffset now,
        IReadOnlyDictionary<string, string>? values = null,
        string? subject = null)
    {
        var template = Templates.TryGetValue(code, out var found)
            ? found
            : throw new ArgumentException($"Unknown alarm code {code}.", nameof(code));

        string Fill(string text)
        {
            text = text.Replace("{source}", source);
            foreach (var (key, value) in values ?? new Dictionary<string, string>())
                text = text.Replace($"{{{key}}}", value);
            return text;
        }

        return new Alarm
        {
            Id = id,
            Code = code,
            Severity = template.Severity,
            Source = source,
            Title = template.Title,
            What = Fill(template.What),
            Cause = Fill(template.Cause),
            Steps = template.Steps.Select(Fill).ToArray(),
            Action = template.Action,
            ActionLabel = template.ActionLabel is null ? null : Fill(template.ActionLabel),
            Subject = subject,
            RaisedAt = now,
            State = AlarmState.Active,
        };
    }
}

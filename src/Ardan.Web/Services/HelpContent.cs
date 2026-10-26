namespace Ardan.Web.Services;

public sealed record HelpTopic(string Title, string Intro, IReadOnlyList<string> Points);

/// <summary>Short help for each page, opened with the ? key or the Help button.</summary>
public static class HelpContent
{
    public static IReadOnlyList<(string Keys, string Action)> Shortcuts { get; } =
    [
        ("⌘K", "Search pallets, orders, products and machines"),
        ("?", "Open or close this help"),
        ("Esc", "Close panels and dialogs"),
    ];

    public static HelpTopic For(string page) => page switch
    {
        "live" => new(
            "Live system",
            "A front view of the rack, as if you were standing at the dock.",
            [
                "Each box is a lane. The dots show how many pallets it holds, up to six.",
                "Taxis drive along the rails. The two VTUs on the left carry them between levels.",
                "Amber dots are stock that expires within three days.",
                "Type a product in Find to light up its lanes. Click a lane or a Taxi to see what is inside or what it is doing.",
            ]),
        "fleet" => new(
            "Fleet",
            "Every machine, what it is doing and what you can do with it.",
            [
                "Click a row for details and actions.",
                "Send to charge: the Taxi finishes its job, then charges to 95%.",
                "Take out of service: it parks at the dock and gets no new jobs. Use this before maintenance.",
                "Reset only appears when a machine has stopped. Check the area first.",
            ]),
        "orders" => new(
            "Orders",
            "Work from the ERP, plus any orders you add here.",
            [
                "Outbound orders send pallets to a dock. Inbound orders receive pallets through the infeed.",
                "Urgent and high priority orders are served first, then by due time.",
                "Progress counts pallets that reached the dock or were stored.",
                "Cancelling stops new work. Pallets already on a Taxi finish their trip.",
            ]),
        "inventory" => new(
            "Inventory",
            "What is in store, where it is, and when it expires.",
            [
                "Each lane holds one batch, so the oldest stock always leaves first (FEFO).",
                "Fast movers (A) sit close to the VTUs; slow movers (C) further back.",
                "Click a pallet to see its full history, from the infeed to now.",
            ]),
        "alarms" => new(
            "Alarms",
            "Each alarm says what happened, the likely cause and the steps to fix it.",
            [
                "Acknowledge tells the team you are on it. The alarm stays until the cause is fixed.",
                "Some alarms have an action button, such as Reset or Re-induct pallet.",
                "Warnings like low battery or weak Wi-Fi clear by themselves.",
                "If nobody resets a stopped machine within four minutes, remote support checks the camera and resets it.",
            ]),
        "insights" => new(
            "Insights",
            "Planned features for the next phase. Nothing on this page acts on the store yet.",
            [
                "Each card says what the feature does, which data it uses and how it stays safe.",
                "Suggestions always need a person to approve them before anything moves.",
            ]),
        _ => new(
            "Overview",
            "A summary of the whole store, updated four times a second.",
            [
                "Hover any ⓘ to see what a number means and how it is counted.",
                "The fleet strip shows each Taxi and VTU. Click one to open it in Fleet.",
                "Needs attention lists alarms with the first thing to do. Open Alarms for the full steps.",
                "Use the search box, or press ⌘K, to find any pallet, order, product or machine.",
            ]),
    };
}

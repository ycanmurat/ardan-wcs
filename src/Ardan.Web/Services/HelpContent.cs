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
            "The whole store in 3D, run like a game: keep the work flowing, fix stops fast and the shift scores points.",
            [
                "Drag to turn, right-drag or Shift + drag to move, scroll to zoom. On a touch screen one finger turns and two fingers move and zoom. Q and E turn, 0 shows the whole store, F is focus mode.",
                "Green arrows lead a Taxi into store and blue arrows out of store. A frame marks the place it is going to.",
                "Click a Taxi, VTU, conveyor or lane to see it on the right with its actions. A red ! marks a machine that needs you: hover it to see what happened.",
                "Points: 10 per pallet stored, 15 per pallet sent, 50 per order on time, 25 for each stop you fix. Late orders and stops left for remote support cost points.",
                "Stars follow how many of the last 20 orders finished on time. Goals pay a reward and come back harder.",
                "Offers suggest work worth doing now. Nothing changes until you accept one.",
                "2D in the right-hand toolbar shows the flat rack drawing. The layers button colours lanes and finds a product.",
            ]),
        "fleet" => new(
            "Fleet",
            "Every machine, what it is doing and what you can do with it.",
            [
                "Each card shows the battery, the level, where the machine is going and what it carries. Details opens the rest.",
                "Send to charge: the Taxi finishes its job, then charges to 95%.",
                "Take out of service (Park): it parks at the dock and gets no new jobs. Use this before maintenance.",
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
            "A summary of the whole store, updated every second.",
            [
                "The top row is this shift: points, level, stars and the next goals, next to the state of the system.",
                "Hover any ⓘ to see what a number means and how it is counted.",
                "The fleet shows each Taxi with its battery ring and each VTU with its level. Click one to open it in Fleet.",
                "Needs attention lists alarms with the first thing to do and the fix button. Offers suggest work worth doing now.",
                "Use the search box, or press ⌘K, to find any pallet, order, product or machine.",
            ]),
    };
}

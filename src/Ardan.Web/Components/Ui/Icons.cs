namespace Ardan.Web.Components.Ui;

/// <summary>Line icons drawn on a 24 × 24 grid.</summary>
public static class Icons
{
    private const string Circle = "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z";

    private static readonly Dictionary<string, string> Paths = new()
    {
        ["overview"] = "M4 4h7v7H4z M13 4h7v4h-7z M13 10h7v10h-7z M4 13h7v7H4z",
        ["live"] = "M3 12h4l3-7 4 14 3-7h4",
        ["taxi"] = "M3 11h18v5H3z M6 16v2 M18 16v2 M7 7h10v4H7z",
        ["lift"] = "M6 3h12v18H6z M12 6v12 M9.5 8.5 12 6l2.5 2.5 M9.5 15.5 12 18l2.5-2.5",
        ["conveyor"] = "M3 15h18v3H3z M6 10h5v5H6z M13 10h5v5h-5z",
        ["orders"] = "M9 3h6v3H9z M7 5H5v16h14V5h-2 M8.5 11h7 M8.5 15h4.5",
        ["inventory"] = "M3 7.5 12 3l9 4.5-9 4.5z M3 7.5v9L12 21l9-4.5v-9 M12 12v9",
        ["alarm"] = "M6 17v-6a6 6 0 0 1 12 0v6l1.5 1.5h-15z M10 21h4",
        ["insights"] = "M9 18h6 M10 21h4 M12 3a6 6 0 0 0-3.6 10.8c.7.5 1.1 1.3 1.1 2.2h5c0-.9.4-1.7 1.1-2.2A6 6 0 0 0 12 3z",
        ["search"] = "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z M20 20l-4.2-4.2",
        ["help"] = Circle + " M9.6 9.4a2.5 2.5 0 1 1 3.4 2.3c-.6.3-1 .9-1 1.6v.4 M12 17h.01",
        ["info"] = Circle + " M12 11v5 M12 7.8h.01",
        ["bolt"] = "M13 3 5 14h6l-1 7 8-11h-6z",
        ["check"] = "M5 12.5 9.5 17 19 7.5",
        ["x"] = "M6 6l12 12 M18 6 6 18",
        ["plus"] = "M12 5v14 M5 12h14",
        ["minus"] = "M5 12h14",
        ["chevron-right"] = "M9 6l6 6-6 6",
        ["chevron-down"] = "M6 9l6 6 6-6",
        ["arrow-right"] = "M5 12h14 M13 6l6 6-6 6",
        ["arrow-down"] = "M12 4v15 M6 13l6 6 6-6",
        ["arrow-up"] = "M12 20V5 M6 11l6-6 6 6",
        ["wifi"] = "M4 9.5a12 12 0 0 1 16 0 M7 13a7.5 7.5 0 0 1 10 0 M10 16.5a3 3 0 0 1 4 0 M12 19.5h.01",
        ["thermometer"] = "M14 14.8V5a2 2 0 1 0-4 0v9.8a4 4 0 1 0 4 0z",
        ["reset"] = "M4 12a8 8 0 1 0 2.4-5.7 M4 4.5V8h3.5",
        ["pause"] = "M8 5h3v14H8z M13 5h3v14h-3z",
        ["play"] = "M8 5v14l11-7z",
        ["sun"] = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z M12 2.5v2 M12 19.5v2 M4.9 4.9l1.4 1.4 M17.7 17.7l1.4 1.4 M2.5 12h2 M19.5 12h2 M4.9 19.1l1.4-1.4 M17.7 6.3l1.4-1.4",
        ["moon"] = "M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z",
        ["clock"] = Circle + " M12 7v5l3.2 2",
        ["dock"] = "M2.5 7h11v9h-11z M13.5 10H18l3 3v3h-7.5 M4.5 18a1.5 1.5 0 1 0 3 0 1.5 1.5 0 1 0-3 0 M15.5 18a1.5 1.5 0 1 0 3 0 1.5 1.5 0 1 0-3 0",
        ["pallet"] = "M5 6h14v8H5z M4 17h16 M6 17v2 M12 17v2 M18 17v2",
        ["warning"] = "M12 3.5 2.5 20h19z M12 10v4.5 M12 17.3h.01",
        ["calendar"] = "M4 6h16v14H4z M8 3.5V8 M16 3.5V8 M4 10.5h16",
        ["layers"] = "M12 3l9 5-9 5-9-5z M3 13l9 5 9-5",
        ["map"] = "M4 5h16v14H4z M4 11h16 M10 5v14",
        ["user"] = "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M4.5 21a7.5 7.5 0 0 1 15 0",
        ["link"] = "M10 14a4.5 4.5 0 0 0 6.4 0l3-3a4.5 4.5 0 0 0-6.4-6.4l-1 1 M14 10a4.5 4.5 0 0 0-6.4 0l-3 3a4.5 4.5 0 0 0 6.4 6.4l1-1",
        ["database"] = "M12 3c4.4 0 8 1.3 8 3s-3.6 3-8 3-8-1.3-8-3 3.6-3 8-3z M4 6v6c0 1.7 3.6 3 8 3s8-1.3 8-3V6 M4 12v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6",
        ["shield"] = "M12 3l7 3v5c0 4.5-3 8.2-7 10-4-1.8-7-5.5-7-10V6z M9 12l2 2 4-4",
        ["chat"] = "M5 5h14v10H9l-4 4z",
        ["route"] = "M6 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M18 9a2 2 0 1 0 0-4 2 2 0 0 0 0 4z M8 17h7a3 3 0 0 0 0-6H9a3 3 0 0 1 0-6h7",
        ["gauge"] = "M4 17a8 8 0 1 1 16 0 M12 17l4-5",
        ["wrench"] = "M14.5 6.5a4 4 0 0 0 5 5L11 20a2 2 0 0 1-3-3l8.5-8.5z",
        ["code"] = "M8 7l-5 5 5 5 M16 7l5 5-5 5 M13.5 4l-3 16",
        ["rotate-left"] = "M4 12a8 8 0 1 0 2.4-5.7 M4 4.5V8h3.5",
        ["rotate-right"] = "M20 12a8 8 0 1 1-2.4-5.7 M20 4.5V8h-3.5",
        ["home"] = "M4 11.5 12 5l8 6.5 M6.5 9.5V19h11V9.5 M10 19v-5h4v5",
        ["expand"] = "M4 9V4h5 M15 4h5v5 M20 15v5h-5 M9 20H4v-5",
        ["compress"] = "M9 4v5H4 M20 9h-5V4 M15 20v-5h5 M4 15h5v5",
        ["tag"] = "M3.5 12.5V4h8.5l8.5 8.5-8.5 8.5z M8 8h.01",
        ["flag"] = "M5 21V4 M5 4h11l-2 4 2 4H5",
        ["spark"] = "M12 3v4 M12 17v4 M3 12h4 M17 12h4 M6 6l2.5 2.5 M15.5 15.5 18 18 M18 6l-2.5 2.5 M8.5 15.5 6 18",
        ["trophy"] = "M8 4h8v5a4 4 0 0 1-8 0z M8 6H4.5a3.5 3.5 0 0 0 4 4 M16 6h3.5a3.5 3.5 0 0 1-4 4 M12 13v4 M8.5 20h7 M10 17h4",
        ["external"] = "M14 4h6v6 M20 4l-9 9 M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5",
    };

    public static string PathOf(string name) => Paths.TryGetValue(name, out var path) ? path : Paths["info"];
}

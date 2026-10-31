using Ardan.Core.Contracts;
using Ardan.Core.Engine;

namespace Ardan.Web.Components.Game;

/// <summary>The fixed shape of the site, sent once when the 3D view starts.</summary>
public sealed record WorldSetup(
    int Levels,
    int Bays,
    int LaneDepth,
    double BayPitchMetres,
    double LevelHeightMetres,
    double PalletPitchMetres,
    IReadOnlyList<string> Lifts,
    IReadOnlyList<WorldBeltSetup> Belts,
    double BeltLength,
    double ProfileCheckAt,
    int Docks);

public sealed record WorldBeltSetup(string Id, string Name, bool Inbound);

/// <summary>
/// What the 3D view draws next. Texts and tones are worked out here, so the view only places things.
/// Lanes holds the lanes that changed since the last frame. A spot names a machine, a lane (L2·B05), a dock (Dock 2) or Reject.
/// </summary>
public sealed record WorldFrame(
    double Speed,
    IReadOnlyList<WorldTaxi> Taxis,
    IReadOnlyList<WorldLift> Lifts,
    IReadOnlyList<WorldBelt> Belts,
    IReadOnlyList<WorldLane> Lanes,
    IReadOnlyList<WorldMarker> Markers,
    IReadOnlyList<ScoreEvent> Events,
    int Rejects,
    string RejectTip,
    string? Highlight);

/// <summary>
/// A Taxi as the 3D view draws it. Flow is "in" for putaway and "out" for retrieval. Phase is "pick" or "drop" while it
/// moves a pallet between its satellite and a lane or conveyor, and Handling says how far through that it is, from 0 to 1.
/// </summary>
public sealed record WorldTaxi(
    string Id,
    int Level,
    double X,
    string? Lift,
    bool Loaded,
    string Tone,
    string Tip,
    string Label,
    string? Flow,
    string? From,
    string? To,
    string? Phase,
    double? Handling);

public sealed record WorldLift(string Id, int Shaft, double Position, string Tone, string Tip);

public sealed record WorldBelt(string Id, bool Running, double Length, IReadOnlyList<ConveyorItemView> Items, string Tip);

public sealed record WorldLane(int Level, int Bay, int Count, string? Tone, bool Busy, bool Lit, bool Dim, string Tip);

public sealed record WorldMarker(string Spot, string Alarm, string Tone, string Title, string Detail);

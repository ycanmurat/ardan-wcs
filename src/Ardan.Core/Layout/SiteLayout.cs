namespace Ardan.Core.Layout;

/// <summary>The shape of the rack: how many levels and bays, and how deep each lane is.</summary>
public sealed record SiteLayout(
    int Levels,
    int Bays,
    int LaneDepth,
    double BayPitchMetres,
    double LevelHeightMetres,
    double PalletPitchMetres)
{
    public static SiteLayout Default { get; } = new(
        Levels: 6,
        Bays: 18,
        LaneDepth: 6,
        BayPitchMetres: 1.4,
        LevelHeightMetres: 1.9,
        PalletPitchMetres: 1.25);

    public int LaneCount => Levels * Bays;

    public int Capacity => LaneCount * LaneDepth;
}

using Ardan.Core.Layout;
using Ardan.Web.Services;

namespace Ardan.Web.Components.Rack;

public enum LaneColour { Fill, Velocity, Expiry }

/// <summary>
/// Places things on the front view of the rack. Sizes are in drawing units;
/// the screen scales them, so HTML markers use percentages of the drawing.
/// </summary>
public sealed class RackGeometry(int levels, int bays)
{
    public const double LabelWidth = 36;
    public const double FrontWidth = 160;
    public const double BayWidth = 50;
    public const double LevelHeight = 72;
    public const double TopMargin = 22;
    public const double BottomMargin = 92;
    public const double RightMargin = 12;
    public const double ShaftWidth = 40;
    public const double TaxiWidth = 38;
    public const double TaxiHeight = 16;
    public const double CarriageHeight = 24;
    public const double ParcelSize = 15;
    private const double ShaftGap = 50;
    private const double LaneInset = 4;
    private const double LaneTop = 7;
    private const double LaneHeight = 36;
    private const double RailLift = 3;
    private const double BeltSpan = 420;
    private const double BeltHeight = 14;

    public int Levels { get; } = levels;
    public int Bays { get; } = bays;
    public double Width => LabelWidth + FrontWidth + Bays * BayWidth + RightMargin;
    public double Height => TopMargin + Levels * LevelHeight + BottomMargin;
    public double RackLeft => LabelWidth + FrontWidth;
    public double RackRight => RackLeft + Bays * BayWidth;
    public double DockLeft => RackLeft - BayWidth;
    public double Ground => FloorY(0);
    public double BeltLeft => DockLeft;
    public double BeltRight => DockLeft + BeltSpan;
    public double InfeedY => Ground + 22;
    public double OutfeedY => Ground + 56;
    public double GateX => BeltRight - Motion.ProfileCheckAt / Motion.ConveyorLength * BeltSpan;

    public double FloorY(double level) => TopMargin + (Levels - level) * LevelHeight;

    public double RailY(int level) => FloorY(level) - RailLift;

    public double LevelMiddle(int level) => FloorY(level) - LevelHeight / 2;

    public double ShaftCentre(int shaft) => LabelWidth + 28 + shaft * ShaftGap;

    public double BayCentre(double x) => RackLeft + (x - 0.5) * BayWidth;

    public string LaneBox(int level, int bay) => Box(
        RackLeft + (bay - 1) * BayWidth + LaneInset,
        FloorY(level + 1) + LaneTop,
        BayWidth - 2 * LaneInset,
        LaneHeight);

    public string TaxiAt(double x, int level) => Spot(BayCentre(x) - TaxiWidth / 2, RailY(level) - TaxiHeight);

    public string TaxiOnLift(int shaft, double position) =>
        Spot(ShaftCentre(shaft) - TaxiWidth / 2, FloorY(position) - RailLift - TaxiHeight);

    public string Carriage(int shaft, double position) =>
        Spot(ShaftCentre(shaft) - ShaftWidth / 2 + 2, FloorY(position) - CarriageHeight);

    public string Parcel(bool inbound, double position, double length)
    {
        var share = Math.Clamp(position / length, 0, 1);
        var centre = inbound ? BeltRight - share * BeltSpan : BeltLeft + share * BeltSpan;
        var belt = inbound ? InfeedY : OutfeedY;
        return Spot(centre - ParcelSize / 2, belt - ParcelSize + 1);
    }

    public string Belt(double y) => Box(BeltLeft, y, BeltSpan, BeltHeight);

    public string Spot(double x, double y) => $"left: {X(x)}; top: {Y(y)}";

    public string Box(double x, double y, double width, double height) =>
        $"left: {X(x)}; top: {Y(y)}; width: {X(width)}; height: {Y(height)}";

    public string X(double units) => Pct(units / Width);

    public string Y(double units) => Pct(units / Height);

    private static string Pct(double share) => $"{Display.Num(share * 100, "0.###")}%";
}

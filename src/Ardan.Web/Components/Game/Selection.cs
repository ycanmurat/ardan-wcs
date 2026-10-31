using Ardan.Core.Layout;

namespace Ardan.Web.Components.Game;

public enum SelectionKind { Taxi, Lift, Belt, Lane, Alarm, Reject, Order }

/// <summary>What the operator picked in the live view. Lanes use the same id as everywhere else, such as L2·B05.</summary>
public sealed record Selection(SelectionKind Kind, string Id)
{
    public static Selection Lane(int level, int bay) => new(SelectionKind.Lane, new LaneId(level, bay).ToString());

    public bool TryLane(out LaneId lane)
    {
        lane = default;
        if (Kind != SelectionKind.Lane || !Id.StartsWith('L')) return false;
        var parts = Id[1..].Split("·B");
        if (parts.Length != 2 || !int.TryParse(parts[0], out var level) || !int.TryParse(parts[1], out var bay)) return false;
        lane = new LaneId(level, bay);
        return true;
    }
}

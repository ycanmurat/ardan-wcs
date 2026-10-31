using Ardan.Core.Contracts;
using Ardan.Core.Stock;
using Ardan.Web.Services;

namespace Ardan.Web.Components.Rack;

/// <summary>The colour tone of a lane for each way of colouring the rack. Empty lanes have none.</summary>
public static class LaneTone
{
    public static string? Of(LaneView lane, LaneColour colour, IReadOnlyDictionary<string, Velocity>? velocities, DateTimeOffset now)
    {
        if (lane.Count == 0 || lane.SkuCode is null) return null;
        var daysLeft = lane.Expiry is { } expiry ? Display.DaysLeft(expiry, now) : int.MaxValue;
        return colour switch
        {
            LaneColour.Velocity when velocities?.TryGetValue(lane.SkuCode, out var velocity) == true => $"v-{velocity.ToString().ToLowerInvariant()}",
            LaneColour.Expiry => daysLeft switch
            {
                < 0 => "expired",
                <= 3 => "soon",
                <= 7 => "week",
                _ => "fresh",
            },
            _ => daysLeft <= 3 ? "soon" : "fill",
        };
    }
}

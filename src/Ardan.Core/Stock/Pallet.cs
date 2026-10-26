using System.Text.Json.Serialization;
using Ardan.Core.Layout;

namespace Ardan.Core.Stock;

[JsonConverter(typeof(JsonStringEnumConverter<PalletStatus>))]
public enum PalletStatus { Arriving, Stored, Moving, Dispatched, Rejected, Returned }

public enum ProfileFault { None, TooTall, TooHeavy, LabelUnreadable }

/// <summary>One pallet, tracked from the infeed to the dock.</summary>
public sealed class Pallet
{
    public required string Id { get; init; }
    public required string SkuCode { get; init; }
    public required string Batch { get; init; }
    public required DateOnly Expiry { get; init; }
    public int WeightKg { get; set; }
    public int HeightMm { get; set; }
    public bool LabelReadable { get; set; } = true;
    public PalletStatus Status { get; set; }
    public LaneId? Lane { get; set; }
    public int Slot { get; set; }
    public string? OrderId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? RejectedAt { get; set; }

    public ProfileFault CheckProfile()
    {
        if (HeightMm > Limits.MaxPalletHeightMm) return ProfileFault.TooTall;
        if (WeightKg > Limits.MaxPalletWeightKg) return ProfileFault.TooHeavy;
        return LabelReadable ? ProfileFault.None : ProfileFault.LabelUnreadable;
    }
}

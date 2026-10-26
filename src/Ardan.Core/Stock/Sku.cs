using System.Text.Json.Serialization;

namespace Ardan.Core.Stock;

[JsonConverter(typeof(JsonStringEnumConverter<Velocity>))]
public enum Velocity { A, B, C }

/// <summary>A product line as the ERP knows it. Velocity A moves often, C rarely.</summary>
public sealed record Sku(
    string Code,
    string Name,
    string Category,
    Velocity Velocity,
    int CasesPerPallet,
    int ShelfLifeDays);

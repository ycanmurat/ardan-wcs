namespace Ardan.Core.Layout;

/// <summary>Address of a storage lane. Level 0 is the ground floor and bay 1 is next to the lifts.</summary>
public readonly record struct LaneId(int Level, int Bay)
{
    public override string ToString() => $"L{Level}·B{Bay:00}";
}

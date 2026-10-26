namespace Ardan.Core.Fleet;

/// <summary>One step of a Taxi route.</summary>
public abstract record Leg;

/// <summary>Drive along the current level to a position, counted in bays from the lifts.</summary>
public sealed record Drive(double ToX) : Leg;

/// <summary>Ride a VTU from one level to another.</summary>
public sealed record Ride(int FromLevel, int ToLevel) : Leg;

/// <summary>Pick up or set down a pallet.</summary>
public sealed record Transfer(bool Pickup, double Seconds) : Leg;

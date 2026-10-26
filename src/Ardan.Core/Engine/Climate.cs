namespace Ardan.Core.Engine;

/// <summary>Store temperature. It drifts around the set point and rises when a dock door stays open.</summary>
public sealed class Climate
{
    private const double SetPointC = 3.0;
    private double _drift;
    private double _spike;

    public double TemperatureC { get; private set; } = SetPointC;

    internal void Advance(double seconds, Random random)
    {
        _spike *= Math.Exp(-seconds / 150);
        _drift = Math.Clamp(_drift + (random.NextDouble() - 0.5) * 0.03 * seconds, -0.6, 0.6);
        TemperatureC = Math.Round(SetPointC + _drift + _spike, 1);
    }

    internal void DoorOpened(Random random) => _spike += 1.8 + random.NextDouble() * 1.4;
}

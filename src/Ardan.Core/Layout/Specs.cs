namespace Ardan.Core.Layout;

/// <summary>Speeds and handling times, taken from the machine data sheets.</summary>
public static class Motion
{
    public const double TaxiEmptySpeed = 1.8;
    public const double TaxiLoadedSpeed = 1.4;
    public const double LiftSpeed = 1.0;
    public const double ConveyorSpeed = 0.18;
    public const double ConveyorLength = 6.0;
    public const double ConveyorSpacing = 1.4;
    public const double ProfileCheckAt = 0.8;
    public const double TransferSeconds = 3.0;
    public const double BoardingSeconds = 2.0;
}

/// <summary>Limits the control system enforces.</summary>
public static class Limits
{
    public const int MaxPalletHeightMm = 1800;
    public const int MaxPalletWeightKg = 1500;
    public const double LowBattery = 20;
    public const double WorkBattery = 30;
    public const double ChargedBattery = 95;
    public const double StoreMinC = 0;
    public const double StoreMaxC = 5;
    public const int MaxJobsPerOrder = 3;
    public const int MaxPalletsPerOrder = 24;
    public const int Docks = 3;
}

/// <summary>Battery use and charge rates, in percent per second.</summary>
public static class Energy
{
    public const double DrainDriving = 0.035;
    public const double DrainHandling = 0.02;
    public const double DrainIdle = 0.002;
    public const double ChargeRate = 0.15;
}

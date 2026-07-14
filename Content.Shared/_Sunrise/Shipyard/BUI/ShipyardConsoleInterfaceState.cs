using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Shipyard.BUI;

[Serializable, NetSerializable]
public sealed class ShipyardConsoleInterfaceState : BoundUserInterfaceState
{
    public readonly string AccountName;
    public readonly int Balance;
    public readonly string? CurrentShuttleName;
    public readonly int CurrentShuttlePrice;
    public readonly int CurrentShuttleSellValue;
    public readonly float SellRate;
    public readonly List<ShipyardVesselData> Vessels;

    public ShipyardConsoleInterfaceState(
        string accountName,
        int balance,
        string? currentShuttleName,
        int currentShuttlePrice,
        int currentShuttleSellValue,
        float sellRate,
        List<ShipyardVesselData> vessels)
    {
        AccountName = accountName;
        Balance = balance;
        CurrentShuttleName = currentShuttleName;
        CurrentShuttlePrice = currentShuttlePrice;
        CurrentShuttleSellValue = currentShuttleSellValue;
        SellRate = sellRate;
        Vessels = vessels;
    }
}

[Serializable, NetSerializable]
public readonly record struct ShipyardVesselData(string Id, string Name, string Description, int Price);

namespace FieldSim.Core;

public enum AssetStatus
{
    Ready,
    Assigned,
    Operating,
    Turnaround,
    ScheduledMaintenance,
    UnscheduledMaintenance,
    Damaged,
    LongTermRepair
}

public sealed class AssetState
{
    private int _fuelUsePerHour;
    private int _storesUsePerHour;
    private int _turnaroundHours;

    public required string AssetId { get; init; }
    public required string PlatformId { get; init; }
    public AssetStatus Status { get; private set; } = AssetStatus.Ready;
    public int FuelPercent { get; private set; } = 100;
    public int StoresPercent { get; private set; } = 100;
    public int OperatingHours { get; private set; }
    public int HoursUntilStateChange { get; private set; }

    public bool Assign()
    {
        if (Status != AssetStatus.Ready) return false;
        Status = AssetStatus.Assigned;
        return true;
    }

    public bool BeginOperation(int plannedHours, int fuelUsePerHour,
        int storesUsePerHour, int turnaroundHours)
    {
        if (Status != AssetStatus.Assigned || plannedHours <= 0 ||
            fuelUsePerHour < 0 || storesUsePerHour < 0 || turnaroundHours <= 0)
        {
            return false;
        }
        _fuelUsePerHour = fuelUsePerHour;
        _storesUsePerHour = storesUsePerHour;
        _turnaroundHours = turnaroundHours;
        Status = AssetStatus.Operating;
        HoursUntilStateChange = plannedHours;
        return true;
    }

    public void TickHour()
    {
        switch (Status)
        {
            case AssetStatus.Operating:
                OperatingHours++;
                FuelPercent = Math.Max(0, FuelPercent - _fuelUsePerHour);
                StoresPercent = Math.Max(0, StoresPercent - _storesUsePerHour);
                if (--HoursUntilStateChange <= 0)
                {
                    Status = AssetStatus.Turnaround;
                    HoursUntilStateChange = _turnaroundHours;
                }
                break;

            case AssetStatus.Turnaround:
                if (--HoursUntilStateChange <= 0)
                {
                    FuelPercent = 100;
                    StoresPercent = 100;
                    Status = AssetStatus.Ready;
                }
                break;

            case AssetStatus.ScheduledMaintenance:
            case AssetStatus.UnscheduledMaintenance:
            case AssetStatus.Damaged:
            case AssetStatus.LongTermRepair:
                if (HoursUntilStateChange > 0 && --HoursUntilStateChange == 0)
                {
                    Status = AssetStatus.Ready;
                }
                break;
        }
    }

    public bool EnterMaintenance(bool scheduled, int durationHours)
    {
        if (Status is AssetStatus.Operating or AssetStatus.LongTermRepair || durationHours <= 0) return false;
        Status = scheduled ? AssetStatus.ScheduledMaintenance : AssetStatus.UnscheduledMaintenance;
        HoursUntilStateChange = durationHours;
        return true;
    }

    public void RecordDamage(int repairHours, bool longTerm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repairHours);
        Status = longTerm ? AssetStatus.LongTermRepair : AssetStatus.Damaged;
        HoursUntilStateChange = repairHours;
    }
    public bool ReleaseToReady()
    {
        if (Status is not (AssetStatus.Assigned or AssetStatus.Operating or AssetStatus.Turnaround)) return false;
        Status = AssetStatus.Ready;
        HoursUntilStateChange = 0;
        return true;
    }

}

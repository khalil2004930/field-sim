namespace FieldSim.Core;

public enum LightCondition
{
    Day,
    Dusk,
    Night,
    MoonlitNight
}

public enum WeatherVisibility
{
    Clear,
    Haze,
    Rain,
    Fog,
    Dust
}

public sealed class EnvironmentState
{
    public LightCondition Light { get; set; } = LightCondition.Day;
    public WeatherVisibility Visibility { get; set; } = WeatherVisibility.Clear;
    public double AmbientLight01 { get; set; } = 1.0;
    public double ThermalContrast01 { get; set; } = 0.65;
    public double VisibilityMeters { get; set; } = 4000;
    public double WindMetersPerSecond { get; set; } = 2;
    public double TemperatureCelsius { get; set; } = 22;
    public double Precipitation01 { get; set; }

    public static EnvironmentState DayClear() => new();

    public void ApplyPreset(LightCondition light, WeatherVisibility visibility)
    {
        Light = light;
        Visibility = visibility;
        AmbientLight01 = light switch
        {
            LightCondition.Day => 1.0,
            LightCondition.Dusk => 0.38,
            LightCondition.MoonlitNight => 0.18,
            _ => 0.06
        };
        ThermalContrast01 = light switch
        {
            LightCondition.Day => 0.58,
            LightCondition.Dusk => 0.72,
            LightCondition.MoonlitNight => 0.78,
            _ => 0.82
        };
        VisibilityMeters = visibility switch
        {
            WeatherVisibility.Clear => 4000,
            WeatherVisibility.Haze => 2200,
            WeatherVisibility.Rain => 1500,
            WeatherVisibility.Fog => 650,
            WeatherVisibility.Dust => 900,
            _ => 2500
        };
        Precipitation01 = visibility == WeatherVisibility.Rain ? 0.55 : 0.0;
    }
}

public enum InfantryWeaponClass
{
    Handgun,
    SubmachineGun,
    AssaultRifle,
    BattleRifle,
    LightMachineGun,
    GeneralPurposeMachineGun,
    DesignatedMarksmanRifle,
    SniperRifle,
    Shotgun,
    GrenadeLauncher,
    LightAntiArmor,
    HeavyAntiArmor,
    SupportWeapon
}

public enum FireMode
{
    Safe,
    SemiAutomatic,
    Burst,
    Automatic,
    SingleShot
}

public sealed record OpticProfile(
    string Name,
    double Magnification,
    bool NightVision,
    bool Thermal,
    bool LaserRangefinder,
    double FieldOfViewDegrees,
    double AcquisitionQuality01)
{
    public static OpticProfile IronSights => new("Iron sights", 1.0, false, false, false, 70, 0.38);
    public static OpticProfile RedDot => new("Reflex sight", 1.0, false, false, false, 80, 0.52);
    public static OpticProfile Magnified => new("Magnified optic", 4.0, false, false, false, 28, 0.66);
    public static OpticProfile NightOptic => new("Night-capable optic", 3.0, true, false, false, 30, 0.70);
    public static OpticProfile ThermalOptic => new("Thermal optic", 3.0, false, true, false, 26, 0.78);
}

public sealed record InfantryWeaponDefinition(
    string Id,
    string DisplayName,
    InfantryWeaponClass Class,
    string AmmunitionClass,
    int MagazineCapacity,
    double MassKg,
    double PracticalEngagementRangeMeters,
    double MaximumPhysicalRangeMeters,
    double CyclicRateRpm,
    double SustainedRateRpm,
    double BaseReloadSeconds,
    double Precision01,
    double Handling01,
    double Reliability01,
    double Suppression01,
    IReadOnlyList<FireMode> FireModes,
    OpticProfile DefaultOptic)
{
    public static InfantryWeaponDefinition GenericRifle(OpticProfile? optic = null) => new(
        "generic-rifle", "Service rifle", InfantryWeaponClass.AssaultRifle, "intermediate",
        30, 3.6, 450, 2200, 700, 90, 2.7, 0.68, 0.76, 0.985, 0.42,
        new[] { FireMode.SemiAutomatic, FireMode.Automatic }, optic ?? OpticProfile.RedDot);

    public static InfantryWeaponDefinition GenericMachineGun() => new(
        "generic-lmg", "Light machine gun", InfantryWeaponClass.LightMachineGun, "intermediate",
        100, 7.2, 650, 3000, 800, 130, 5.5, 0.61, 0.50, 0.975, 0.78,
        new[] { FireMode.Automatic }, OpticProfile.RedDot);

    public static InfantryWeaponDefinition GenericMarksmanRifle() => new(
        "generic-dmr", "Designated marksman rifle", InfantryWeaponClass.DesignatedMarksmanRifle, "full-power",
        20, 4.8, 750, 3500, 450, 45, 3.2, 0.81, 0.60, 0.985, 0.38,
        new[] { FireMode.SemiAutomatic }, OpticProfile.Magnified);
}

public sealed class WeaponRuntime
{
    public required InfantryWeaponDefinition Definition { get; init; }
    public OpticProfile Optic { get; set; } = OpticProfile.IronSights;
    public FireMode SelectedFireMode { get; set; } = FireMode.SemiAutomatic;
    public int RoundsLoaded { get; set; }
    public int ReserveRounds { get; set; }
    public double CooldownSeconds { get; set; }
    public double ReloadSecondsRemaining { get; set; }
    public double Heat01 { get; set; }
    public bool Malfunctioned { get; set; }
    public double MalfunctionSecondsRemaining { get; set; }

    public int TotalRounds => RoundsLoaded + ReserveRounds;
    public int InitialTotalRounds { get; private set; }

    public void InitializeAmmo(int reserveRounds)
    {
        RoundsLoaded = Definition.MagazineCapacity;
        ReserveRounds = Math.Max(0, reserveRounds);
        InitialTotalRounds = RoundsLoaded + ReserveRounds;
        Optic = Definition.DefaultOptic;
    }
}

public enum SoldierRole
{
    Leader,
    TeamLeader,
    Rifleman,
    AutomaticRifleman,
    MachineGunner,
    AssistantGunner,
    Grenadier,
    Marksman,
    Medic,
    RadioOperator,
    Scout,
    Engineer,
    AntiArmorSpecialist
}

public enum BodyRegion
{
    Head,
    Neck,
    Chest,
    Abdomen,
    Pelvis,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}

public enum WoundType
{
    MinorTrauma,
    Penetrating,
    Fragment,
    BluntTrauma,
    Burn
}

[Flags]
public enum SoldierCondition
{
    None = 0,
    Fatigued = 1 << 0,
    Suppressed = 1 << 1,
    Wounded = 1 << 2,
    Bleeding = 1 << 3,
    InShock = 1 << 4,
    Unconscious = 1 << 5,
    Incapacitated = 1 << 6,
    Dead = 1 << 7
}

public sealed class Wound
{
    public required BodyRegion Region { get; init; }
    public required WoundType Type { get; init; }
    public double Severity01 { get; set; }
    public double BleedingPerMinute01 { get; set; }
    public double Pain01 { get; set; }
    public double MobilityPenalty01 { get; set; }
    public bool Treated { get; set; }
}

public sealed class BodyArmorProfile
{
    public string Name { get; init; } = "Body armor";
    public double TorsoCoverage01 { get; init; } = 0.62;
    public double HeadCoverage01 { get; init; } = 0.45;
    public double Protection01 { get; init; } = 0.58;
    public double MassKg { get; init; } = 8.0;
}

public sealed class SoldierVitals
{
    public double HitPoints { get; set; } = 100;
    public double BloodVolume01 { get; set; } = 1.0;
    public double Pain01 { get; set; }
    public double Shock01 { get; set; }
    public double Consciousness01 { get; set; } = 1.0;
    public double Fatigue01 { get; set; }
    public double Hydration01 { get; set; } = 1.0;
    public double Nutrition01 { get; set; } = 1.0;
    public double Suppression01 { get; set; }
    public double Morale01 { get; set; } = 0.75;
    public SoldierCondition Condition { get; set; } = SoldierCondition.None;
    public List<Wound> Wounds { get; } = [];
}

public sealed class SoldierEquipment
{
    public BodyArmorProfile Armor { get; set; } = new();
    public string Camouflage { get; set; } = "Terrain-appropriate uniform";
    public bool Helmet { get; set; } = true;
    public bool Radio { get; set; }
    public bool Medkit { get; set; }
    public int Grenades { get; set; } = 2;
    public int SmokeGrenades { get; set; } = 1;
    public double WaterLiters { get; set; } = 2.0;
    public double FoodKg { get; set; } = 0.7;
    public double OtherLoadKg { get; set; } = 4.0;
}

public sealed class SoldierSkills
{
    public double Marksmanship01 { get; set; } = 0.58;
    public double Observation01 { get; set; } = 0.58;
    public double Medical01 { get; set; } = 0.20;
    public double Fitness01 { get; set; } = 0.60;
    public double Discipline01 { get; set; } = 0.62;
}

public enum CasualtyDisposition
{
    None,
    WoundedMobile,
    NeedsTreatment,
    Stabilized,
    AwaitingEvacuation,
    EvacuationAssigned,
    Evacuated,
    Killed
}

public sealed class SoldierRuntime
{
    public SoldierRole Role { get; set; } = SoldierRole.Rifleman;
    public SoldierVitals Vitals { get; } = new();
    public SoldierEquipment Equipment { get; } = new();
    public SoldierSkills Skills { get; } = new();
    public required WeaponRuntime PrimaryWeapon { get; init; }
    public double BaseBodyMassKg { get; set; } = 78;
    public double TimeSinceLastHitSeconds { get; set; } = double.MaxValue;
    public double TimeSinceTreatmentSeconds { get; set; } = double.MaxValue;
    public CasualtyDisposition CasualtyDisposition { get; set; } = CasualtyDisposition.None;
    public long? CasualtySinceTick { get; set; }
    public string? EvacuationRequestId { get; set; }
    public bool IsEvacuated { get; set; }
    public double SupplyReadiness01 { get; set; } = 1.0;
    public bool NeedsResupply { get; set; }

    public bool IsAlive => !Vitals.Condition.HasFlag(SoldierCondition.Dead);
    public bool IsCombatEffective => IsAlive && !IsEvacuated &&
        !Vitals.Condition.HasFlag(SoldierCondition.Unconscious) &&
        !Vitals.Condition.HasFlag(SoldierCondition.Incapacitated);

    public double CarriedMassKg =>
        PrimaryWeapon.Definition.MassKg + Equipment.Armor.MassKg + Equipment.WaterLiters +
        Equipment.FoodKg + Equipment.OtherLoadKg + PrimaryWeapon.TotalRounds * 0.012;
}

public enum CombatEventType
{
    Fire,
    Hit,
    Reload,
    Medical,
    Casualty,
    Disabled,
    System
}

public sealed record CombatEvent(
    long Tick,
    int? SourceUnitId,
    int? TargetUnitId,
    string Message,
    CombatEventType Type = CombatEventType.System);

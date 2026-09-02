namespace FieldSim.Core;

public enum CohesionBand
{
    Cohesive,
    Stretched,
    Fragmented,
    Broken
}

/// <summary>
/// Normalized local cohesion/morale model. It reacts to nearby friendly presence, leadership,
/// suppression, fatigue, wounds and nearby casualties. Values are game abstractions.
/// </summary>
public static class CohesionMoraleEngine
{
    public static void Update(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.RebuildSpatialIndex();

        foreach (var unit in state.Units.Where(item => item.Alive && item.Soldier is { IsEvacuated: false }))
        {
            var position = state.GroundPositionOf(unit);
            var nearbyIds = state.SpatialIndex.QueryRadius(position, 90);
            state.Performance.RecordSpatialQuery(nearbyIds.Count);
            var friendlies = nearbyIds
                .Select(state.UnitById)
                .Where(item => item is { Alive: true } && item.Id != unit.Id && item.Faction == unit.Faction &&
                               item.Soldier is { IsCombatEffective: true })
                .Select(item => item!)
                .ToArray();

            var closeCount = friendlies.Count(item =>
                position.HorizontalDistanceTo(state.GroundPositionOf(item)) <= 40);
            var nearest = friendlies.Select(item => position.HorizontalDistanceTo(state.GroundPositionOf(item)))
                .DefaultIfEmpty(120)
                .Min();
            var leaderNearby = friendlies.Any(item =>
                item.Soldier?.Role is SoldierRole.Leader or SoldierRole.TeamLeader &&
                position.HorizontalDistanceTo(state.GroundPositionOf(item)) <= 65);
            var nearbyCasualty = nearbyIds
                .Select(state.UnitById)
                .Any(item => item is not null && item.Id != unit.Id && item.Faction == unit.Faction &&
                             (!item.Alive || item.Soldier?.Vitals.Condition.HasFlag(SoldierCondition.Incapacitated) == true) &&
                             position.HorizontalDistanceTo(state.GroundPositionOf(item)) <= 35);

            var soldier = unit.Soldier!;
            var cohesion = 0.24 + Math.Min(4, closeCount) * 0.13;
            if (leaderNearby) cohesion += 0.16;
            cohesion += Math.Clamp((70 - nearest) / 140.0, -0.12, 0.20);
            cohesion -= soldier.Vitals.Suppression01 * 0.34;
            cohesion -= soldier.Vitals.Fatigue01 * 0.15;
            cohesion -= Math.Min(0.20, soldier.Vitals.Wounds.Sum(wound => wound.Severity01) * 0.08);
            if (nearbyCasualty) cohesion -= 0.10;
            cohesion = Math.Clamp(cohesion, 0, 1);

            unit.LocalCohesion01 = unit.LocalCohesion01 * 0.72 + cohesion * 0.28;
            var newBand = unit.LocalCohesion01 switch
            {
                >= 0.68 => CohesionBand.Cohesive,
                >= 0.46 => CohesionBand.Stretched,
                >= 0.25 => CohesionBand.Fragmented,
                _ => CohesionBand.Broken
            };

            if (newBand != unit.CohesionBand && state.Tick - unit.LastCohesionChangeTick >= 8)
            {
                unit.CohesionBand = newBand;
                unit.LastCohesionChangeTick = state.Tick;
                state.AddActivity(TacticalEventType.Movement,
                    $"COHESION: {unit.DisplayName} is now {newBand.ToString().ToLowerInvariant()} ({unit.LocalCohesion01:P0}).",
                    unit.Faction, unit.Id);
            }
            else
            {
                unit.CohesionBand = newBand;
            }

            var moraleTarget = 0.40 + unit.LocalCohesion01 * 0.42 + soldier.Skills.Discipline01 * 0.16;
            moraleTarget -= soldier.Vitals.Suppression01 * 0.24;
            moraleTarget -= soldier.Vitals.Shock01 * 0.18;
            if (nearbyCasualty) moraleTarget -= 0.10;
            moraleTarget = Math.Clamp(moraleTarget, 0.08, 0.95);
            var response = moraleTarget < soldier.Vitals.Morale01 ? 0.08 : 0.025;
            soldier.Vitals.Morale01 = Math.Clamp(
                soldier.Vitals.Morale01 + (moraleTarget - soldier.Vitals.Morale01) * response,
                0, 1);
        }
    }
}

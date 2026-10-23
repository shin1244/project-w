public partial class UnitManager
{
    // Only the healers draw a beam; the server sends their patient while someone sees them.
    public void HandleHealing(HealingSnapshot snapshot)
    {
        if (!_units.TryGetValue(snapshot.UnitId, out Unit unit) || unit.UnitType is not (UnitCatalog.Medic or UnitCatalog.Refueler) ||
            !CanInspect(unit)) return;
        unit.ApplyHealing(snapshot.TargetId);
    }
}

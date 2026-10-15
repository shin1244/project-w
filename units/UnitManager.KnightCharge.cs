public partial class UnitManager
{
    public void HandleKnightCharge(KnightChargeSnapshot snapshot, uint tick)
    {
        if (!_units.TryGetValue(snapshot.UnitId, out Unit unit) || unit.UnitType != UnitCatalog.Knight || !CanInspect(unit)) return;
        unit.EffectTick = tick;
        unit.ApplyKnightCharge(snapshot);
        if (_selectedUnitIds.Contains(unit.UnitId)) CommandStateChanged?.Invoke();
    }
}

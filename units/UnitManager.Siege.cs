using System;
using System.Linq;

public partial class UnitManager
{
    public event Action CommandStateChanged;

    public void HandleSiege(SiegeSnapshot snapshot)
    {
        if (!_units.TryGetValue(snapshot.UnitId, out Unit unit) || unit.UnitType != UnitCatalog.SteamArtillery || !CanInspect(unit)) return;
        unit.ApplySiege(snapshot);
        if (_selectedUnitIds.Contains(unit.UnitId)) CommandStateChanged?.Invoke();
    }

    private Unit[] SelectedArtillery() => !CommanderControl || Spectating ? Array.Empty<Unit>() :
        _selectedUnitIds.Where(_units.ContainsKey).Select(id => _units[id])
            .Where(unit => unit.UnitType == UnitCatalog.SteamArtillery && CanControl(unit) && unit.Team == _localTeam)
            .OrderBy(unit => unit.UnitId).ToArray();

    public bool HasSelectedArtillery => SelectedArtillery().Length > 0;

    public bool TryGetSiegeToggle(out bool deploy, out string reason)
    {
        var artillery = SelectedArtillery();
        deploy = true;
        reason = null;
        if (artillery.Length == 0) { reason = "내 증기포격차를 선택하세요"; return false; }
        if (artillery.Any(unit => unit.Siege is { Mode: SiegeMode.Mobile })) return true;
        if (artillery.Any(unit => unit.Siege is { Mode: SiegeMode.Deployed })) { deploy = false; return true; }
        reason = artillery.Any(unit => unit.Siege == null) ? "공성 상태 수신 대기" : "전환 중";
        return false;
    }

    public void RequestSiegeToggle()
    {
        if (TryGetSiegeToggle(out bool deploy, out _)) RequestSiege(deploy);
    }

    public bool RequestSiege(bool deploy)
    {
        SiegeMode expected = deploy ? SiegeMode.Mobile : SiegeMode.Deployed;
        uint[] ids = SelectedArtillery().Where(unit => unit.Siege is { } state && state.Mode == expected)
            .Select(unit => unit.UnitId).ToArray();
        if (ids.Length == 0) return false;
        // 전환·완료 상태는 서버 응답을 받을 때만 바뀝니다.
        CommandRequested?.Invoke(Protocol.BuildSiege(deploy, ids));
        return true;
    }
}

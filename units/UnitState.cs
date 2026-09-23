public enum UnitActivity { Idle, Gather, Attack }

// 서버가 확정한 행동과 운반량. 애니메이션은 이 정보를 표시하기만 합니다.
public readonly record struct UnitState(
    UnitActivity Activity, int Carrying, uint FocusId, uint SwingSequence);

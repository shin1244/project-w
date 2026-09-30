using System;
using System.Collections.Generic;

public static class Protocol
{
    public static string BuildConstruct(uint buildingId, uint workerId)
        => FormattableString.Invariant($"CONSTRUCT {buildingId} {workerId}");

    public static string BuildConstruction(uint buildingType, float x, float z, uint workerId)
        => FormattableString.Invariant($"BUILD {buildingType} {x} {z} {workerId}");

    // TRAIN 유닛타입 (일꾼: 0, 사용자는 서버 접속 정보로 판별합니다.)
    public static string BuildTrain(uint unitType)
    {
        return FormattableString.Invariant($"TRAIN {unitType}");
    }

    // ATTACK 대상ID 내유닛ID... (줄바꿈은 NetClient.Send에서 붙입니다.)
    public static string BuildAttack(uint targetId, IEnumerable<uint> unitIds)
    {
        return FormattableString.Invariant($"ATTACK {targetId} {string.Join(" ", unitIds)}");
    }

    public static string BuildMove(IEnumerable<uint> unitIds, float x, float z)
    {
        string ids = string.Join(" ", unitIds);

        return FormattableString.Invariant($"MOVE {x} {z} {ids}");
    }

    public static string BuildGather(uint targetId, IEnumerable<uint> unitIds)
    {
        return FormattableString.Invariant($"GATHER {targetId} {string.Join(" ", unitIds)}");
    }

    public static string BuildAttackMove(IEnumerable<uint> unitIds, float x, float z)
    {
        return FormattableString.Invariant($"ATTACK_MOVE {x} {z} {string.Join(" ", unitIds)}");
    }
}

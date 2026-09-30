using System;
using System.Collections.Generic;

public static class Protocol
{
    public static string BuildRallyMove(uint buildingId, float x, float z)
        => FormattableString.Invariant($"RALLY {buildingId} MOVE {x} {z}");
    public static string BuildRallyGather(uint buildingId, uint resourceId)
        => FormattableString.Invariant($"RALLY {buildingId} GATHER {resourceId}");
    public static string BuildRallyClear(uint buildingId)
        => FormattableString.Invariant($"RALLY {buildingId} CLEAR");
    public static string BuildCancelTrain(uint buildingId, uint jobId)
        => FormattableString.Invariant($"CANCEL_TRAIN {buildingId} {jobId}");

    public static string BuildCancelConstruction(uint buildingId)
        => FormattableString.Invariant($"CANCEL_BUILD {buildingId}");

    public static string BuildConstruct(uint buildingId, uint workerId)
        => FormattableString.Invariant($"CONSTRUCT {buildingId} {workerId}");

    public static string BuildConstruction(uint buildingType, float x, float z, uint workerId)
        => FormattableString.Invariant($"BUILD {buildingType} {x} {z} {workerId}");

    // TRAIN 건물ID 유닛타입. 요청자의 ID는 서버 접속 정보로 판별합니다.
    public static string BuildTrain(uint buildingId, uint unitType)
    {
        return FormattableString.Invariant($"TRAIN {buildingId} {unitType}");
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

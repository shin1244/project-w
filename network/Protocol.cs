using System;
using System.Collections.Generic;

public static class Protocol
{
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
}

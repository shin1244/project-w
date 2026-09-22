using System;
using System.Collections.Generic;

public static class Protocol
{
    public static string BuildMove(IEnumerable<uint> unitIds, float x, float z)
    {
        string ids = string.Join(" ", unitIds);

        return FormattableString.Invariant($"MOVE {x} {z} {ids}");
    }
}
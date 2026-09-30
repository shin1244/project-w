using System.Globalization;
using System.Collections.Generic;

public readonly record struct ProductionJob(uint Id, uint UnitType, uint OwnerId);

// QUEUE buildingID percent jobID:unitType:ownerID ...; empty queues have no jobs.
public readonly record struct ProductionSnapshot(uint BuildingId, int Percent, uint[] UnitTypes, ProductionJob[] Jobs = null)
{
    public const int MaxQueue = 5;

    public static bool TryParse(string[] parts, out ProductionSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length < 3 || parts.Length > 3 + MaxQueue || parts[0] != "QUEUE" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int percent) || percent > 100 ||
            (parts.Length == 3 && percent != 0)) return false;
        var types = new uint[parts.Length - 3];
        var jobs = new ProductionJob[types.Length];
        var ids = new HashSet<uint>();
        bool detailed = types.Length > 0 && parts[3].Contains(':');
        for (int i = 0; i < types.Length; i++)
        {
            string token = parts[i + 3];
            if (detailed)
            {
                string[] fields = token.Split(':');
                if (fields.Length != 3 || !uint.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint jobId) ||
                    jobId == 0 || !ids.Add(jobId) || !uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out types[i]) || types[i] > 2 ||
                    !uint.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint owner) || owner == 0) return false;
                jobs[i] = new(jobId, types[i], owner);
            }
            else
            {
                // 이전 스냅샷/오프라인 미리보기는 표시만 허용하며 예약 ID 없이 취소하지 않는다.
                if (!uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out types[i]) || types[i] > 2) return false;
                jobs[i] = new(0, types[i], 0);
            }
        }
        snapshot = new ProductionSnapshot(id, percent, types, jobs);
        return true;
    }
}

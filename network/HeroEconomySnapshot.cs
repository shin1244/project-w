using Godot;
using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;

public sealed record HeroItemDefinition(uint Id, string Key, string Name, string Description, int Cost, uint From, int Tier = 0,
    HeroItemEffect[] Effects = null)
{
    // The server names items in Korean. locale/en.po holds English by the item's key (context
    // "item"); a key it does not know, or Korean, shows the server's name.
    public string DisplayName
    {
        get
        {
            string text = Lang.T(Key, "item");
            return text != Key ? text : Name;
        }
    }

    // Worded here from the server's numbers. An older server sends no effects and a newer one may
    // send a kind this client cannot word; both show the server's Korean description.
    public string EffectText
    {
        get
        {
            if (Effects is not { Length: > 0 }) return Description;
            var parts = new string[Effects.Length];
            for (int i = 0; i < Effects.Length; i++)
                if ((parts[i] = Effects[i].Text) == null) return Description;
            return string.Join(" · ", parts);
        }
    }

    public static bool TryParseCatalog(string message, out HeroItemDefinition[] items)
    {
        items = null;
        if (!message.StartsWith("ITEM_CATALOG ", StringComparison.Ordinal)) return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<HeroItemDefinition[]>(message[13..],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed == null || parsed.Length is 0 or > 128 ||
                parsed.Any(i => i == null || i.Id == 0 || i.Cost < 0 || string.IsNullOrWhiteSpace(i.Name) ||
                    string.IsNullOrWhiteSpace(i.Key) || i.Description == null ||
                    i.Effects != null && i.Effects.Any(e => e == null || string.IsNullOrWhiteSpace(e.Stat) || !float.IsFinite(e.Value))) ||
                parsed.Select(i => i.Id).Distinct().Count() != parsed.Length) return false;
            var byId = parsed.ToDictionary(i => i.Id);
            for (int i = 0; i < parsed.Length; i++)
            {
                var item = parsed[i];
                int depth = 1;
                var ancestor = item;
                long value = item.Cost;
                while (ancestor.From != 0)
                {
                    if (++depth > 3 || !byId.TryGetValue(ancestor.From, out ancestor)) return false;
                    value += ancestor.Cost;
                }
                if (value > int.MaxValue || item.Tier != 0 && item.Tier != depth) return false;
                parsed[i] = item with { Tier = depth };
            }
            items = parsed;
            return true;
        }
        catch (JsonException) { return false; }
    }
}

// One effect of an item as ITEM_CATALOG sends it (project-w-server hero_items.go, ItemEffect).
public sealed record HeroItemEffect(string Stat, float Value)
{
    public string Text => Stat switch
    {
        "attack" => Lang.F("공격력 +{0:0.##}", Value),
        "maxHp" => Lang.F("최대 체력 +{0:0.##}", Value),
        "moveSpeed" => Lang.F("이동 속도 +{0:0.##}", Value),
        "attackSpeed" => Lang.F("공격 속도 +{0:0.##}%", Value),
        "damageReduction" => Lang.F("받는 피해 {0:0.##}% 감소 (고정 피해 제외)", Value),
        _ => null
    };
}

public sealed record HeroInventorySnapshot(int Gold, bool CanShop, bool HasForge, uint[] Items, bool[] Pending, int TeamTier = 1)
{
    public static bool TryParse(string[] p, out HeroInventorySnapshot snapshot)
    {
        snapshot = null;
        if (p.Length is not (12 or 13) || p[0] != "HERO_ITEMS" ||
            !int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int gold) || gold < 0 ||
            p[2] is not ("0" or "1") || p[3] is not ("0" or "1")) return false;
        int tier = 1;
        if (p.Length == 13 && (!int.TryParse(p[12], NumberStyles.None, CultureInfo.InvariantCulture, out tier) || tier is < 1 or > 3)) return false;
        var items = new uint[4]; var pending = new bool[4];
        for (int i = 0; i < 4; i++)
        {
            if (!WireNumber.UInt(p[4+i*2], out items[i]) || p[5+i*2] is not ("0" or "1")) return false;
            pending[i] = p[5+i*2] == "1";
            if (items[i] == 0 && pending[i]) return false;
        }
        snapshot = new(gold, p[2] == "1", p[3] == "1", items, pending, tier);
        return true;
    }
}

public readonly record struct ItemCrateSnapshot(uint Id, uint PlayerId, uint ItemId, uint CarrierId, Vector3 Position)
{
    public static bool TryParse(string[] p, out ItemCrateSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 7 || p[0] != "ITEM_CRATE" ||
            !WireNumber.UInt(p[1], out uint id) || id == 0 || !WireNumber.UInt(p[2], out uint player) || player == 0 ||
            !WireNumber.UInt(p[3], out uint item) || item == 0 || !WireNumber.UInt(p[4], out uint carrier) ||
            !float.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) ||
            !float.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !float.IsFinite(z)) return false;
        snapshot = new(id, player, item, carrier, new Vector3(x, 0, z));
        return true;
    }
}

public readonly record struct RecallSnapshot(uint HeroId, uint Start, uint End)
{
    public bool Active => End != 0;
    public double Remaining(uint tick) => Math.Max(0, unchecked((int)(End-tick))) / 30.0;
    public static bool TryParse(string[] p, out RecallSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 4 || p[0] != "RECALL" || !WireNumber.UInt(p[1], out uint id) || id == 0 ||
            !WireNumber.UInt(p[2], out uint start) || !WireNumber.UInt(p[3], out uint end) ||
            (end == 0 ? start != 0 : unchecked((int)(end-start)) <= 0)) return false;
        snapshot = new(id, start, end);
        return true;
    }
}

public readonly record struct MarshalEffectSnapshot(uint UnitId, uint MarchUntil, uint RallyUntil)
{
    public static bool TryParse(string[] p, out MarshalEffectSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 4 || p[0] != "MARSHAL" || !WireNumber.UInt(p[1], out uint id) || id == 0 ||
            !WireNumber.UInt(p[2], out uint march) || !WireNumber.UInt(p[3], out uint rally)) return false;
        snapshot = new(id, march, rally);
        return true;
    }
    public bool Marching(uint tick) => MarchUntil != 0 && unchecked((int)(MarchUntil-tick)) > 0;
    public bool Rallying(uint tick) => RallyUntil != 0 && unchecked((int)(RallyUntil-tick)) > 0;
}

internal static class WireNumber
{
    internal static bool UInt(string text, out uint value)
        => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}

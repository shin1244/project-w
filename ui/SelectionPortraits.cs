using Godot;
using System.Collections.Generic;

// 미리 저장한 이미지 리소스만 읽습니다. 게임 실행 중 모델/카메라/SubViewport를 만들지 않습니다.
public partial class SelectionPortraits : Node
{
    private readonly Dictionary<string, Texture2D> _cache = new();

    public Texture2D GetPortrait(PackedScene scene, bool enemy = false)
    {
        if (scene == null) return null;
        string name = scene.ResourcePath switch
        {
            "res://units/Worker.tscn" => enemy ? "worker-enemy" : "worker",
            "res://units/Knight.tscn" => enemy ? "knight-enemy" : "knight",
            "res://units/HeroTest.tscn" => "mutant-wolf",
            "res://units/HeroGolem.tscn" => "rune-golem",
            "res://units/HeroMarshal.tscn" => "marshal",
            "res://units/HeroRanger.tscn" => enemy ? "archer-enemy" : "archer", // Same model as the archer, scaled up.
            "res://units/Archer.tscn" => enemy ? "archer-enemy" : "archer",
            "res://units/Medic.tscn" => enemy ? "medic-enemy" : "medic",
            "res://units/Refueler.tscn" => enemy ? "refueler-enemy" : "refueler",
            "res://units/FlameAutomaton.tscn" => enemy ? "flame-automaton-enemy" : "flame-automaton",
            "res://units/SteamArtillery.tscn" => enemy ? "steam-artillery-enemy" : "steam-artillery",
            "res://units/MinionKnight.tscn" => enemy ? "minion-knight-enemy" : "minion-knight",
            "res://units/MinionArcher.tscn" => enemy ? "minion-archer-enemy" : "minion-archer",
            "res://units/MinionHealer.tscn" => enemy ? "minion-healer-enemy" : "minion-healer",
            "res://units/MinionFurnace.tscn" => enemy ? "minion-furnace-enemy" : "minion-furnace",
            "res://units/MinionWarden.tscn" => enemy ? "minion-warden-enemy" : "minion-warden",
            "res://units/MinionArc.tscn" => enemy ? "minion-arc-enemy" : "minion-arc",
            "res://units/SiegeRam.tscn" => enemy ? "siege-ram-enemy" : "siege-ram",
            "res://buildings/TownHall.tscn" => enemy ? "townhall-enemy" : "townhall-ally",
            "res://buildings/Fortress.tscn" => enemy ? "fortress-enemy" : "fortress-ally",
            "res://buildings/Tower.tscn" => enemy ? "tower-enemy" : "tower-ally",
            "res://buildings/Store.tscn" => enemy ? "store-enemy" : "store-ally",
            "res://buildings/Supply.tscn" => enemy ? "supply-enemy" : "supply-ally",
            "res://buildings/Barracks.tscn" => enemy ? "barracks-enemy" : "barracks-ally",
            "res://buildings/Forge.tscn" => enemy ? "forge-enemy" : "forge-ally",
            "res://buildings/OilDerrick.tscn" => enemy ? "oil_derrick-enemy" : "oil_derrick-ally",
            "res://buildings/Workshop.tscn" => enemy ? "workshop-enemy" : "workshop-ally",
            "res://buildings/ResearchLab.tscn" => enemy ? "research_lab-enemy" : "research_lab-ally",
            "res://buildings/CoalDrill.tscn" => enemy ? "coal_drill-enemy" : "coal_drill-ally",
            "res://buildings/Tunnel.tscn" => enemy ? "tunnel-enemy" : "tunnel-ally",
            _ => null
        };
        if (name == null) return null;
        if (!_cache.TryGetValue(name, out Texture2D texture))
        {
            string path = name is "minion-healer" or "minion-healer-enemy" or "siege-ram" or "siege-ram-enemy" or "marshal" or "marshal-enemy" or
                "medic" or "medic-enemy" or "refueler" or "refueler-enemy"
                ? $"res://ui/icons/{name}.svg" : $"res://ui/portraits/{name}.png";
            texture = GD.Load<Texture2D>(path);
            _cache.Add(name, texture);
        }
        return texture;
    }

    public override void _ExitTree() => _cache.Clear();
}

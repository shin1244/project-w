using Godot;
using System.Collections.Generic;

// 미리 저장한 PNG만 읽습니다. 게임 실행 중 모델/카메라/SubViewport를 만들지 않습니다.
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
            "res://units/Archer.tscn" => enemy ? "archer-enemy" : "archer",
            "res://units/MinionKnight.tscn" => enemy ? "minion-knight-enemy" : "minion-knight",
            "res://units/MinionArcher.tscn" => enemy ? "minion-archer-enemy" : "minion-archer",
            "res://buildings/TownHall.tscn" => enemy ? "townhall-enemy" : "townhall-ally",
            "res://buildings/Fortress.tscn" => enemy ? "fortress-enemy" : "fortress-ally",
            "res://buildings/Tower.tscn" => enemy ? "tower-enemy" : "tower-ally",
            "res://buildings/Store.tscn" => enemy ? "store-enemy" : "store-ally",
            "res://buildings/Supply.tscn" => enemy ? "supply-enemy" : "supply-ally",
            "res://buildings/Barracks.tscn" => enemy ? "barracks-enemy" : "barracks-ally",
            "res://buildings/Forge.tscn" => enemy ? "forge-enemy" : "forge-ally",
            _ => null
        };
        if (name == null) return null;
        if (!_cache.TryGetValue(name, out Texture2D texture))
        {
            texture = GD.Load<Texture2D>($"res://ui/portraits/{name}.png");
            _cache.Add(name, texture);
        }
        return texture;
    }

    public override void _ExitTree() => _cache.Clear();
}

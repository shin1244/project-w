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
            "res://units/Worker.tscn" => "worker",
            "res://units/Knight.tscn" => "knight",
            "res://units/Archer.tscn" => "archer",
            "res://buildings/TownHall.tscn" => enemy ? "townhall-enemy" : "townhall-ally",
            "res://buildings/Tower.tscn" => enemy ? "tower-enemy" : "tower-ally",
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

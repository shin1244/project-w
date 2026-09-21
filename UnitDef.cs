using Godot;

[GlobalClass]
public partial class UnitDef : Resource
{
    [Export] public string Id = "";
    [Export] public PackedScene Model;   // 이 유닛의 겉모습
    [Export] public float Scale = 1f;
    [Export] public bool ShowRangeRing;
}
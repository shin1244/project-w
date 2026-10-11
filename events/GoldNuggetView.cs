using Godot;
using System;

// 정글몹이 떨어뜨린 금덩어리. 위치·수집·제거는 모두 서버 메시지로만 바뀐다.
public partial class GoldNuggetView : Node3D
{
    public GoldNuggetSnapshot Snapshot { get; private set; }
    public bool Available { get; private set; }
    private uint _tick, _localTeam;
    private Area3D _pick;
    private Label3D _label;
    private Node3D _pile;
    private MeshInstance3D _progress;
    private StandardMaterial3D _progressMaterial;
    private int _arcSteps = -1;
    private double _time;
    private static readonly Color Gold = new("f2c14e");

    public override void _Ready()
    {
        var gold = new StandardMaterial3D { AlbedoColor = Gold, Metallic = .75f, Roughness = .32f,
            EmissionEnabled = true, Emission = new Color("5a3d08"), EmissionEnergyMultiplier = .6f };
        _pile = new Node3D { Name = "Pile" };
        AddChild(_pile);
        // 몇 개의 기울어진 덩어리로 만든 낮은 금 더미. 그림자는 바닥 표시를 가리지 않게 끈다.
        (Vector3 position, Vector3 size, float yaw)[] lumps =
        {
            (new Vector3(0, .22f, 0), new Vector3(.62f, .4f, .48f), .3f),
            (new Vector3(.38f, .14f, .16f), new Vector3(.38f, .26f, .34f), -.5f),
            (new Vector3(-.33f, .13f, .2f), new Vector3(.34f, .24f, .3f), .9f),
            (new Vector3(.05f, .12f, -.36f), new Vector3(.32f, .22f, .28f), 1.4f)
        };
        foreach (var (position, size, yaw) in lumps)
            _pile.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = gold,
                Position = position, Rotation = new Vector3(.18f, yaw, -.12f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _progressMaterial = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Gold, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _progress = new MeshInstance3D { Name = "ChannelArc", MaterialOverride = _progressMaterial,
            Position = new Vector3(0, .05f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(_progress);
        _label = new Label3D { FontSize = 26, PixelSize = .014f, OutlineSize = 6, Shaded = false, NoDepthTest = true,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = new Color("f8dca0"), Position = new Vector3(0, 1.5f, 0),
            OutlineModulate = new Color("151821") };
        AddChild(_label);
        _pick = new Area3D { Name = "Pick", CollisionMask = 0, Monitoring = false, Monitorable = false, InputRayPickable = false };
        _pick.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = .9f, Height = 1.2f }, Position = new Vector3(0, .6f, 0) });
        AddChild(_pick);
        Refresh();
    }

    public void Apply(GoldNuggetSnapshot snapshot, uint tick, uint localTeam)
    {
        Snapshot = snapshot; _tick = tick; _localTeam = localTeam; Available = true;
        Position = snapshot.Position;
        Refresh();
    }

    public void SetTick(uint tick)
    {
        _tick = tick;
        if (Snapshot.Channeling) Refresh();
    }

    public void Deactivate() { Available = false; Hide(); Refresh(); }

    public override void _Process(double delta)
    {
        if (!Visible || _pile == null) return;
        _time += delta;
        _pile.Rotation = new Vector3(0, (float)_time * .6f, 0);
    }

    private void Refresh()
    {
        if (_pick == null) return;
        _pick.CollisionLayer = Available ? TributeEventView.PickLayer : 0;
        bool channeling = Available && Snapshot.Channeling;
        Color color = channeling ? TeamColor(Snapshot.CapturerTeam) : Gold;
        _label.Modulate = color;
        // 큰 금덩어리(무료권 포함)는 더 크게 그린다.
        _pile.Scale = Vector3.One * (Snapshot.Credit ? 1.7f : 1.1f);
        _label.Position = new Vector3(0, Snapshot.Credit ? 1.9f : 1.5f, 0);
        _label.Text = channeling ? $"{Snapshot.CapturerTeam}팀 수집 · {Snapshot.RemainingSeconds(_tick):0.0}초"
            : Snapshot.Credit
                ? $"큰 금덩어리 · {Snapshot.Gold}골드 · 경험치 {Snapshot.Experience}\n+ 미니언 무료권 · 우클릭 3초 수집"
                : $"작은 금덩어리 · {Snapshot.Gold}골드 · 경험치 {Snapshot.Experience}\n우클릭 3초 수집";
        _progressMaterial.AlbedoColor = color;
        _progress.Visible = channeling;
        int steps = channeling ? Math.Clamp((int)Math.Ceiling(Snapshot.Progress(_tick) * 48), 0, 48) : 0;
        if (steps != _arcSteps)
        {
            _arcSteps = steps;
            _progress.Mesh = RingMesh(.95f, 1.08f, steps, 48);
        }
    }

    private Color TeamColor(uint team) => _localTeam is 1 or 2
        ? team == _localTeam ? Minimap.AllyColor : Minimap.EnemyColor
        : team == 1 ? new Color("80bce7") : new Color("efae77");

    private static ArrayMesh RingMesh(float inner, float outer, int segments, int full)
    {
        var mesh = new ArrayMesh();
        if (segments <= 0) return mesh;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float a = -Mathf.Pi / 2 + Mathf.Tau * i / full, b = -Mathf.Pi / 2 + Mathf.Tau * (i + 1) / full;
            Vector3 p = new(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new(Mathf.Cos(b), 0, Mathf.Sin(b));
            foreach (Vector3 v in new[] { p * inner, q * outer, q * inner, p * inner, p * outer, q * outer })
            {
                surface.SetNormal(Vector3.Up);
                surface.AddVertex(v);
            }
        }
        surface.Commit(mesh);
        return mesh;
    }
}

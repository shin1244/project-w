using Godot;

// 모든 건물의 공사 부지는 같은 자원으로 표시하고 점유 크기만 바꿉니다.
public partial class ConstructionSite : Node3D
{
    public const float SelectionHeight = 1.48f;
    public const float HealthHeight = 1.68f;
    public Vector2I Footprint { get; private set; }
    public int Percent { get; private set; }
    private Node3D _sign;
    private static BoxMesh _box;
    private static StandardMaterial3D _wood, _border, _dark, _soil;

    public static ConstructionSite Create(Vector2I footprint)
    {
        EnsureResources();
        var site = new ConstructionSite { Name = "ConstructionSite", Footprint = footprint };
        site.Build();
        return site;
    }

    private static void EnsureResources()
    {
        if (_box != null) return;
        _box = new BoxMesh { Size = Vector3.One };
        _wood = Material("775035");
        _border = Material("edb959");
        _dark = Material("302921");
        _soil = Material("655443");
    }

    private static StandardMaterial3D Material(string color) => new()
    {
        AlbedoColor = new Color(color), Roughness = 1
    };

    private void Build()
    {
        float width = Footprint.X, depth = Footprint.Y;
        const float edge = .075f;
        AddBox(this, "PreparedGround", new(0, .015f, 0), new(width - .12f, .025f, depth - .12f), _soil);
        foreach (int side in new[] { -1, 1 })
        {
            AddBox(this, $"BorderX{side}", new(0, .105f, side * (depth - edge) * .5f), new(width, .15f, edge), _border);
            AddBox(this, $"BorderZ{side}", new(side * (width - edge) * .5f, .105f, 0), new(edge, .15f, depth), _border);
            foreach (int other in new[] { -1, 1 })
            {
                Vector3 corner = new(side * (width - .13f) * .5f, .18f, other * (depth - .13f) * .5f);
                AddBox(this, $"Stake{side}_{other}", corner, new(.13f, .36f, .13f), _wood);
                AddBox(this, $"Cap{side}_{other}", corner + Vector3.Up * .14f, new(.135f, .08f, .135f), _border);
            }
        }

        _sign = new Node3D { Name = "Sign" };
        AddChild(_sign);
        foreach (int side in new[] { -1, 1 })
            AddBox(_sign, $"Post{side}", new(side * .46f, .51f, 0), new(.08f, 1.02f, .08f), _wood);
        AddBox(_sign, "Board", new(0, 1.12f, 0), new(1.34f, .60f, .09f), _border);
        AddBox(_sign, "Inset", new(0, 1.12f, .048f), new(1.22f, .45f, .016f), _dark);
        for (int i = 0; i < 5; i++)
            AddBox(_sign, $"Mark{i}", new(-.52f + i * .26f, 1.385f, .052f), new(.13f, .055f, .018f), _dark);
        // 글자 없이도 공사 부지를 알아볼 수 있는 작은 교차 망치 표시입니다.
        foreach (int side in new[] { -1, 1 })
        {
            var tool = new Node3D { Name = $"Hammer{side}", Position = new Vector3(0, 1.09f, .075f), Rotation = new Vector3(0, 0, side * .72f) };
            _sign.AddChild(tool);
            AddBox(tool, "Handle", Vector3.Zero, new(.04f, .34f, .025f), _border);
            AddBox(tool, "Head", new(0, .17f, 0), new(.20f, .09f, .03f), _border);
        }
        SetPercent(0);
    }

    public void SetPercent(int percent)
    {
        Percent = percent;
    }

    public override void _Process(double delta)
    {
        // 직사각형 테두리는 건물 회전을 따르고 중앙 팻말만 카메라 쪽을 향합니다.
        Camera3D camera = GetViewport().GetCamera3D();
        if (camera != null) _sign.GlobalRotation = new Vector3(0, camera.GlobalRotation.Y, 0);
    }

    private static void AddBox(Node3D parent, string name, Vector3 position, Vector3 size, Material material)
        => parent.AddChild(new MeshInstance3D
        {
            Name = name, Position = position, Scale = size, Mesh = _box, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
}

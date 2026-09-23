using Godot;

// 건물은 서버가 보내준 식별자와 배치를 표시합니다. SideId는 접속한 플레이어 ID가 아닙니다.
public partial class Building : Node3D
{
    [Export] public uint BuildingType { get; set; }
    [Export] public float HealthBarHeight { get; set; } = 4.7f;
    public HealthBar HealthBar { get; private set; }
    public uint BuildingId { get; private set; }
    public uint SideId { get; private set; }

    public override void _Ready() => HealthBar = HealthBar.Attach(this, HealthBarHeight, 76);

    public void ApplyHealth(HealthSnapshot health) => HealthBar.Apply(health.Current, health.Maximum);

    public void ApplySnapshot(uint id, uint sideId, float x, float z, float yaw)
    {
        BuildingId = id;
        GlobalPosition = new Vector3(x, 0, z);
        GlobalRotation = new Vector3(0, yaw, 0);
        if (SideId == sideId) return;
        SideId = sideId;
        UpdateBanner();
    }

    private void UpdateBanner()
    {
        MeshInstance3D visual = GetNode<MeshInstance3D>("Visual");
        for (int i = 0; i < visual.Mesh.GetSurfaceCount(); i++)
        {
            if (visual.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source ||
                source.ResourceName != "banner") continue;

            // 메시 재질은 공유됩니다. 각 건물에 별도 재질을 덮어씌웁니다.
            var banner = (StandardMaterial3D)source.Duplicate();
            banner.AlbedoColor = SideId switch
            {
                1 => new Color("376a94"),
                2 => new Color("a33c37"),
                _ => source.AlbedoColor
            };
            visual.SetSurfaceOverrideMaterial(i, banner);
        }
    }
}

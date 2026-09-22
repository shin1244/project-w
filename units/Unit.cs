using Godot;

public partial class Unit : Node3D
{
    // 서버의 타입 번호: 0 = 기사, 1 = 궁수. 각 씬에서 지정합니다.
    [Export] public uint UnitType { get; set; }

    public uint UnitId { get; private set; }
    public uint OwnerId { get; private set; }

    public void Initialize(uint unitId, uint ownerId)
    {
        UnitId = unitId;
        OwnerId = ownerId;
    }

    // 루트는 발밑(Y=0)에 둡니다. 실제 이동 계산은 서버에서만 합니다.
    public void ApplyServerPosition(float x, float z)
    {
        GlobalPosition = new Vector3(x, 0f, z);
    }

    public void SetSelected(bool selected)
    {
        GetNode<MeshInstance3D>("SelectionRing").Visible = selected;
    }
}

using Godot;

public partial class Unit : Node3D
{
    // 서버의 타입 번호: 0 = 일꾼, 1 = 기사, 2 = 궁수. 각 씬에서 지정합니다.
    [Export] public uint UnitType { get; set; }

    public uint UnitId { get; private set; }
    public uint OwnerId { get; private set; }
    private bool _hasServerPosition;

    public void Initialize(uint unitId, uint ownerId)
    {
        UnitId = unitId;
        OwnerId = ownerId;
    }

    // 루트는 발밑(Y=0)에 둡니다. 실제 이동 계산은 서버에서만 합니다.
    public void ApplyServerPosition(float x, float z)
    {
        Vector3 next = new(x, 0f, z);
        Vector3 direction = next - GlobalPosition;
        direction.Y = 0f;
        GlobalPosition = next;

        // 첫 스폰은 제외하고, 움직였을 때만 모델의 정면(-Z)을 이동 방향으로 돌립니다.
        if (_hasServerPosition && direction.LengthSquared() > 0.000001f)
        {
            Node3D visual = GetNode<Node3D>("Visual");
            visual.LookAt(visual.GlobalPosition + direction, Vector3.Up);
        }

        _hasServerPosition = true;
    }

    public void SetSelected(bool selected)
    {
        GetNode<MeshInstance3D>("SelectionRing").Visible = selected;
    }
}

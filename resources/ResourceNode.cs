using Godot;

// 서버 자원 ID와 선택 표시는 모든 외형이 공유합니다.
public partial class ResourceNode : Node3D
{
    // 외형 구분용 값이며 자원 종류나 채집 능력치를 뜻하지 않습니다.
    [Export] public uint VisualVariant { get; set; }
    public uint ResourceId { get; private set; }

    public void Initialize(uint resourceId) => ResourceId = resourceId;

    public void SetSelected(bool selected)
    {
        GetNode<MeshInstance3D>("SelectionRing").Visible = selected;
    }
}

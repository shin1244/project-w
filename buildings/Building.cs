using Godot;
using System;
using System.Collections.Generic;

// 건물은 서버가 보내준 식별자와 배치를 표시합니다. SideId는 접속한 플레이어 ID가 아닙니다.
public partial class Building : Node3D
{
    [Export] public uint BuildingType { get; set; }
    [Export] public float HealthBarHeight { get; set; } = 4.7f;
    public HealthBar HealthBar { get; private set; }
    public uint BuildingId { get; private set; }
    public uint SideId { get; private set; }
    public UnitState State { get; private set; }
    public bool HasServerState { get; private set; }
    public int ConstructionPercent { get; private set; } = 100;
    public bool IsUnderConstruction => ConstructionPercent < 100;
    public bool IsDefense => BuildingType is 1 or 6;
    public Func<uint, Node3D> ResolveFocus { get; set; }
    public event Action<UnitState, bool> StateChanged;
    private Node3D _turret;
    private Node3D _ballista;
    private Marker3D _muzzle;
    private Vector3 _restPosition;
    private Tween _recoil;
    private MeshInstance3D _selection;
    private uint _localTeam;
    private TeamMaterials _teamMaterials;
    private Node3D _visual;
    private bool _completedVisualVisible, _completedTurretVisible;
    private CollisionShape3D _selectionShape;
    private Shape3D _completedShape;
    private Transform3D _completedShapeTransform;
    private ConstructionSite _constructionSite;
    private Marker3D _entrance;
    private Vector3 _entranceOffset;
    private readonly HashSet<MeshInstance3D> _shotTraces = new();

    public override void _Ready()
    {
        HealthBar = HealthBar.Attach(this, HealthBarHeight, 76);
        _visual = GetNode<MeshInstance3D>("Visual");
        _completedVisualVisible = _visual.Visible;
        _teamMaterials = new TeamMaterials((MeshInstance3D)_visual);
        _teamMaterials.Apply(SideId, _localTeam);
        _turret = GetNodeOrNull<Node3D>("Turret");
        _completedTurretVisible = _turret?.Visible ?? false;
        _ballista = GetNodeOrNull<Node3D>("Turret/Ballista");
        _muzzle = GetNodeOrNull<Marker3D>("Turret/Muzzle");
        _entrance = GetNodeOrNull<Marker3D>("Entrance");
        if (_entrance != null) _entranceOffset = _entrance.Position;
        if (_ballista != null) _restPosition = _ballista.Position;
        _selectionShape = GetNodeOrNull<CollisionShape3D>("SelectionArea/CollisionShape3D");
        if (_selectionShape != null)
        {
            _completedShape = _selectionShape.Shape;
            _completedShapeTransform = _selectionShape.Transform;
        }
        SetProcess(BuildingId != 0);
        if (IsUnderConstruction) UpdateConstructionVisuals();
    }

    public void SetSelected(bool selected)
    {
        HealthBar?.SetSelected(selected);
        if (_selection == null && selected)
        {
            Vector2I footprint = GetMeta("footprint").AsVector2I();
            float radius = new Vector2(footprint.X, footprint.Y).Length() * .5f + .08f;
            _selection = new MeshInstance3D
            {
                Name = "SelectionRing",
                Position = new Vector3(0, .045f, 0),
                Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + .045f, Rings = 48, RingSegments = 8 },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color("63e88a")
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_selection);
        }
        if (_selection != null) _selection.Visible = selected;
    }

    public void ApplyServerState(UnitState state)
    {
        if (!IsDefense || state.Activity is not (UnitActivity.Idle or UnitActivity.Attack) || state.Carrying != 0) return;
        // 접속 스냅샷의 누적 발사 횟수로 지난 공격을 재생하지 않습니다.
        bool fired = !IsUnderConstruction && HasServerState && state.SwingSequence != State.SwingSequence;
        State = state;
        HasServerState = true;
        FaceCamera();
        Node3D target = FaceTarget();
        if (fired) PlayShot(target);
        StateChanged?.Invoke(state, fired);
    }

    public override void _Process(double delta)
    {
        FaceCamera();
        FaceTarget();
    }

    // 직교 카메라는 모든 건물에 같은 시선 방향을 사용합니다. 이동/줌에는 회전하지 않습니다.
    public static float ViewYaw(Basis cameraBasis) => Mathf.Atan2(-cameraBasis.Z.X, -cameraBasis.Z.Z);

    private void FaceCamera()
    {
        if (BuildingId == 0 || _visual == null) return;
        Camera3D camera = GetViewport().GetCamera3D();
        float yaw = camera == null ? Mathf.Pi : ViewYaw(camera.GlobalBasis);
        _visual.Rotation = new Vector3(0, yaw, 0);
        if (_turret != null && (!HasServerState || State.Activity != UnitActivity.Attack || State.FocusId == 0))
            _turret.Rotation = new Vector3(0, yaw, 0);
    }

    private Node3D FaceTarget()
    {
        if (IsUnderConstruction || _turret == null || State.Activity != UnitActivity.Attack || State.FocusId == 0) return null;
        Node3D target = ResolveFocus?.Invoke(State.FocusId);
        if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() ||
            target.IsQueuedForDeletion() || target is Unit { IsDying: true }) return null;
        Vector3 aim = target.GlobalPosition;
        aim.Y = _turret.GlobalPosition.Y;
        if (aim.DistanceSquaredTo(_turret.GlobalPosition) > .0001f) _turret.LookAt(aim);
        return target;
    }

    private void PlayShot(Node3D target)
    {
        if (IsUnderConstruction || _ballista == null) return;
        _recoil?.Kill();
        _ballista.Position = _restPosition + new Vector3(0, 0, .15f);
        _recoil = CreateTween();
        _recoil.TweenProperty(_ballista, "position", _restPosition, .22).SetTrans(Tween.TransitionType.Quad);
        if (target == null || _muzzle == null) return;

        // 서버는 즉시 피해를 적용합니다. 짧은 궤적은 발사 피드백만 표시합니다.
        Vector3 start = _muzzle.GlobalPosition;
        Vector3 end = target.GlobalPosition + Vector3.Up;
        float distance = start.DistanceTo(end);
        if (distance < .01f) return;
        var trace = new MeshInstance3D
        {
            Name = "ShotTrace",
            Mesh = new BoxMesh { Size = new Vector3(.035f, .035f, distance) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color("ffd27b")
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(trace);
        _shotTraces.Add(trace);
        trace.GlobalPosition = (start + end) * .5f;
        trace.LookAt(end);
        trace.CreateTween().TweenCallback(Callable.From(() =>
        {
            _shotTraces.Remove(trace);
            trace.QueueFree();
        })).SetDelay(.09);
    }

    public void ApplyConstruction(int percent)
    {
        if (percent < 0 || percent > 100 || percent == ConstructionPercent) return;
        ConstructionPercent = percent;
        if (IsNodeReady()) UpdateConstructionVisuals();
    }

    private void UpdateConstructionVisuals()
    {
        if (IsUnderConstruction)
        {
            StopAttackEffects();
            _visual.Hide();
            _turret?.Hide();
            if (_constructionSite == null)
            {
                _constructionSite = ConstructionSite.Create(GetMeta("footprint").AsVector2I());
                AddChild(_constructionSite);
            }
            _constructionSite.SetPercent(ConstructionPercent);
            _constructionSite.Show();
            _constructionSite.SetProcess(true);
            if (_selectionShape != null)
            {
                Vector2I footprint = GetMeta("footprint").AsVector2I();
                if (_selectionShape.Shape == _completedShape)
                    _selectionShape.Shape = new BoxShape3D
                    {
                        Size = new Vector3(footprint.X, ConstructionSite.SelectionHeight, footprint.Y)
                    };
                _selectionShape.Transform = new Transform3D(Basis.Identity, Vector3.Up * (ConstructionSite.SelectionHeight * .5f));
            }
            HealthBar.SetWorldHeight(ConstructionSite.HealthHeight);
        }
        else
        {
            _visual.Visible = _completedVisualVisible;
            if (_turret != null) _turret.Visible = _completedTurretVisible;
            _constructionSite?.Hide();
            _constructionSite?.SetProcess(false);
            if (_selectionShape != null)
            {
                _selectionShape.Shape = _completedShape;
                _selectionShape.Transform = _completedShapeTransform;
            }
            HealthBar.SetWorldHeight(HealthBarHeight);
            // 누적 발사 번호는 공사 중에도 갱신했습니다. 완성 통지만으로 발사를 재생하지 않습니다.
            FaceCamera();
        }
    }

    private void StopAttackEffects()
    {
        _recoil?.Kill();
        _recoil = null;
        if (_ballista != null) _ballista.Position = _restPosition;
        foreach (MeshInstance3D trace in _shotTraces)
            if (GodotObject.IsInstanceValid(trace))
            {
                trace.Hide();
                RemoveChild(trace);
                trace.QueueFree();
            }
        _shotTraces.Clear();
    }

    public override void _ExitTree()
    {
        _recoil?.Kill();
        _shotTraces.Clear();
        _teamMaterials?.Dispose();
        _teamMaterials = null;
    }

    public void ApplyHealth(HealthSnapshot health) => HealthBar.Apply(health.Current, health.Maximum);

    public void ApplySnapshot(uint id, uint sideId, float x, float z, float yaw)
    {
        BuildingId = id;
        GlobalPosition = new Vector3(x, 0, z);
        // 서버의 충돌은 yaw와 무관한 X/Z 사각형입니다. 외형만 카메라 쪽으로 돌립니다.
        GlobalRotation = Vector3.Zero;
        if (_entrance != null) _entrance.Position = new Basis(Vector3.Up, yaw) * _entranceOffset;
        SetProcess(true);
        FaceCamera();
        FaceTarget();
        if (SideId == sideId) return;
        SideId = sideId;
        _teamMaterials?.Apply(SideId, _localTeam);
    }

    public void SetLocalTeam(uint team)
    {
        _localTeam = team;
        _teamMaterials?.Apply(SideId, _localTeam);
    }
}

using Godot;
using System.Collections.Generic;

// 세 유닛이 공유하는 화면 연출. 게임에서의 제거는 UnitManager가 먼저 처리합니다.
public static class UnitDeathEffect
{
    public const double FallSeconds = 0.45;
    public const double HoldSeconds = 0.15;
    public const double FadeSeconds = 0.45;

    public static Tween Play(Unit unit)
    {
        Node3D visual = unit.GetNode<Node3D>("Visual");
        unit.GetNodeOrNull<AnimationPlayer>("AnimationPlayer")?.Pause();
        var meshes = new List<MeshInstance3D>();
        CollectMeshes(visual, meshes);

        Quaternion start = visual.Quaternion;
        Quaternion fallen = start * new Quaternion(Vector3.Back, -Mathf.Pi / 2);
        Vector3 startPosition = visual.Position;
        // 옆으로 누웠을 때 모델의 가장 낮은 부분이 바닥 아래로 파묻히지 않게 합니다.
        float floorY = 0;
        Transform3D inverse = visual.GlobalTransform.AffineInverse();
        foreach (MeshInstance3D mesh in meshes)
        {
            if (!mesh.IsVisibleInTree()) continue;
            Transform3D relative = inverse * mesh.GlobalTransform;
            Aabb bounds = mesh.GetAabb();
            for (int i = 0; i < 8; i++)
                floorY = Mathf.Min(floorY, (new Basis(fallen) * (relative * bounds.GetEndpoint(i))).Y);
        }
        Vector3 endPosition = new(startPosition.X, -floorY + 0.025f, startPosition.Z);

        Tween tween = unit.CreateTween();
        using var fall = tween.TweenMethod(Callable.From<float>(amount =>
        {
            visual.Quaternion = start.Slerp(fallen, amount);
            visual.Position = startPosition.Lerp(endPosition, amount);
        }), 0f, 1f, FallSeconds);
        fall.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        using var hold = tween.TweenInterval(HoldSeconds);
        using var fade = tween.TweenMethod(Callable.From<float>(amount =>
        {
            foreach (MeshInstance3D mesh in meshes) mesh.Transparency = amount;
        }), 0f, 1f, FadeSeconds);
        using var finish = tween.TweenCallback(Callable.From(unit.QueueFree));
        return tween;
    }

    private static void CollectMeshes(Node node, List<MeshInstance3D> meshes)
    {
        if (node is MeshInstance3D mesh) meshes.Add(mesh);
        foreach (Node child in node.GetChildren()) CollectMeshes(child, meshes);
    }
}

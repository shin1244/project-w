using Godot;

// 카메라 각도/줌을 유지한 채 화면 중심의 지면을 지정 좌표로 옮깁니다.
public static class CameraNavigation
{
    private static readonly Plane Ground = new(Vector3.Up, 0);

    public static bool TryGroundPoint(Camera3D camera, Vector2 screen, out Vector3 point)
    {
        point = default;
        if (!GodotObject.IsInstanceValid(camera) || !camera.IsInsideTree()) return false;
        if (Ground.IntersectsRay(camera.ProjectRayOrigin(screen), camera.ProjectRayNormal(screen)) is not Vector3 hit ||
            !float.IsFinite(hit.X) || !float.IsFinite(hit.Z)) return false;
        point = hit;
        return true;
    }

    public static void FocusGround(Camera3D camera, Vector3 target)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Z) || !GodotObject.IsInstanceValid(camera) ||
            !TryGroundPoint(camera, camera.GetViewport().GetVisibleRect().GetCenter(), out Vector3 current)) return;
        camera.GlobalPosition += new Vector3(target.X - current.X, 0, target.Z - current.Z);
        if (camera.HasMethod("clamp_position")) camera.Call("clamp_position");
    }
}

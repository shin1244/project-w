using Godot;
using System;
using System.Collections.Generic;

// 서버와 동일한 타입 ID, 건물 외형·점유 크기·선택 영역 검증.
public partial class ProductionBuildingChecks : Node3D
{
    private const float Tolerance = .001f;

    public override void _Ready()
    {
        try
        {
            CheckAsset("TownHall", new Vector2I(5, 5), 0);
            CheckAsset("Store", new Vector2I(3, 3), 2);
            CheckAsset("Supply", new Vector2I(3, 3), 3);
            CheckAsset("Barracks", new Vector2I(5, 3), 4);
            CheckAsset("Forge", new Vector2I(5, 3), 5);
            CheckAsset("Fortress", new Vector2I(4, 4), 1);
            CheckAsset("Tower", new Vector2I(3, 3), 6);
            CheckExistingSquareSelection("TownHall", 5);
            CheckExistingSquareSelection("Fortress", 4);
            CheckDefenseSweep();
            GD.Print("PASS: all seven enlarged server footprints, scaled shared meshes, selection/health bounds, team colours and 3x3 tower weapon sweep including recoil");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckAsset(string name, Vector2I footprint, uint type)
    {
        PackedScene scene = GD.Load<PackedScene>($"res://buildings/{name}.tscn");
        Check(scene != null, $"{name}: scene loads");
        var blue = scene.Instantiate<Building>();
        var red = scene.Instantiate<Building>();
        AddChild(blue);
        AddChild(red);
        Check(blue.Scale.IsEqualApprox(Vector3.One), $"{name}: root keeps world units while visual children enlarge the original meshes");
        Check(blue.BuildingType == type && blue.GetMeta("footprint").AsVector2I() == footprint,
            $"{name}: reserved asset type and footprint");
        Check(blue.GetMeta("front").AsString() == "-Z", $"{name}: front convention is -Z");

        Aabb bounds = MeshBounds(blue);
        Vector3 end = bounds.End;
        Check(Near(bounds.Position.X, -footprint.X * .5f) && Near(end.X, footprint.X * .5f) &&
            Near(bounds.Position.Z, -footprint.Y * .5f) && Near(end.Z, footprint.Y * .5f),
            $"{name}: all visual geometry fills the centered {footprint.X}x{footprint.Y} footprint without overhang; bounds={bounds}");
        Check(bounds.Position.Y >= -Tolerance && end.Y > .5f,
            $"{name}: complete geometry stands above ground");
        Check(blue.HealthBar != null && blue.HealthBarHeight > end.Y + Tolerance,
            $"{name}: health anchor clears the highest visual point ({end.Y})");

        var area = blue.GetNode<Area3D>("SelectionArea");
        Check(area.CollisionLayer == 8 && area.CollisionMask == 0,
            $"{name}: selection uses its existing dedicated layer");
        var collider = area.GetNode<CollisionShape3D>("CollisionShape3D");
        Check(!collider.Disabled && collider.Shape is BoxShape3D, $"{name}: selection box is active");
        var box = (BoxShape3D)collider.Shape;
        Check(Near(box.Size.X, footprint.X) && Near(box.Size.Z, footprint.Y),
            $"{name}: collision box matches exact footprint dimensions");
        Aabb selection = TransformedBounds(new Aabb(-box.Size * .5f, box.Size),
            blue.GlobalTransform.AffineInverse() * collider.GlobalTransform);
        Check(Near(selection.Position.X, -footprint.X * .5f) && Near(selection.End.X, footprint.X * .5f) &&
            Near(selection.Position.Z, -footprint.Y * .5f) && Near(selection.End.Z, footprint.Y * .5f) &&
            Near(selection.Position.Y, 0) && selection.End.Y + Tolerance >= end.Y,
            $"{name}: selection covers the full model from the ground");

        var entrance = blue.GetNode<Marker3D>("Entrance");
        Vector3 entrancePosition = blue.ToLocal(entrance.GlobalPosition);
        Check(Near(entrancePosition.Z, -footprint.Y * .5f) &&
            Mathf.Abs(entrancePosition.X) <= footprint.X * .5f && Near(entrancePosition.Y, 0),
            $"{name}: entrance is on the front ground edge");

        CheckBannerIsolation(name, blue, red);
        blue.SetSelected(true);
        var ring = blue.GetNode<MeshInstance3D>("SelectionRing");
        var ringMesh = (TorusMesh)ring.Mesh;
        float expectedRadius = new Vector2(footprint.X, footprint.Y).Length() * .5f + .08f;
        Check(ring.Visible && Near(ringMesh.InnerRadius, expectedRadius) && Near(ringMesh.OuterRadius, expectedRadius + .045f),
            $"{name}: selection ring encloses the rectangle diagonal");
        blue.SetSelected(false);
        Check(!ring.Visible, $"{name}: deselection hides the ring");
        GD.Print($"{name}: footprint={footprint}, height={end.Y:0.000}, selection radius={ringMesh.InnerRadius:0.000}");
        blue.Free();
        red.Free();
    }

    private static void CheckBannerIsolation(string name, Building blue, Building red)
    {
        var visualA = blue.GetNode<MeshInstance3D>("Visual");
        var visualB = red.GetNode<MeshInstance3D>("Visual");
        Check(visualA.Mesh == visualB.Mesh, $"{name}: instances share immutable model geometry");
        var sources = new List<(int Index, StandardMaterial3D Material, Color Color)>();
        for (int i = 0; i < visualA.Mesh.GetSurfaceCount(); i++)
            if (visualA.Mesh.SurfaceGetMaterial(i) is StandardMaterial3D material && material.ResourceName == "banner")
                sources.Add((i, material, material.AlbedoColor));
        Check(sources.Count > 0, $"{name}: main visual includes team banner surfaces");
        blue.ApplySnapshot(101, 1, -5, 0, 0);
        red.ApplySnapshot(102, 2, 5, 0, 0);
        blue.SetLocalTeam(1);
        red.SetLocalTeam(1);
        foreach (var source in sources)
        {
            var blueBanner = visualA.GetSurfaceOverrideMaterial(source.Index) as StandardMaterial3D;
            var redBanner = visualB.GetSurfaceOverrideMaterial(source.Index) as StandardMaterial3D;
            Check(blueBanner != null && redBanner != null && blueBanner != redBanner &&
                blueBanner != source.Material && redBanner != source.Material &&
                blueBanner.AlbedoColor.IsEqualApprox(TeamMaterials.FriendlyColor) &&
                redBanner.AlbedoColor.IsEqualApprox(TeamMaterials.EnemyColor) &&
                source.Material.AlbedoColor.IsEqualApprox(source.Color),
                $"{name}: team snapshots tint only each instance's banner");
        }
        blue.ApplySnapshot(101, 2, -5, 0, 0);
        blue.ApplySnapshot(101, 1, -5, 0, 0);
        foreach (var source in sources)
            Check(((StandardMaterial3D)visualB.GetSurfaceOverrideMaterial(source.Index)).AlbedoColor.IsEqualApprox(TeamMaterials.EnemyColor) &&
                source.Material.AlbedoColor.IsEqualApprox(source.Color),
                $"{name}: repeated team changes preserve other instances and shared materials");
    }

    private void CheckDefenseSweep()
    {
        var tower = GD.Load<PackedScene>("res://buildings/Tower.tscn").Instantiate<Building>();
        AddChild(tower);
        var turret = tower.GetNode<Node3D>("Turret");
        var weapon = tower.GetNode<MeshInstance3D>("Turret/Ballista");
        for (int angle = 0; angle < 360; angle += 15)
        {
            turret.RotationDegrees = new Vector3(0, angle, 0);
            foreach (float recoil in new[] { 0f, .15f })
            {
                weapon.Position = new Vector3(0, 0, recoil);
                Transform3D transform = tower.GlobalTransform.AffineInverse() * weapon.GlobalTransform;
                // Transform real vertices: rotating a local AABB would include empty corners beyond the bow.
                for (int surface = 0; surface < weapon.Mesh.GetSurfaceCount(); surface++)
                    foreach (Vector3 vertex in weapon.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    {
                        Vector3 point = transform * vertex;
                        Check(Mathf.Abs(point.X) <= 1.501f && Mathf.Abs(point.Z) <= 1.501f,
                            $"Tower: enlarged weapon rotation/recoil stays in 3x3 at {angle} degrees");
                    }
            }
        }
        tower.Free();
    }

    private void CheckExistingSquareSelection(string name, int size)
    {
        var building = GD.Load<PackedScene>($"res://buildings/{name}.tscn").Instantiate<Building>();
        AddChild(building);
        building.SetSelected(true);
        float radius = ((TorusMesh)building.GetNode<MeshInstance3D>("SelectionRing").Mesh).InnerRadius;
        Check(Near(radius, size * .7072f + .08f), $"{name}: selection radius encloses the enlarged square footprint");
        building.Free();
    }

    private static Aabb MeshBounds(Building building)
    {
        Aabb? combined = null;
        foreach (Node node in Descendants(building))
        {
            if (node is not MeshInstance3D { Mesh: not null } mesh) continue;
            Aabb bounds = TransformedBounds(mesh.Mesh.GetAabb(), building.GlobalTransform.AffineInverse() * mesh.GlobalTransform);
            combined = combined.HasValue ? combined.Value.Merge(bounds) : bounds;
        }
        Check(combined.HasValue, $"{building.Name}: contains visual geometry");
        return combined.Value;
    }

    private static Aabb TransformedBounds(Aabb bounds, Transform3D transform)
    {
        Vector3 first = transform * bounds.GetEndpoint(0);
        Aabb result = new Aabb(first, Vector3.Zero);
        for (int i = 1; i < 8; i++) result = result.Expand(transform * bounds.GetEndpoint(i));
        return result;
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool Near(float a, float b) => Mathf.Abs(a - b) < Tolerance;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

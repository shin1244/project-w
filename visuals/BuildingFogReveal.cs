using Godot;
using System;
using System.Collections.Generic;

// 원본 깊이 검사를 유지하며 안개(우선순위 120) 뒤에 건물 표면만 다시 그립니다.
// 지형 시야 마스크를 넓히지 않아 주변 땅이나 다른 물체를 공개하지 않습니다.
public sealed class BuildingFogReveal : IDisposable
{
    private readonly Dictionary<MeshInstance3D, MeshInstance3D> _meshes = new();
    private readonly Dictionary<StandardMaterial3D, StandardMaterial3D> _materials = new();
    public bool Enabled { get; private set; }

    public void Include(Node root)
    {
        if (root == null) return;
        foreach (Node child in root.GetChildren())
            if (child.Name != "FogReveal") Include(child);
        if (root is not MeshInstance3D source || source.Mesh == null || _meshes.ContainsKey(source)) return;
        var overlay = new MeshInstance3D
        {
            Name = "FogReveal", Mesh = source.Mesh, Visible = Enabled,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        for (int i = 0; i < source.Mesh.GetSurfaceCount(); i++)
        {
            if (source.GetActiveMaterial(i) is not StandardMaterial3D material)
                throw new InvalidOperationException("Building fog reveal requires StandardMaterial3D: " + source.GetPath());
            if (!_materials.TryGetValue(material, out StandardMaterial3D copy))
            {
                copy = (StandardMaterial3D)material.Duplicate();
                copy.NextPass = null;
                copy.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                copy.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled;
                copy.NoDepthTest = false;
                copy.RenderPriority = 121;
                _materials.Add(material, copy);
            }
            overlay.SetSurfaceOverrideMaterial(i, copy);
        }
        source.AddChild(overlay);
        _meshes.Add(source, overlay);
    }

    public void SetEnabled(bool enabled)
    {
        if (Enabled != enabled)
            foreach (MeshInstance3D mesh in _meshes.Values) mesh.Visible = enabled;
        Enabled = enabled;
        if (enabled) RefreshColors();
    }

    public void RefreshColors()
    {
        foreach (var pair in _materials) pair.Value.AlbedoColor = pair.Key.AlbedoColor;
    }

    public void Dispose()
    {
        foreach (MeshInstance3D mesh in _meshes.Values)
            if (GodotObject.IsInstanceValid(mesh)) { mesh.Hide(); mesh.QueueFree(); }
        foreach (StandardMaterial3D material in _materials.Values) material.Dispose();
        _meshes.Clear();
        _materials.Clear();
    }
}

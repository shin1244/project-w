using Godot;
using System;
using System.Collections.Generic;

// 팀 표시용으로 이름 붙인 재질만 인스턴스별로 복제합니다. 메시와 원본 재질은 공유해도 안전합니다.
public sealed class TeamMaterials : IDisposable
{
    public static readonly Color FriendlyColor = new("8dbde3");
    public static readonly Color EnemyColor = new("e3a09b");

    private sealed class TintMaterial
    {
        public StandardMaterial3D Material;
        public Color OriginalColor;
        public float Shade;
    }

    private sealed class Binding
    {
        public MeshInstance3D Mesh;
        public int Surface;
        public Material PreviousOverride;
        public StandardMaterial3D Override;
    }

    private readonly Dictionary<ulong, TintMaterial> _materials = new();
    private readonly List<Binding> _bindings = new();
    private int _relationship;
    private bool _disposed;

    public TeamMaterials(Node visualRoot)
    {
        ArgumentNullException.ThrowIfNull(visualRoot);
        Collect(visualRoot);
    }

    // 팀 번호 자체가 아니라 현재 플레이어와의 관계가 달라졌을 때만 색을 갱신합니다.
    public void Apply(uint objectTeam, uint localTeam)
    {
        if (_disposed) return;
        int relationship = objectTeam == 0 || localTeam == 0 ? 0 : objectTeam == localTeam ? 1 : 2;
        if (_relationship == relationship) return;
        _relationship = relationship;

        foreach (TintMaterial entry in _materials.Values)
        {
            Color color = relationship == 1 ? FriendlyColor : EnemyColor;
            entry.Material.AlbedoColor = relationship == 0
                ? entry.OriginalColor
                : new Color(color.R * entry.Shade, color.G * entry.Shade,
                    color.B * entry.Shade, entry.OriginalColor.A);
        }
    }

    private void Collect(Node node)
    {
        if (node is MeshInstance3D mesh)
        {
            if (mesh.MaterialOverride != null)
            {
                // 전체 덮어쓰기 재질이 있으면 표면 재질은 표시되지 않습니다.
                AddBinding(mesh, -1, mesh.MaterialOverride, mesh.MaterialOverride);
            }
            else if (mesh.Mesh != null)
            {
                for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
                {
                    Material previous = mesh.GetSurfaceOverrideMaterial(surface);
                    AddBinding(mesh, surface, previous ?? mesh.Mesh.SurfaceGetMaterial(surface), previous);
                }
            }
        }

        foreach (Node child in node.GetChildren()) Collect(child);
    }

    private void AddBinding(MeshInstance3D mesh, int surface, Material material, Material previous)
    {
        if (material is not StandardMaterial3D source || !TryGetShade(source.ResourceName, out float shade))
            return;

        ulong sourceId = source.GetInstanceId();
        if (!_materials.TryGetValue(sourceId, out TintMaterial entry))
        {
            entry = new TintMaterial
            {
                Material = (StandardMaterial3D)source.Duplicate(),
                OriginalColor = source.AlbedoColor,
                Shade = shade
            };
            _materials.Add(sourceId, entry);
        }

        if (surface < 0) mesh.MaterialOverride = entry.Material;
        else mesh.SetSurfaceOverrideMaterial(surface, entry.Material);

        _bindings.Add(new Binding
        {
            Mesh = mesh,
            Surface = surface,
            PreviousOverride = previous,
            Override = entry.Material
        });
    }

    private static bool TryGetShade(string name, out float shade)
    {
        shade = name switch
        {
            "team_cloth" or "banner" or "roof_light" => 1f,
            "roof_mid" => .91f,
            "roof" => .8f,
            _ => 0f
        };
        return shade > 0f;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 살아 있는 노드에서는 기존 덮어쓰기를 복원하고, 소유한 복제본만 해제합니다.
        foreach (Binding binding in _bindings)
        {
            if (!GodotObject.IsInstanceValid(binding.Mesh)) continue;
            if (binding.Surface < 0)
            {
                if (binding.Mesh.MaterialOverride == binding.Override)
                    binding.Mesh.MaterialOverride = binding.PreviousOverride;
            }
            else if (binding.Mesh.Mesh != null && binding.Surface < binding.Mesh.Mesh.GetSurfaceCount()
                && binding.Mesh.GetSurfaceOverrideMaterial(binding.Surface) == binding.Override)
            {
                binding.Mesh.SetSurfaceOverrideMaterial(binding.Surface, binding.PreviousOverride);
            }
        }

        _bindings.Clear();
        foreach (TintMaterial entry in _materials.Values) entry.Material.Dispose();
        _materials.Clear();
    }
}

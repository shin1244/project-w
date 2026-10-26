using System;
using System.Globalization;
using Godot;

// TUNNEL_LINK tunnelId partnerId: a tunnel and the exit the commander opened from it (TUNNEL_EXIT), sent only
// to that team. The client keeps the link to pick the panel and right-click orders; the server decides it.
public partial class BuildingManager
{
    public event Action TunnelLinkChanged;

    public void HandleTunnelLink(string[] parts)
    {
        if (parts.Length != 3 || parts[0] != "TUNNEL_LINK" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint partner) ||
            !_buildings.TryGetValue(id, out Building tunnel) || tunnel.BuildingType != BuildingCatalog.Tunnel ||
            _localTeam == 0 || tunnel.SideId != _localTeam) return;
        if (tunnel.TunnelPartner == partner) return;
        tunnel.TunnelPartner = partner;
        TunnelLinkChanged?.Invoke();
    }

    // The linked tunnel on the same team, built or not; null while unpaired.
    public Building TunnelPartner(Building tunnel)
    {
        if (!GodotObject.IsInstanceValid(tunnel) || tunnel.BuildingType != BuildingCatalog.Tunnel || tunnel.TunnelPartner == 0 ||
            !_buildings.TryGetValue(tunnel.TunnelPartner, out Building partner) || partner.IsQueuedForDeletion() ||
            partner.SideId != tunnel.SideId) return null;
        return partner;
    }

    // Units can pass only when both tunnels stand complete.
    public Building TunnelExit(Building tunnel) =>
        GodotObject.IsInstanceValid(tunnel) && !tunnel.IsUnderConstruction && TunnelPartner(tunnel) is { IsUnderConstruction: false } exit
            ? exit : null;
}

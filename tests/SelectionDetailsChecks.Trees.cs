using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class SelectionDetailsChecks
{
    // Trees come from the synchronized map; their amounts from the test's own TREE lines.
    // The real clicks that pick a tree are in RtsControlChecks.
    private async Task CheckTreeInspection()
    {
        ResourceNode tree = Resources.GetChildren().OfType<ResourceNode>().First();
        uint id = tree.ResourceId;
        var ring = tree.GetNode<MeshInstance3D>("SelectionRing");
        InvokeMain("SelectResource", tree);
        Receive($"TREE {id} 37");
        await Layout();
        Check(Resources.SelectedResource == tree && ring.Visible && Single.Visible &&
            Labels().Any(label => label.Visible && label.Text == Lang.T("나무")) &&
            Labels().Any(label => label.Visible && label.Text == Lang.F("남은 채집량   {0}", 37)),
            "The tree panel shows its name and the server's remaining amount");
        Check(!Labels().Any(label => label.Visible && (label.Text.StartsWith(Lang.T("체력")) || label.Text.StartsWith(Lang.T("공격력")))) &&
            !Single.FindChildren("Health", "ProgressBar", true, false).OfType<Control>().Single().Visible,
            "The tree panel shows nothing but its name and remaining amount");

        // Main.ConnectInput, which this scene skips, also drops the tree; the panel prefers the building anyway.
        Receive($"BUILDING 3 9501 {Units.LocalTeam} 30 30 0");
        // Reveal it now, not on the fog's 20 Hz tick, so the removal below always frees a fog reveal overlay.
        Fog.RefreshVision();
        InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single(building => building.BuildingId == 9501));
        await Layout();
        Check(Labels().Any(label => label.Visible && label.Text.StartsWith(Lang.T("체력"))) &&
            !Labels().Any(label => label.Visible && label.Text.StartsWith(Lang.F("남은 채집량   {0}", ""))),
            "A building selected after a tree shows its own health again");
        Receive("REMOVE 9501");

        InvokeMain("ClearSelection");
        InvokeMain("SelectResource", tree);
        Receive($"TREE {id} 0");
        await Layout();
        Check(Resources.SelectedResource == null && !Single.Visible, "A felled tree clears its inspection");
        ResourceNode other = Resources.GetChildren().OfType<ResourceNode>().First(node => !node.IsQueuedForDeletion());
        InvokeMain("SelectResource", other);
        InvokeMain("ClearSelection");
        Check(Resources.SelectedResource == null && !other.GetNode<MeshInstance3D>("SelectionRing").Visible,
            "Clicking empty ground clears the inspected tree");
    }
}

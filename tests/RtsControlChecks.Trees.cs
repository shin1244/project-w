using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class RtsControlChecks
{
    // Real left clicks on a tree of the synchronized map; the panel itself is SelectionDetailsChecks.
    private async Task CheckTreeClicks()
    {
        BeginSession();
        ResourceNode tree = Resources.GetChildren().OfType<ResourceNode>().First();
        Vector3 trunk = tree.GlobalPosition + Vector3.Up * 2;
        Units.Camera.Size = 30;
        CameraNavigation.FocusGround(Units.Camera, tree.GlobalPosition);
        Receive($"UNIT 0 101 7 {tree.GlobalPosition.X + 6} {tree.GlobalPosition.Z} 1");
        await Flush();

        ClickUnit(101);
        await Flush();
        Click(Units.Camera.UnprojectPosition(trunk), MouseButton.Left, shift: true);
        await Flush();
        Expect(101);
        Check(Resources.SelectedResource == null, "Shift-clicking a tree keeps the unit selection and adds no tree");
        Click(Units.Camera.UnprojectPosition(trunk), MouseButton.Left);
        await Flush();
        Expect();
        Check(Resources.SelectedResource == tree && Buildings.SelectedBuilding == null && Units.InspectedUnit == null,
            "A left click on a tree inspects only that tree");
        ClickUnit(101);
        await Flush();
        Expect(101);
        Check(Resources.SelectedResource == null, "Selecting a unit replaces the inspected tree");

        Receive($"UNIT 0 102 7 {tree.GlobalPosition.X} {tree.GlobalPosition.Z} 1");
        await Flush();
        Click(Units.Camera.UnprojectPosition(tree.GlobalPosition + Vector3.Up), MouseButton.Left);
        await Flush();
        Expect(102);
        Check(Resources.SelectedResource == null, "A unit standing at a tree is picked before the tree");

        Receive("WELCOME 7 1 HERO 200");
        Click(Units.Camera.UnprojectPosition(trunk), MouseButton.Left);
        await Flush();
        Check(Resources.SelectedResource == null, "Heroes do not inspect trees");
    }
}

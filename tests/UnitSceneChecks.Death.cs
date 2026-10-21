using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class UnitSceneChecks
{
    // DEATH only picks the presentation of the next REMOVE; it never removes or revives a unit itself.
    private async Task CheckDeathCauses()
    {
        bool Parse(string line, out DeathSnapshot death) => DeathSnapshot.TryParse(line.Split(' '), out death);
        Check(Parse("DEATH 9600 BLADE", out var blade) && blade == new DeathSnapshot(9600, DeathCause.Blade) &&
            Parse("DEATH 9600 SHOT", out var shot) && shot.Cause == DeathCause.Shot &&
            Parse("DEATH 9600 FIRE", out var fire) && fire.Cause == DeathCause.Fire &&
            Parse("DEATH 9600 BLAST", out var blast) && blast.Cause == DeathCause.Blast,
            "Every server death cause parses with its unit ID");
        foreach (string invalid in new[] { "DEATH", "DEATH 9600", "DEATH 9600 BLADE extra", "DEATH 0 BLADE",
            "DEATH -1 BLADE", "DEATH 4294967296 BLADE", "DEATH 9600 blade", "DEATH 9600 NONE", "DEATH 9600 LASER", "REMOVE 9600 BLADE" })
            Check(!Parse(invalid, out _), $"Malformed or unknown death cause accepted: {invalid}");

        int Pieces(Unit unit) => unit.GetChildren().Count(child => child.Name.ToString().StartsWith("BreakupPiece"));
        int Burns(Unit unit) => unit.GetChildren().Count(child => child.Name.ToString().StartsWith("Burn"));
        Unit Spawn(uint id, int x)
        {
            Receive($"UNIT 1 {id} 8 {x} 9 2");
            Units.TryGetUnit(id, out Unit unit);
            return unit;
        }
        Unit cut = Spawn(9600, -12), burnt = Spawn(9601, -9), shattered = Spawn(9602, -6), plain = Spawn(9603, -3);
        Receive("DEATH 9600 BLADE");
        Receive("DEATH 9601 FIRE");
        Receive("DEATH 9602 BLAST");
        Receive("DEATH 9699 BLAST");
        Check(cut.DeathCause == DeathCause.Blade && burnt.DeathCause == DeathCause.Fire && shattered.DeathCause == DeathCause.Blast &&
            plain.DeathCause == DeathCause.None && new[] { cut, burnt, shattered, plain }.All(unit => !unit.IsDying),
            "DEATH marks only its own live unit and does not start a death before REMOVE");
        foreach (uint id in new uint[] { 9600, 9601, 9602, 9603 }) Receive($"REMOVE {id}");
        Receive("DEATH 9603 FIRE");
        Check(plain.IsDying && plain.DeathCause == DeathCause.None && Pieces(plain) == 0 && Burns(plain) == 0,
            "A cause arriving after REMOVE cannot change the death that already started");
        Check(Pieces(cut) == 2 && Burns(cut) == 0, "A blade cuts two parts off and the rest falls");
        Check(Burns(burnt) > 0 && Pieces(burnt) == 0, "Fire chars the model into ash instead of breaking it apart");
        Check(Pieces(shattered) > 2 && Burns(shattered) == 0, "A siege shell throws every part");
        // Each minion's and the artillery's part list matches its scene: a missing node would silently fall back to the plain fall.
        foreach ((uint type, uint id, string cause, bool cuts) in new[] { (100u, 9610u, "BLADE", true), (101u, 9611u, "BLAST", false),
            (102u, 9612u, "BLADE", true), (100u, 9613u, "BLAST", false), (101u, 9614u, "BLADE", true), (102u, 9615u, "BLAST", false),
            (5u, 9616u, "BLADE", true), (5u, 9617u, "BLAST", false) })
        {
            Receive($"UNIT {type} {id} {(UnitCatalog.IsMinion(type) ? 0 : 8)} {(int)id - 9620} 12 2");
            Units.TryGetUnit(id, out Unit minion);
            Receive($"DEATH {id} {cause}");
            Receive($"REMOVE {id}");
            Check(cuts ? Pieces(minion) == 2 : Pieces(minion) > 2, $"Unit type {type} comes apart on {cause}");
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}

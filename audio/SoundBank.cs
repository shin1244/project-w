// The synthesized effects in audio/sfx/<Name>-<n>.wav (tools/synth_sfx.py writes them).
// MaxVoices and MinGap keep a big fight readable; Priority decides which sound may cut another
// off when every voice is busy (higher wins).
public sealed record SoundDef(string Name, int Variants, string Bus, float VolumeDb, int MaxVoices,
    float MinGap, int Priority, float PitchJitter = .05f);

public static class SoundBank
{
    public const string World = "SFX", Interface = "UI";

    public static readonly SoundDef[] All =
    {
        // Weapons
        new("rifle", 3, World, -7, 4, .04f, 1),
        new("pistol", 2, World, -8, 3, .05f, 1),
        new("bow", 2, World, -8, 3, .05f, 1),
        new("sword", 3, World, -8, 4, .04f, 1),
        new("claw", 2, World, -6, 2, .06f, 2),
        new("stone", 2, World, -4, 2, .08f, 2),
        new("flame", 2, World, -6, 2, .12f, 2),
        new("tower-shot", 2, World, -5, 2, .1f, 3),
        new("artillery-fire", 2, World, -2, 2, .15f, 4, .03f),
        new("shell-impact", 2, World, -3, 3, .08f, 4, .04f),
        new("artillery-deploy", 1, World, -4, 2, .2f, 3, .02f),
        new("artillery-pack", 1, World, -5, 2, .2f, 3, .02f),
        new("knight-charge", 1, World, -4, 2, .15f, 3),
        new("skill-cast", 2, World, -6, 3, .08f, 3),
        new("skill-ultimate", 1, World, -3, 2, .2f, 4, .02f),
        new("ram-impact", 1, World, 0, 1, .3f, 5, .02f),
        // Work
        new("chop", 3, World, -14, 3, .07f, 0, .08f),
        new("hammer", 2, World, -14, 3, .07f, 0, .08f),
        // Deaths and destruction
        new("death-soldier", 3, World, -6, 4, .05f, 2),
        new("death-machine", 2, World, -3, 3, .1f, 4),
        new("death-beast", 2, World, -5, 2, .1f, 3),
        new("death-heavy", 2, World, -4, 2, .1f, 4),
        new("building-collapse", 1, World, -1, 2, .3f, 5, .02f),
        // Construction and production, heard only for the player's own team
        new("build-start", 1, World, -8, 2, .2f, 2, .02f),
        new("build-complete", 1, Interface, -6, 1, .5f, 3, 0),
        new("unit-ready", 1, Interface, -9, 1, .35f, 2, 0),
        // Interface
        new("ui-click", 1, Interface, -12, 2, .03f, 1, .03f),
        new("ui-error", 1, Interface, -10, 1, .4f, 2, 0),
        new("select", 3, Interface, -10, 1, .08f, 1),
        new("order-move", 2, Interface, -11, 1, .08f, 1),
        new("order-attack", 2, Interface, -9, 1, .08f, 1),
    };

    // A unit's attack or work swing. Steam artillery fires and lands its own shells in
    // WorkshopMachineVisual and WorkshopShellFlight.
    public static string Swing(uint type, UnitActivity activity) => (type, activity) switch
    {
        (UnitCatalog.Worker, UnitActivity.Build) => "hammer",
        (UnitCatalog.Worker, _) => "chop",
        (_, not UnitActivity.Attack) => null,
        (UnitCatalog.Knight or UnitCatalog.MinionMelee or UnitCatalog.MinionFurnace or UnitCatalog.MinionWarden, _) => "sword",
        (UnitCatalog.Archer, _) => "rifle",
        (UnitCatalog.MinionHealer or UnitCatalog.HeroMarshal, _) => "pistol",
        (UnitCatalog.MinionRanged or UnitCatalog.MinionArc or UnitCatalog.HeroRanger, _) => "bow",
        (UnitCatalog.FlameAutomaton, _) => "flame",
        (UnitCatalog.HeroTest or UnitCatalog.JungleBoar, _) => "claw",
        (UnitCatalog.HeroGolem or UnitCatalog.JungleTreant, _) => "stone",
        _ => null
    };

    public static string Death(uint type) => type switch
    {
        UnitCatalog.FlameAutomaton or UnitCatalog.SteamArtillery or UnitCatalog.SiegeRam => "death-machine",
        UnitCatalog.HeroTest or UnitCatalog.JungleBoar => "death-beast",
        UnitCatalog.HeroGolem or UnitCatalog.JungleTreant => "death-heavy",
        _ => "death-soldier"
    };

    // Orders the local player sends, by protocol verb.
    public static string Order(string command)
    {
        int space = command.IndexOf(' ');
        return (space < 0 ? command : command[..space]) switch
        {
            "ATTACK" or "ATTACK_MOVE" => "order-attack",
            "MOVE" or "GATHER" or "RETURN" or "BUILD" or "CONSTRUCT" or "STOP" or "HOLD" or "RALLY" or
                "PICKUP_CRATE" or "DELIVER_ITEM" or "NUGGET" or "TRIBUTE" => "order-move",
            _ => null
        };
    }
}

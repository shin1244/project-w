using Godot;

// The lobby screens' three tones (docs/art-direction.md): sooty iron for the ground and text,
// brass for headings and the player's own marks, furnace ember for the one action and warnings.
public static class LobbyPalette
{
    public static readonly Color Text = new("e4dfd3"), Muted = new("8d8a82"), Faint = new("6b6964");
    public static readonly Color Brass = new("c9a04a"), BrassLight = new("e0ba62");
    public static readonly Color Ember = new("ea7b3c");

    public static Label Caption(string text, int size = 13, Color? color = null)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", color ?? Muted);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
}

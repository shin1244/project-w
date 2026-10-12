using Godot;
using System;
using System.Globalization;

// 체력 뒤에 보호막을 이어 그린다. 합계가 최대 체력을 넘으면 같은 비율로 축소한다.
public static class HealthDisplay
{
    public static readonly Color ShieldColor = new("bceef4");

    public static float HealthFraction(float current, float maximum, float shield) =>
        maximum > 0 ? (float)(current / Math.Max(maximum, (double)current + shield)) : 0;
    public static float ShieldFraction(float current, float maximum, float shield) =>
        maximum > 0 ? (float)(shield / Math.Max(maximum, (double)current + shield)) : 0;

    public static ColorRect CreateShieldFill(ProgressBar bar)
    {
        var fill = new ColorRect { Name = "Shield", Color = ShieldColor, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddChild(fill);
        fill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        fill.OffsetTop = 1;
        fill.OffsetBottom = -1;
        return fill;
    }

    public static void Apply(ProgressBar bar, ColorRect fill, float current, float maximum, float shield)
    {
        float health = HealthFraction(current, maximum, shield);
        bar.Value = health * 100;
        fill.Visible = maximum > 0 && shield > 0;
        fill.AnchorLeft = health;
        fill.AnchorRight = Mathf.Min(1, health + ShieldFraction(current, maximum, shield));
    }

    public static string Text(float current, float maximum, float shield) => maximum <= 0 ? "— / —" :
        $"{Whole(current)} / {Whole(maximum)}" + (shield > 0 ? $"  (+{Whole(shield)})" : "");

    // The server computes HP, shields and damage as real numbers; the UI shows them
    // rounded for readability. A positive value never reads as 0, so a living unit
    // with a sliver of HP still shows 1.
    public static string Whole(float value)
    {
        if (!float.IsFinite(value) || value <= 0) return "0";
        return Math.Max(1, Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
    }
}

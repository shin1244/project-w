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
        $"{Number(current)} / {Number(maximum)}" + (shield > 0 ? $"  (+{Number(shield)})" : "");
    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

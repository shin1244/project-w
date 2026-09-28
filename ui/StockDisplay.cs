using Godot;
using System.Globalization;
using System.Collections.Generic;

// STOCK은 증감량이 아닌 내 자원의 현재 총량입니다. 타인의 자원은 서버가 보내지 않습니다.
public partial class StockDisplay : PanelContainer
{
    [Export] public Godot.Collections.Dictionary<uint, string> ResourceNames { get; set; } = new() { { 0, "목재" } };
    private readonly SortedDictionary<uint, long> _amounts = new();
    private Label _label;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var style = new StyleBoxFlat
        {
            BgColor = new Color("18232eee"),
            BorderColor = new Color("65776a"),
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 10, ContentMarginBottom = 10
        };
        AddThemeStyleboxOverride("panel", style);
        _label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _label.AddThemeFontSizeOverride("font_size", 18);
        _label.AddThemeColorOverride("font_color", new Color("f2e8cb"));
        AddChild(_label);
        Clear();
    }

    public void HandleStock(string[] parts)
    {
        if (parts.Length != 3 || parts[0] != "STOCK" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long amount))
            return;

        _amounts[type] = amount;
        Refresh();
    }

    public void Clear()
    {
        _amounts.Clear();
        // 서버의 전체 목록에서 생략된 자원은 0입니다.
        foreach (uint type in ResourceNames.Keys) _amounts[type] = 0;
        Refresh();
    }

    private void Refresh()
    {
        if (_label == null) return;
        var lines = new List<string> { "보유 자원" };
        foreach (var (type, amount) in _amounts)
        {
            string name = ResourceNames.TryGetValue(type, out string configured) && !string.IsNullOrWhiteSpace(configured)
                ? configured : $"자원 {type}";
            lines.Add($"{name}   {amount.ToString("N0", CultureInfo.InvariantCulture)}");
        }
        if (_amounts.Count == 0) lines.Add("—");
        _label.Text = string.Join("\n", lines);
    }
}

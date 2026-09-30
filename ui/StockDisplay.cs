using Godot;
using System.Globalization;
using System.Collections.Generic;

// STOCK은 증감량이 아닌 내 자원의 현재 총량입니다. 타인의 자원은 서버가 보내지 않습니다.
public partial class StockDisplay : PanelContainer
{
    [Export] public Godot.Collections.Dictionary<uint, string> ResourceNames { get; set; } = new() { { 0, "목재" } };
    private readonly SortedDictionary<uint, long> _amounts = new();
    private HBoxContainer _resources;
    private readonly Dictionary<uint, HBoxContainer> _rows = new();
    private Texture2D _woodIcon, _genericIcon;
    private Label _supplyAmount;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _woodIcon = GD.Load<Texture2D>("res://ui/icons/wood.svg");
        _genericIcon = GD.Load<Texture2D>("res://ui/icons/resource.svg");
        _resources = new HBoxContainer { Name = "Resources", Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = MouseFilterEnum.Ignore };
        _resources.AddThemeConstantOverride("separation", 20);
        AddChild(_resources);
        var supply = new HBoxContainer { Name = "Supply", MouseFilter = MouseFilterEnum.Ignore };
        supply.AddThemeConstantOverride("separation", 8);
        supply.AddChild(new TextureRect { Name = "Icon", Texture = GD.Load<Texture2D>("res://ui/icons/population.svg"),
            CustomMinimumSize = new Vector2(40, 36), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        _supplyAmount = AmountLabel("Amount", "0 / 0", 24);
        supply.AddChild(_supplyAmount);
        _resources.AddChild(supply);
        Clear();
    }

    // 대기열 포함 사용 인구와 완성된 건물의 한도는 서버가 계산합니다.
    public void HandleSupply(string[] parts)
    {
        if (parts.Length != 3 || parts[0] != "SUPPLY" ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long used) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long limit))
            return;
        SetSupply(used, limit);
    }

    private void SetSupply(long used, long limit)
    {
        if (_supplyAmount == null) return;
        _supplyAmount.Text = $"{used.ToString("N0", CultureInfo.InvariantCulture)} / {limit.ToString("N0", CultureInfo.InvariantCulture)}";
        _supplyAmount.AddThemeColorOverride("font_color", new Color(used >= limit ? "ff9b82" : "fff3d6"));
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
        SetSupply(0, 0);
        _amounts.Clear();
        // 서버의 전체 목록에서 생략된 자원은 0입니다.
        foreach (uint type in ResourceNames.Keys) _amounts[type] = 0;
        Refresh();
    }

    private void Refresh()
    {
        if (_resources == null) return;
        foreach (uint type in new List<uint>(_rows.Keys))
            if (!_amounts.ContainsKey(type))
            {
                _resources.RemoveChild(_rows[type]);
                _rows[type].QueueFree();
                _rows.Remove(type);
            }
        int index = 0;
        foreach (var (type, amount) in _amounts)
        {
            if (!_rows.TryGetValue(type, out HBoxContainer row))
            {
                row = new HBoxContainer { Name = $"Resource{type}", MouseFilter = MouseFilterEnum.Ignore };
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(new TextureRect { Name = "Icon", Texture = type == 0 ? _woodIcon : _genericIcon,
                    CustomMinimumSize = new Vector2(40, 36), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
                if (type != 0) row.AddChild(AmountLabel("Type", type.ToString(CultureInfo.InvariantCulture), 13));
                row.AddChild(AmountLabel("Amount", "", 24));
                _resources.AddChild(row);
                _rows.Add(type, row);
            }
            _resources.MoveChild(row, index++);
            row.GetNode<Label>("Amount").Text = amount.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    private static Label AmountLabel(string name, string text, int size)
    {
        var label = new Label { Name = name, Text = text, MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("fff3d6"));
        label.AddThemeColorOverride("font_outline_color", new Color("16212a"));
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeColorOverride("font_shadow_color", new Color("101820cc"));
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }
}

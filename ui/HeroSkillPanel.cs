using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// 늑대 Q만 연결한다. 서버의 COOLDOWN/TICK으로 표시하고 판정은 서버에 맡긴다.
public partial class HeroSkillPanel : PanelContainer
{
    public uint? HeroType { get; private set; }
    public uint? HeroUnitId { get; private set; }
    public event Action<int> TargetingRequested;
    public event Action AvailabilityChanged;
    public bool CanUseQ => HeroType == UnitCatalog.HeroTest && HeroUnitId.HasValue && !_stunned &&
        (!_readyTick.HasValue || _hasTick && RemainingTicks == 0);
    public double QRemainingSeconds => RemainingTicks / (double)InterpolationClock.TickRate;
    private readonly List<StyleBoxFlat> _styles = new();
    private uint _playerId, _team;
    private ProgressBar _health, _mana;
    private Label _healthText, _manaText;
    private Button _qButton;
    private Label _cooldownText;
    private ColorRect _cooldownCover;
    private TextureRect _qIcon;
    private StyleBoxFlat _qNormal, _qTargeting;
    private uint _tick;
    private uint? _readyTick;
    private int _cooldownTicks;
    private bool _hasTick, _stunned, _targeting, _lastAvailable;
    private int RemainingTicks => _readyTick.HasValue && _hasTick ? Math.Max(0, unchecked((int)(_readyTick.Value - _tick))) : 0;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        var frame = Style("18232ef5", "65776a", 6);
        frame.ContentMarginLeft = frame.ContentMarginRight = 12;
        frame.ContentMarginTop = frame.ContentMarginBottom = 10;
        AddThemeStyleboxOverride("panel", frame);

        var contentColumn = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        contentColumn.AddThemeConstantOverride("separation", 8);
        AddChild(contentColumn);
        var vitals = new VBoxContainer { Name = "Vitals", MouseFilter = MouseFilterEnum.Ignore };
        vitals.AddThemeConstantOverride("separation", 4);
        contentColumn.AddChild(vitals);
        (_health, _healthText) = CreateVital(vitals, "Health", "64cf79");
        (_mana, _manaText) = CreateVital(vitals, "Mana", "539fe0");

        var row = new HBoxContainer { Name = "Slots", MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        contentColumn.AddChild(row);
        var passive = Style("242825", "807653", 4);
        var active = Style("101c26", "536b69", 4);
        foreach (string key in new[] { "P", "Q", "W", "E", "R" })
        {
            bool isPassive = key == "P";
            Control slot;
            if (key == "Q")
            {
                _qButton = new Button { FocusMode = FocusModeEnum.None, MouseForcePassScrollEvents = false };
                _qNormal = Style("27332d", "a48b52", 4);
                _qTargeting = Style("394333", "efd28c", 4);
                _qButton.AddThemeStyleboxOverride("normal", _qNormal);
                _qButton.AddThemeStyleboxOverride("hover", _qTargeting);
                _qButton.AddThemeStyleboxOverride("pressed", _qTargeting);
                _qButton.AddThemeStyleboxOverride("disabled", active);
                _qButton.Pressed += RequestQ;
                slot = _qButton;
            }
            else
            {
                var placeholder = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
                placeholder.AddThemeStyleboxOverride("panel", isPassive ? passive : active);
                slot = placeholder;
            }
            slot.Name = isPassive ? "Passive" : key;
            slot.CustomMinimumSize = new Vector2(isPassive ? 64 : 72, isPassive ? 64 : 72);
            slot.SizeFlagsHorizontal = isPassive ? SizeFlags.ShrinkBegin : SizeFlags.ExpandFill;
            slot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(slot);
            var content = new Control { MouseFilter = MouseFilterEnum.Ignore };
            slot.AddChild(content);
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            if (key == "Q")
            {
                _qIcon = new TextureRect { Texture = GD.Load<Texture2D>("res://ui/icons/wolf-bite.svg"),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore };
                content.AddChild(_qIcon);
                _qIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                _qIcon.OffsetLeft = _qIcon.OffsetTop = 8;
                _qIcon.OffsetRight = _qIcon.OffsetBottom = -8;
                _cooldownCover = new ColorRect { Color = new Color(0, 0, 0, .6f), MouseFilter = MouseFilterEnum.Ignore };
                content.AddChild(_cooldownCover);
                _cooldownCover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                _cooldownText = new Label { Name = "Cooldown", HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                _cooldownText.AddThemeFontSizeOverride("font_size", 23);
                _cooldownText.AddThemeColorOverride("font_color", new Color("eee5cd"));
                _cooldownText.AddThemeColorOverride("font_outline_color", new Color("101820"));
                _cooldownText.AddThemeConstantOverride("outline_size", 3);
                content.AddChild(_cooldownText);
                _cooldownText.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }
            var label = new Label { Name = "Key", Text = key, MouseFilter = MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color(isPassive ? "c7ba88" : "9db7b4"));
            content.AddChild(label);
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
            label.OffsetLeft = 8;
            label.OffsetTop = -23;
            label.OffsetRight = 28;
            label.OffsetBottom = -5;
        }
        RefreshQ();
    }

    public void SetHero(uint? heroType, uint playerId = 0, uint team = 0)
    {
        if (HeroType != heroType || _playerId != playerId || _team != team || !heroType.HasValue)
        {
            ClearVitals();
            _readyTick = null;
            _cooldownTicks = 0;
            if (!heroType.HasValue) { _hasTick = false; _tick = 0; }
        }
        HeroType = heroType;
        _playerId = playerId;
        _team = team;
        Visible = heroType.HasValue;
        RefreshQ();
    }

    // 모델 등록·선택 상태와 무관하게 UNIT의 소유자/타입/진영으로 내 영웅 ID를 찾는다.
    public void HandleUnit(string[] parts)
    {
        if (!HeroType.HasValue || _playerId == 0 || parts.Length != 7 || parts[0] != "UNIT" ||
            !uint.TryParse(parts[1], out uint type) || !uint.TryParse(parts[2], out uint id) || id == 0 ||
            !uint.TryParse(parts[3], out uint owner) || !uint.TryParse(parts[6], out uint team) || team == 0 ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) ||
            !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !float.IsFinite(z)) return;
        if (owner != _playerId || team != _team || type != HeroType.Value)
        {
            if (HeroUnitId == id) ClearVitals();
            return;
        }
        if (HeroUnitId == id) return;
        ClearVitals();
        HeroUnitId = id;
        RefreshQ();
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (HeroUnitId == health.Id) ApplyVital(_health, _healthText, health.Current, health.Maximum);
    }

    public void HandleState(StateSnapshot state)
    {
        if (HeroUnitId != state.Id) return;
        _stunned = state.State.Activity == UnitActivity.Stun;
        RefreshQ();
    }

    public void HandleTick(string[] parts)
    {
        if (parts.Length != 2 || parts[0] != "TICK" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick) ||
            _hasTick && unchecked((int)(tick - _tick)) <= 0) return;
        _tick = tick;
        _hasTick = true;
        if (_cooldownTicks == 0) _cooldownTicks = RemainingTicks;
        RefreshQ();
    }

    public void ApplyCooldown(SkillCooldownSnapshot cooldown)
    {
        if (HeroType != UnitCatalog.HeroTest || cooldown.Slot != 0 ||
            _readyTick.HasValue && unchecked((int)(cooldown.ReadyTick - _readyTick.Value)) <= 0) return;
        _readyTick = cooldown.ReadyTick;
        _cooldownTicks = RemainingTicks;
        RefreshQ();
    }

    public bool TryHandleShortcut(InputEvent input)
    {
        if (HeroType != UnitCatalog.HeroTest || input is not InputEventKey { Pressed: true } key ||
            (key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode) != Key.Q ||
            key.CtrlPressed || key.ShiftPressed || key.AltPressed || key.MetaPressed) return false;
        if (!key.Echo) RequestQ();
        return true;
    }

    private void RequestQ() { if (CanUseQ) TargetingRequested?.Invoke(0); }

    public bool IsEnemyTarget(Unit unit) => GodotObject.IsInstanceValid(unit) && unit.IsInsideTree() &&
        unit.IsVisibleInTree() && !unit.IsQueuedForDeletion() && !unit.IsDying && unit.Team != _team;

    public void SetTargeting(bool active) { _targeting = active && CanUseQ; RefreshQ(); }

    private void RefreshQ()
    {
        if (_qButton == null) return;
        bool available = CanUseQ;
        if (!available) _targeting = false;
        _qButton.Disabled = !available;
        _qButton.AddThemeStyleboxOverride("normal", _targeting ? _qTargeting : _qNormal);
        _qIcon.Visible = HeroType == UnitCatalog.HeroTest;
        _qIcon.Modulate = available ? Colors.White : new Color(.55f, .55f, .55f);
        bool cooling = _readyTick.HasValue && (!_hasTick || RemainingTicks > 0);
        _cooldownCover.Visible = cooling;
        _cooldownCover.AnchorTop = cooling && _hasTick ? 1 - Mathf.Clamp(RemainingTicks / (float)Math.Max(1, _cooldownTicks), 0, 1) : 0;
        _cooldownText.Text = cooling ? _hasTick ? Math.Ceiling(QRemainingSeconds).ToString(CultureInfo.InvariantCulture) : "—" : "";
        if (_lastAvailable == available) return;
        _lastAvailable = available;
        AvailabilityChanged?.Invoke();
    }

    // 마나 메시지는 서버에 아직 없다. 메시지가 정해지면 검증된 내 영웅 수치를 이곳에 연결한다.
    public void ApplyMana(uint unitId, float current, float maximum)
    {
        if (HeroUnitId == unitId) ApplyVital(_mana, _manaText, current, maximum);
    }

    public void HandleRemove(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint id) && HeroUnitId == id) ClearVitals();
    }

    private void ClearVitals()
    {
        HeroUnitId = null;
        _stunned = false;
        _targeting = false;
        RefreshQ();
        if (_health == null) return;
        _health.Value = _mana.Value = 0;
        _healthText.Text = _manaText.Text = "— / —";
    }

    private (ProgressBar, Label) CreateVital(VBoxContainer parent, string name, string color)
    {
        var row = new Control { Name = name, CustomMinimumSize = new Vector2(0, 16), MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(row);
        var bar = new ProgressBar { Name = "Bar", ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
        var track = Style("101c26", color, 3);
        track.BgColor = new Color(color).Darkened(0.82f);
        track.BorderColor = new Color(color).Darkened(0.6f);
        bar.AddThemeStyleboxOverride("background", track);
        bar.AddThemeStyleboxOverride("fill", Style(color, color, 3));
        row.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var label = new Label
        {
            Name = "Value", Text = "— / —", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("f2f4ee"));
        label.AddThemeColorOverride("font_outline_color", new Color("101820"));
        label.AddThemeConstantOverride("outline_size", 3);
        row.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return (bar, label);
    }

    private static void ApplyVital(ProgressBar bar, Label label, float current, float maximum)
    {
        if (bar == null || !float.IsFinite(current) || !float.IsFinite(maximum) || current < 0 || maximum < 0 ||
            (maximum == 0 && current != 0)) return;
        current = Mathf.Min(current, maximum);
        bar.Value = maximum > 0 ? 100.0 * current / maximum : 0;
        label.Text = current.ToString("0.#", CultureInfo.InvariantCulture) + " / " + maximum.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private StyleBoxFlat Style(string background, string border, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
        };
        _styles.Add(style);
        return style;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}

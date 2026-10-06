using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// 서버의 ABILITY 정의와 COOLDOWN/CONTROL 상태로 표시한다. 특정 영웅을 알지 않는다.
public partial class HeroSkillPanel : PanelContainer
{
    public uint? HeroType { get; private set; }
    public uint? HeroUnitId { get; private set; }
    public event Action<int> TargetingRequested;
    public event Action AvailabilityChanged;
    private readonly Dictionary<(uint, int), SkillDefinitionSnapshot> _definitions = new();
    private readonly Dictionary<int, uint> _readyTicks = new();
    private readonly Dictionary<int, int> _cooldownLengths = new();
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly List<SlotView> _slots = new();
    private uint _playerId, _team, _tick;
    private bool _hasTick;
    private int? _targeting;
    private ControlRestrictions _restrictions;
    private UnitActivity _activity;
    private ProgressBar _health, _mana;
    private Label _healthText, _manaText;
    private Label _status;
    private uint? _respawnTick;
    private bool _dead;
    public bool IsRespawning => _dead || _respawnTick.HasValue;
    public string StatusText => _status?.Text ?? "";
    private ColorRect _shieldFill;
    private Control _passive;
    private float _currentHP, _maxHP, _shield;
    private WolfEffectSnapshot _wolfEffects;
    private GolemEffectSnapshot _golemEffects;

    private sealed class SlotView
    {
        public Button Button;
        public Label Cooldown;
        public TextureRect Icon;
        public ColorRect Cover;
        public StyleBoxFlat Normal, Targeting;
        public string IconId;
    }

    public SkillDefinitionSnapshot? Definition(int slot) => HeroType.HasValue &&
        _definitions.TryGetValue((HeroType.Value, slot), out var definition) ? definition : null;

    public bool CanUse(int slot) => Definition(slot) is SkillDefinitionSnapshot definition && HeroUnitId.HasValue &&
        (_restrictions & (ControlRestrictions.Stun | ControlRestrictions.Silence)) == 0 &&
        (!definition.MovesCaster || (_restrictions & ControlRestrictions.Root) == 0) &&
        _activity is not (UnitActivity.Stun or UnitActivity.Dash) &&
        (!_readyTicks.ContainsKey(slot) || _hasTick && RemainingTicks(slot) == 0);

    private int RemainingTicks(int slot) => _hasTick && _readyTicks.TryGetValue(slot, out uint ready)
        ? Math.Max(0, unchecked((int)(ready - _tick))) : 0;
    public double RemainingSeconds(int slot) => RemainingTicks(slot) / (double)InterpolationClock.TickRate;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        var frame = Style("18232ef5", "65776a", 6);
        frame.ContentMarginLeft = frame.ContentMarginRight = 12;
        frame.ContentMarginTop = frame.ContentMarginBottom = 10;
        AddThemeStyleboxOverride("panel", frame);
        var content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);
        var vitals = new VBoxContainer { Name = "Vitals", MouseFilter = MouseFilterEnum.Ignore };
        vitals.AddThemeConstantOverride("separation", 4);
        content.AddChild(vitals);
        (_health, _healthText) = CreateVital(vitals, "Health", "64cf79");
        _shieldFill = HealthDisplay.CreateShieldFill(_health);
        (_mana, _manaText) = CreateVital(vitals, "Mana", "539fe0");
        _mana.GetParent<Control>().Hide(); // 마나를 실제로 받기 전에는 빈 수치를 표시하지 않는다.
        _status = new Label { Name = "Status", HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore };
        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", new Color("bfcec5"));
        vitals.AddChild(_status);
        var row = new HBoxContainer { Name = "Slots", MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        content.AddChild(row);
        var passive = new PanelContainer { Name = "Passive", CustomMinimumSize = new Vector2(64, 64), MouseFilter = MouseFilterEnum.Stop };
        passive.AddThemeStyleboxOverride("panel", Style("242825", "807653", 4));
        var passiveLabel = new Label { Name = "Key", Text = "패시브", MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        passiveLabel.AddThemeFontSizeOverride("font_size", 12);
        passive.AddChild(passiveLabel);
        row.AddChild(passive);
        _passive = passive;
        for (int i = 0; i < 5; i++) CreateSlot(row, i);
        Refresh();
    }

    private void CreateSlot(HBoxContainer row, int slot)
    {
        string key = slot < 4 ? new[] { "Q", "W", "E", "R" }[slot] : "5";
        var view = new SlotView
        {
            Button = new Button { Name = key, FocusMode = FocusModeEnum.None, MouseForcePassScrollEvents = false,
                CustomMinimumSize = new Vector2(72, 72), SizeFlagsHorizontal = SizeFlags.ExpandFill },
            Normal = Style("27332d", "a48b52", 4), Targeting = Style("394333", "efd28c", 4)
        };
        view.Button.AddThemeStyleboxOverride("normal", view.Normal);
        view.Button.AddThemeStyleboxOverride("hover", view.Targeting);
        view.Button.AddThemeStyleboxOverride("pressed", view.Targeting);
        view.Button.AddThemeStyleboxOverride("disabled", Style("101c26", "536b69", 4));
        view.Button.Pressed += () => Request(slot);
        row.AddChild(view.Button);
        view.Icon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        view.Button.AddChild(view.Icon);
        view.Icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        view.Icon.OffsetLeft = view.Icon.OffsetTop = 8;
        view.Icon.OffsetRight = view.Icon.OffsetBottom = -8;
        view.Cover = new ColorRect { Color = new Color(0, 0, 0, .6f), MouseFilter = MouseFilterEnum.Ignore };
        view.Button.AddChild(view.Cover);
        view.Cover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        view.Cooldown = new Label { Name = "Cooldown", HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        view.Cooldown.AddThemeFontSizeOverride("font_size", 23);
        view.Button.AddChild(view.Cooldown);
        view.Cooldown.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var label = new Label { Name = "Key", Text = key, MouseFilter = MouseFilterEnum.Ignore };
        view.Button.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        label.OffsetLeft = 8; label.OffsetTop = -23;
        _slots.Add(view);
    }

    public void HandleDefinition(SkillDefinitionSnapshot definition)
    {
        _definitions[(definition.UnitType, definition.Slot)] = definition;
        Refresh();
    }

    public void SetHero(uint? heroType, uint playerId = 0, uint team = 0)
    {
        if (HeroType != heroType || _playerId != playerId || _team != team || !heroType.HasValue)
        {
            _dead = false; _respawnTick = null;
            ClearVitals(); _readyTicks.Clear(); _cooldownLengths.Clear();
            if (!heroType.HasValue) { _definitions.Clear(); _hasTick = false; _tick = 0; }
        }
        HeroType = heroType; _playerId = playerId; _team = team;
        Visible = heroType.HasValue;
        Refresh();
    }

    public void HandleUnit(string[] parts)
    {
        if (!HeroType.HasValue || _playerId == 0 || parts.Length != 7 ||
            !uint.TryParse(parts[1], out uint type) || !uint.TryParse(parts[2], out uint id) || id == 0 ||
            !uint.TryParse(parts[3], out uint owner) || !uint.TryParse(parts[6], out uint team) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) ||
            !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !float.IsFinite(z)) return;
        if (owner != _playerId || team != _team || type != HeroType.Value)
        {
            if (HeroUnitId == id) ClearVitals();
            return;
        }
        if (HeroUnitId == id) return;
        ClearVitals(); HeroUnitId = id; _dead = false; _respawnTick = null; Refresh();
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (HeroUnitId != health.Id) return;
        _currentHP = Mathf.Min(health.Current, health.Maximum);
        _maxHP = health.Maximum;
        RefreshHealth();
    }
    public void HandleShield(ShieldSnapshot shield)
    {
        if (HeroUnitId != shield.Id) return;
        _shield = shield.Amount;
        RefreshHealth();
    }
    private void RefreshHealth()
    {
        if (_health == null) return;
        HealthDisplay.Apply(_health, _shieldFill, _currentHP, _maxHP, _shield);
        _healthText.Text = HealthDisplay.Text(_currentHP, _maxHP, _shield);
    }
    public void HandleState(StateSnapshot state)
    { if (HeroUnitId == state.Id) { _activity = state.State.Activity; Refresh(); } }
    public void HandleControl(ControlSnapshot control)
    { if (HeroUnitId == control.Id) { _restrictions = control.Flags; Refresh(); } }
    public void HandleTick(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick) ||
            _hasTick && unchecked((int)(tick - _tick)) <= 0) return;
        _tick = tick; _hasTick = true;
        foreach (int slot in _readyTicks.Keys)
            if (!_cooldownLengths.ContainsKey(slot)) _cooldownLengths[slot] = RemainingTicks(slot);
        Refresh();
    }
    public void ApplyCooldown(SkillCooldownSnapshot cooldown)
    {
        if (cooldown.CasterId != HeroUnitId || Definition(cooldown.Slot) == null ||
            _readyTicks.TryGetValue(cooldown.Slot, out uint previous) && unchecked((int)(cooldown.ReadyTick - previous)) <= 0) return;
        _readyTicks[cooldown.Slot] = cooldown.ReadyTick;
        _cooldownLengths[cooldown.Slot] = RemainingTicks(cooldown.Slot);
        Refresh();
    }

    public bool TryHandleShortcut(InputEvent input)
    {
        if (!HeroType.HasValue || input is not InputEventKey { Pressed: true } key ||
            key.CtrlPressed || key.ShiftPressed || key.AltPressed || key.MetaPressed) return false;
        int slot = (key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode) switch
        { Key.Q => 0, Key.W => 1, Key.E => 2, Key.R => 3, _ => -1 };
        if (slot < 0 || Definition(slot) == null) return false;
        if (!key.Echo) Request(slot);
        return true;
    }
    private void Request(int slot) { if (CanUse(slot)) TargetingRequested?.Invoke(slot); }
    public bool AcceptsTarget(int slot, Node3D target)
    {
        if (Definition(slot) is not SkillDefinitionSnapshot definition || !GodotObject.IsInstanceValid(target) ||
            !target.IsInsideTree() || target.IsQueuedForDeletion()) return false;
        uint? team = target switch { Unit u when !u.IsDying => u.Team, Building b => b.SideId, _ => null };
        return team.HasValue && (definition.Target switch
        { SkillTargetMode.Enemy => team != _team, SkillTargetMode.Ally => team == _team, _ => false });
    }
    public void SetTargeting(int? slot) { _targeting = slot; Refresh(); }
    public void HandleWolfEffects(WolfEffectSnapshot effects)
    {
        if (HeroType != UnitCatalog.HeroTest || HeroUnitId != effects.UnitId) return;
        _wolfEffects = effects;
        RefreshStatus();
    }
    public void HandleGolemEffects(GolemEffectSnapshot effects)
    {
        if (HeroType != UnitCatalog.HeroGolem || HeroUnitId != effects.UnitId) return;
        _golemEffects = effects;
        Refresh();
    }
    public void HandleRespawn(string[] parts)
    {
        if (!HeroType.HasValue || parts.Length != 2 || !uint.TryParse(parts[1], NumberStyles.None,
            CultureInfo.InvariantCulture, out uint tick)) return;
        _respawnTick = tick;
        _dead = true;
        Refresh();
    }

    private void RefreshStatus()
    {
        if (_status == null) return;
        if (_respawnTick is uint ready)
        {
            int remaining = Math.Max(0, unchecked((int)(ready - _tick)));
            _status.Text = !_hasTick ? "전사 · 본진 부활 대기 중" : remaining > 0
                ? $"전사 · {Math.Ceiling(remaining / (double)InterpolationClock.TickRate):0}초 후 본진에서 부활"
                : "전사 · 본진의 부활 위치를 확보하는 중";
        }
        else if (!HeroUnitId.HasValue) _status.Text = _dead ? "전사 · 부활 대기 중" : "내 영웅을 기다리는 중…";
        else if (_targeting is int slot)
            _status.Text = $"{(Definition(slot)?.Target == SkillTargetMode.Ally ? "아군" : "적")}을 클릭해 기술 사용 · Esc 취소";
        else if ((_restrictions & ControlRestrictions.Stun) != 0) _status.Text = "기절 · 잠시 행동할 수 없습니다";
        else if ((_restrictions & ControlRestrictions.Silence) != 0) _status.Text = "침묵 · 이동과 일반 공격은 가능합니다";
        else if (_golemEffects.EmpowerReady) _status.Text = "R · 다음 Q/W/E 1회 강화 대기";
        else if (_wolfEffects.DrainReady || _wolfEffects.AuraUntil != 0 && unchecked((int)(_wolfEffects.AuraUntil - _tick)) > 0)
        {
            string drain = _wolfEffects.DrainReady ? "W · 흡혈 50% 대기" : "";
            int ticks = Math.Max(0, unchecked((int)(_wolfEffects.AuraUntil - _tick)));
            string aura = _wolfEffects.AuraUntil != 0 && ticks > 0
                ? $"E · 지속 피해 {Math.Ceiling(ticks / (double)InterpolationClock.TickRate):0}초" : "";
            _status.Text = drain + (drain != "" && aura != "" ? " / " : "") + aura;
        }
        else if (_maxHP > 0 && _currentHP / _maxHP < .25f) _status.Text = "체력 위험 · 뒤로 물러나 회복하세요";
        else _status.Text = "내 영웅 · 스킬에 마우스를 올리면 설명";
        _status.Modulate = IsRespawning || _maxHP > 0 && _currentHP / _maxHP < .25f
            ? new Color("ffc393") : Colors.White;
    }

    private void Refresh()
    {
        if (_passive != null) _passive.TooltipText = SkillDescriptions.Passive(HeroType);
        if (_targeting.HasValue && !CanUse(_targeting.Value)) _targeting = null;
        for (int slot = 0; slot < _slots.Count; slot++)
        {
            var view = _slots[slot]; var definition = Definition(slot);
            bool available = CanUse(slot);
            view.Button.Visible = slot < 4 || definition.HasValue;
            view.Button.Disabled = !available;
            bool empowered = _golemEffects.EmpowerReady && slot < 3;
            view.Button.TooltipText = SkillDescriptions.Active(HeroType, slot, definition?.Id, empowered);
            view.Button.AddThemeStyleboxOverride("normal", _targeting == slot || empowered ? view.Targeting : view.Normal);
            string iconId = definition?.Id;
            if (view.IconId != iconId)
            {
                view.IconId = iconId;
                string iconPath = $"res://ui/icons/{iconId}.svg";
                view.Icon.Texture = iconId != null && ResourceLoader.Exists(iconPath) ? GD.Load<Texture2D>(iconPath) : null;
            }
            view.Icon.Modulate = available ? Colors.White : new Color(.55f, .55f, .55f);
            bool cooling = _readyTicks.ContainsKey(slot) && (!_hasTick || RemainingTicks(slot) > 0);
            view.Cover.Visible = cooling;
            view.Cover.AnchorTop = cooling && _hasTick ? 1 - Mathf.Clamp(RemainingTicks(slot) / (float)Math.Max(1, _cooldownLengths.GetValueOrDefault(slot)), 0, 1) : 0;
            view.Cooldown.Text = cooling ? _hasTick ? Math.Ceiling(RemainingSeconds(slot)).ToString(CultureInfo.InvariantCulture) : "—" : "";
        }
        RefreshStatus();
        AvailabilityChanged?.Invoke();
    }
    public void ApplyMana(uint id, float current, float maximum)
    {
        if (HeroUnitId != id || !float.IsFinite(current) || !float.IsFinite(maximum) || current < 0 || maximum <= 0) return;
        ApplyVital(_mana, _manaText, current, maximum);
        _mana.GetParent<Control>().Show();
    }
    public void HandleRemove(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint id) && HeroUnitId == id)
        { _dead = parts[0] == "REMOVE"; ClearVitals(); }
    }
    private void ClearVitals()
    {
        _wolfEffects = default; _golemEffects = default;
        HeroUnitId = null; _restrictions = ControlRestrictions.None; _activity = UnitActivity.Idle; _targeting = null;
        Refresh();
        _currentHP = _maxHP = _shield = 0;
        if (_health == null) return;
        _shieldFill.Hide();
        _health.Value = _mana.Value = 0; _healthText.Text = _manaText.Text = "— / —";
        _mana.GetParent<Control>().Hide();
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

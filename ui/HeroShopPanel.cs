using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Purchases update only after HERO_ITEMS. The catalog and prices come from the server.
public partial class HeroShopPanel : Control
{
    public HeroInventorySnapshot Inventory { get; private set; }
    public IReadOnlyDictionary<uint, HeroItemDefinition> Catalog => _catalog;
    public event Action<string> CommandRequested;
    public event Action RecallRequested;
    public bool IsOpen => _shop?.Visible == true;
    public string RecallText => _recall?.Text ?? "";
    private readonly Dictionary<uint,HeroItemDefinition> _catalog=new();
    private readonly Dictionary<uint,Button> _buyButtons=new();
    private readonly List<Button> _slots=new();
    private PanelContainer _shop;
    private VBoxContainer _list;
    private Button _toggle,_recall;
    private Label _gold,_hint,_notice;
    private bool _heroRole,_canRecall;
    private RecallSnapshot _channel;
    private uint _tick;
    private double _noticeUntil;

    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var toolbar=new PanelContainer { Name="Equipment" };
        AddChild(toolbar); toolbar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        toolbar.OffsetLeft=-352;toolbar.OffsetRight=-16;toolbar.OffsetTop=70;
        toolbar.AddThemeStyleboxOverride("panel",Frame());
        var content=new VBoxContainer();content.AddThemeConstantOverride("separation",7);toolbar.AddChild(content);
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",8);content.AddChild(row);
        _gold=new Label { Name="Gold",Text=Lang.T("골드 —"),SizeFlagsHorizontal=SizeFlags.ExpandFill };
        _gold.AddThemeColorOverride("font_color",new Color("f3ce86"));row.AddChild(_gold);
        _toggle=Button(Lang.T("상점 [I]"),()=>Toggle());row.AddChild(_toggle);
        _recall=Button(Lang.T("귀환 [B]"),()=> { if (_canRecall) RecallRequested?.Invoke(); });row.AddChild(_recall);
        _recall.TooltipText=Lang.T("정신집중을 마치면 부활 구역으로 귀환합니다. 이동·적 피격 등으로 취소됩니다.");
        var equipment=new HBoxContainer { Name="Slots" };equipment.AddThemeConstantOverride("separation",6);content.AddChild(equipment);
        for (int i=0;i<4;i++)
        {
            int slot=i;
            var button=Button(Lang.T("빈 칸"),()=>RequestSell(slot));button.Name=$"Slot{i}";
            button.CustomMinimumSize=new Vector2(72,42);button.SizeFlagsHorizontal=SizeFlags.ExpandFill;
            button.AddThemeFontSizeOverride("font_size",12);
            equipment.AddChild(button);_slots.Add(button);
        }
        _notice=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart,Visible=false };
        _notice.AddThemeFontSizeOverride("font_size",12);content.AddChild(_notice);
        _shop=new PanelContainer { Name="Shop",Visible=false };
        AddChild(_shop);_shop.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        _shop.OffsetLeft=-352;_shop.OffsetRight=-16;_shop.OffsetTop=190;_shop.OffsetBottom=-190;
        _shop.AddThemeStyleboxOverride("panel",Frame());
        var body=new VBoxContainer();body.AddThemeConstantOverride("separation",8);_shop.AddChild(body);
        var title=new HBoxContainer();body.AddChild(title);
        title.AddChild(new Label { Text=Lang.T("전장 보급소"),SizeFlagsHorizontal=SizeFlags.ExpandFill });
        title.AddChild(Button(Lang.T("닫기"),()=>_shop.Hide()));
        _hint=new Label { AutowrapMode=TextServer.AutowrapMode.WordSmart };
        _hint.AddThemeFontSizeOverride("font_size",12);body.AddChild(_hint);
        var scroll=new ScrollContainer { HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,SizeFlagsVertical=SizeFlags.ExpandFill };
        body.AddChild(scroll);
        _list=new VBoxContainer { SizeFlagsHorizontal=SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation",6);scroll.AddChild(_list);
        RebuildCatalog();Refresh();
    }
    private static StyleBoxFlat Frame() => new() { BgColor=new Color("18232ef5"),BorderColor=new Color("65776a"),
        BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,
        CornerRadiusTopLeft=6,CornerRadiusTopRight=6,CornerRadiusBottomLeft=6,CornerRadiusBottomRight=6,
        ContentMarginLeft=10,ContentMarginRight=10,ContentMarginTop=8,ContentMarginBottom=8 };
    private static Button Button(string text,Action pressed)
    {
        var b=new Button { Text=text,FocusMode=FocusModeEnum.None,MouseForcePassScrollEvents=false };
        b.Pressed+=pressed;return b;
    }
    public void SetRole(bool hero)
    {
        _heroRole=hero;Inventory=null;_channel=default;_canRecall=false;_noticeUntil=0;_tick=0;
        _catalog.Clear();RebuildCatalog();_shop?.Hide();Refresh();
    }
    public void ApplyCatalog(HeroItemDefinition[] items)
    {
        _catalog.Clear();foreach (var item in items) _catalog[item.Id]=item;
        RebuildCatalog();Refresh();
    }
    public void ApplyInventory(HeroInventorySnapshot snapshot) { if (_heroRole) { Inventory=snapshot;Refresh(); } }
    public void SetHeroAvailability(bool canRecall)
    { bool next=_heroRole && canRecall;if (_canRecall==next) return;_canRecall=next;Refresh(); }
    public void SetRecall(RecallSnapshot channel) { _channel=channel;Refresh(); }
    public void SetTick(uint tick) { _tick=tick;Refresh(); }
    public void Notice(string text)
    { if (!_heroRole || _notice==null) return;_notice.Text=text;_noticeUntil=Time.GetTicksMsec()/1000.0+5;Refresh(); }
    public override void _Process(double delta)
    { if (_notice?.Visible==true && Time.GetTicksMsec()/1000.0>=_noticeUntil) _notice.Hide(); }
    public void Toggle() { if (_heroRole && _shop!=null) _shop.Visible=!_shop.Visible; }
    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (!_heroRole || input is not InputEventKey { Pressed:true,Echo:false } key ||
            key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed ||
            GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
        Key code=key.PhysicalKeycode!=Key.None ? key.PhysicalKeycode : key.Keycode;
        if (code==Key.I) Toggle();
        else if (code==Key.B) { if (_canRecall) RecallRequested?.Invoke(); }
        else if (code==Key.Escape && IsOpen) _shop.Hide();
        else return;
        GetViewport().SetInputAsHandled();
    }
    public string BuyBlockReason(uint id)
    {
        if (!_heroRole || Inventory==null || !_catalog.TryGetValue(id,out var item)) return Lang.T("정보 대기 중");
        var s=Inventory;
        if (s.TeamTier < item.Tier) return Lang.F("아군 HQ {0}티어 필요",item.Tier);
        if (item.From!=0)
        {
            int slot=Array.IndexOf(s.Items,item.From);
            if (slot<0) return Lang.F("{0} 필요",_catalog.GetValueOrDefault(item.From)?.DisplayName ?? Lang.T("이전 단계 장비"));
            if (s.Pending[slot]) return Lang.T("이전 단계 장비 수령 필요");
        }
        else
        {
            if (s.Items.Any(owned=>owned!=0 && ItemFamily(owned)==ItemFamily(id))) return Lang.T("보유한 계열");
            if (!s.Items.Contains(0u)) return Lang.T("장비 칸 부족");
        }
        return s.Gold<item.Cost ? Lang.T("골드 부족") : null;
    }
    public void RequestBuy(uint id)
    { if (BuyBlockReason(id)==null) CommandRequested?.Invoke(Protocol.BuildBuyItem(id)); }
    public void RequestSell(int slot)
    {
        if (_heroRole && Inventory is { CanShop:true } s && slot>=0 && slot<4 && s.Items[slot]!=0 && !s.Pending[slot])
            CommandRequested?.Invoke(Protocol.BuildSellItem(slot));
    }
    private void RebuildCatalog()
    {
        if (_list==null) return;
        foreach (Node child in _list.GetChildren()) { _list.RemoveChild(child);child.QueueFree(); }
        _buyButtons.Clear();
        int lastTier=0;
        foreach (var item in _catalog.Values.OrderBy(i=>i.Tier).ThenBy(i=>i.Id))
        {
            if (item.Tier!=lastTier)
            {
                lastTier=item.Tier;
                _list.AddChild(new Label { Text=Lang.F("{0}티어",item.Tier)+" · "+(item.Tier==1 ? Lang.T("시작부터 구매 가능") : Lang.F("아군 HQ {0}티어 필요",item.Tier)) });
            }
            uint id=item.Id;
            var b=Button("",()=>RequestBuy(id));b.Name=$"Item{id}";b.CustomMinimumSize=new Vector2(0,58);
            b.Alignment=HorizontalAlignment.Left;b.AddThemeFontSizeOverride("font_size",13);
            b.TooltipText=Lang.F("{0}티어",item.Tier)+$" · {item.EffectText}\n"+(item.From==0 ? Lang.T("기본 장비") : Lang.T("이전 단계 장비를 교체 · 표시 가격은 추가 비용"));
            _list.AddChild(b);_buyButtons[id]=b;
        }
    }
    private void Refresh()
    {
        Visible=_heroRole;
        if (_gold==null) return;
        _gold.Text=Inventory==null ? Lang.T("골드 —") : $"{Inventory.Gold:N0} G";
        _toggle.Disabled=Inventory==null;
        _recall.Disabled=!_canRecall || _channel.Active;
        _recall.Text=_channel.Active ? _channel.Remaining(_tick)>0 ? Lang.F("귀환 {0:0.0}초",_channel.Remaining(_tick)) : Lang.T("귀환 확인 중") : Lang.T("귀환 [B]");
        _hint.Text=Lang.F("아군 HQ {0}티어",Inventory?.TeamTier ?? 1)+"\n" + (Inventory?.CanShop==true ? Lang.T("부활 구역 · 즉시 장착 / 장비를 눌러 판매") :
            Lang.T("원격 구매 · 본진에 배송 상자가 생성됩니다.\n영웅으로 줍거나 아군 일꾼으로 배달하세요."));
        _notice.Visible=_noticeUntil>Time.GetTicksMsec()/1000.0;
        for (int i=0;i<_slots.Count;i++)
        {
            uint id=Inventory?.Items[i] ?? 0;
            var d=_catalog.GetValueOrDefault(id);
            bool pending=Inventory?.Pending[i]==true;
            _slots[i].Text=id==0 ? Lang.T("빈 칸") : ShortName(d?.DisplayName ?? Lang.T("장비"))+"\n"+(pending ? Lang.T("배송 / 장착 중") : Lang.T("장착됨"));
            _slots[i].Disabled=id==0 || pending || Inventory?.CanShop!=true;
            int value=ItemValue(id);
            _slots[i].TooltipText=d==null ? Lang.T("아이템 슬롯") : $"{d.DisplayName}\n{d.EffectText}\n"+(pending ? Lang.T("수령 후 장착됩니다.") : Lang.F("부활 구역에서 클릭하여 {0} G에 판매",value/2));
        }
        foreach (var (id,b) in _buyButtons)
        {
            var d=_catalog[id];string reason=BuyBlockReason(id);b.Disabled=reason!=null;
            b.Text=$"{d.DisplayName}   {d.Cost:N0} G\n{reason ?? d.EffectText}";
        }
    }
    private uint ItemFamily(uint id)
    {
        for (int depth=0;depth<3 && _catalog.TryGetValue(id,out var item) && item.From!=0;depth++) id=item.From;
        return id;
    }
    private int ItemValue(uint id)
    {
        int value=0;
        for (int depth=0;depth<3 && _catalog.TryGetValue(id,out var item);depth++) { value+=item.Cost;id=item.From; }
        return value;
    }
    private static string ShortName(string name) => name.Contains(' ') ? name[(name.LastIndexOf(' ')+1)..] : name;
}

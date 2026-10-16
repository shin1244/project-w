using Godot;
using System.Collections.Generic;

// A presentation rig, not a simulation: deployment comes from the server and walking from rendered distance.
internal sealed class SteamArtilleryPose
{
    private readonly Node3D _aim, _hull, _mobileTurret, _mainTurret, _mobileBarrel, _mainBarrel;
    private readonly Node3D _leftGear, _rightGear, _traversePinion;
    private readonly Vector3 _hullRest, _mobileBarrelRest, _mainBarrelRest, _pinionRest;
    private float _mobileAimYaw, _mainAimYaw;
    private const float HullDrop = .55f;
    // Siege recoil slides the whole gun back along its bore; the trunnion gears turn by that travel over their pitch radius.
    private const float SiegeRecoilDepth = .7f, GearPitchRadius = .3f;
    private readonly List<Leg> _legs = new();
    public Node3D MobileMuzzle { get; }
    public Node3D SiegeMuzzle { get; }
    private static readonly (int From, int To)[] Links = { (0, 1), (0, 2), (1, 3), (2, 3), (3, 4), (2, 4), (4, 5), (2, 5) };
    private static readonly string[] LinkNames = { "AB", "AC", "BD", "CD", "DE", "CE", "EF", "CF" };

    private sealed class Leg
    {
        public Node3D Root, Foot;
        public readonly Node3D[] Links = new Node3D[8], Joints = new Node3D[6];
        public readonly Vector3[] Points = new Vector3[6];
        public Vector3 Rest;
        public int Side, Index;
    }

    public SteamArtilleryPose(Node3D rig)
    {
        _aim = rig.GetNode<Node3D>("Aim");
        _hull = rig.GetNode<Node3D>("Hull");
        _mobileTurret = _hull.GetNode<Node3D>("MobileTurret");
        _mainTurret = _hull.GetNode<Node3D>("MainTurret");
        _mobileBarrel = _mobileTurret.GetNode<Node3D>("Barrel");
        _mainBarrel = _mainTurret.GetNode<Node3D>("Barrel");
        var mainTube = _mainBarrel.GetNode<Node3D>("Tube");
        MobileMuzzle = _mobileBarrel.GetNode<Node3D>("Tube/Muzzle");
        SiegeMuzzle = mainTube.GetNode<Node3D>("Muzzle");
        _leftGear = _mainTurret.GetNode<Node3D>("LeftGear");
        _rightGear = _mainTurret.GetNode<Node3D>("RightGear");
        _traversePinion = _hull.GetNode<Node3D>("TraversePinion");
        _hullRest = _hull.Position; _mobileBarrelRest = _mobileBarrel.Position;
        _mainBarrelRest = _mainBarrel.Position;
        _pinionRest = _traversePinion.Rotation;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 4; i++)
            {
                var root = rig.GetNode<Node3D>($"Legs/{(side < 0 ? "Left" : "Right")}{i}");
                var leg = new Leg { Root = root, Foot = root.GetNode<Node3D>("Foot"), Rest = root.Position, Side = side, Index = i };
                for (int link = 0; link < LinkNames.Length; link++) leg.Links[link] = root.GetNode<Node3D>("Link" + LinkNames[link]);
                for (int joint = 0; joint < 6; joint++) leg.Joints[joint] = root.GetNode<Node3D>("Joint" + (char)('A' + joint));
                _legs.Add(leg);
            }
    }

    private static float Ease(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp(value, 0, 1));

    public void Apply(float deployment, float gait, float walk, float recoil, bool siegeShot, float siegeTravel, float dt, bool returnAim,
        bool mobileAttacking, Vector3? target)
    {
        // Tuck the legs, then lower the hull while keeping the underside vents clear. Equal ticks reproduce the same pose.
        float fold = Ease(deployment / .72f), settle = Ease((deployment - .15f) / .85f);
        float gun = Ease((deployment - .18f) / .82f);
        float bob = Mathf.Sin(gait * 2) * .025f * walk * (1 - fold);
        _hull.Position = _hullRest + Vector3.Down * (HullDrop * settle) + Vector3.Up * bob;
        if (returnAim) _aim.Rotation = new Vector3(0, Mathf.LerpAngle(_aim.Rotation.Y, 0, Mathf.Min(1, dt * 8)), 0);
        if (target is Vector3 point)
        {
            // The guns have different pivots, offset from Unit/Aim. Aim each horizontal bore through the actual target.
            _mobileAimYaw = mobileAttacking ? YawTo(_mobileTurret, point, _mobileAimYaw) : 0;
            _mainAimYaw = YawTo(_mainTurret, point, _mainAimYaw);
        }
        else
        {
            _mobileAimYaw = 0;
            if (returnAim) _mainAimYaw = _aim.Rotation.Y;
        }
        _mobileTurret.Rotation = new Vector3(0, _mobileAimYaw, 0);
        float mainYaw = Mathf.LerpAngle(0, _mainAimYaw, gun);
        _mainTurret.Rotation = new Vector3(0, mainYaw, 0);
        // The forty-tooth carriage ring drives the ten-tooth pinion in the opposite direction.
        _traversePinion.Rotation = _pinionRest + Vector3.Up * (-mainYaw * 4);
        _mobileBarrel.Position = _mobileBarrelRest + Vector3.Back * (siegeShot ? 0 : recoil * .11f);
        _mainBarrel.Rotation = new Vector3(Mathf.Lerp(.10f, .32f, gun), 0, 0);
        float travel = siegeTravel * SiegeRecoilDepth;
        // The tube stays seated on the breech: sliding it forward on deployment left it floating in front of it.
        _mainBarrel.Position = _mainBarrelRest + _mainBarrel.Basis.Z * travel;
        _leftGear.Rotation = _rightGear.Rotation = new Vector3(gun * -.95f + travel / GearPitchRadius, 0, 0);
        foreach (Leg leg in _legs) ApplyLeg(leg, gait, fold, settle, bob);
    }

    private static float YawTo(Node3D turret, Vector3 target, float fallback)
    {
        Vector3 direction = turret.GetParent<Node3D>().ToLocal(target) - turret.Position;
        direction.Y = 0;
        return direction.LengthSquared() > .000001f ? Mathf.Atan2(-direction.X, -direction.Z) : fallback;
    }

    private static void ApplyLeg(Leg leg, float gait, float fold, float settle, float bob)
    {
        float phase = gait + (leg.Index % 2) * Mathf.Pi + (leg.Side < 0 ? 0 : Mathf.Pi);
        float stride = Mathf.Cos(phase) * .26f * (1 - fold);
        float lift = Mathf.Max(0, Mathf.Sin(phase)) * .22f * (1 - fold);
        float crank = Mathf.Sin(phase) * .085f * (1 - fold);
        int s = leg.Side;
        var p = leg.Points;
        p[0] = Vector3.Zero;
        p[1] = new Vector3(s * .13f, -.30f + crank, -.24f + stride * .38f);
        p[2] = new Vector3(s * .20f, -.15f, .28f);
        p[3] = new Vector3(s * .42f, -.64f + lift * .48f, -.06f + stride * .60f);
        p[4] = new Vector3(s * .55f, -.55f + lift * .66f, .28f + stride * .80f);
        p[5] = new Vector3(s * .70f, -1.23f + lift, -.03f + stride);

        leg.Root.Position = leg.Rest + Vector3.Down * (HullDrop * settle) + Vector3.Up * bob;
        // Opposing pairs fold toward the center of the hull; folded feet remain above ground throughout settling.
        leg.Root.Rotation = new Vector3((leg.Index < 2 ? -1 : 1) * fold * Mathf.DegToRad(84), 0, 0);
        float padHeight = (leg.Root.Transform * p[5]).Y;
        if (padHeight < .075f) leg.Root.Position += Vector3.Up * (.075f - padHeight);
        // As the carriage finishes settling, the last two links lower the pad into a grounded support.
        // Keep A..E tucked in place and preserve the pad's horizontal position beneath the folded linkage.
        float support = Ease((settle - .45f) / .55f);
        Vector3 supportPoint = leg.Root.Transform * p[5];
        supportPoint.Y = Mathf.Lerp(supportPoint.Y, .075f, support);
        p[5] = leg.Root.Transform.AffineInverse() * supportPoint;
        for (int i = 0; i < leg.Links.Length; i++)
        {
            Vector3 from = p[Links[i].From], to = p[Links[i].To], direction = to - from;
            Vector3 y = direction.Normalized();
            Vector3 x = y.Cross(Vector3.Forward).Normalized(), z = x.Cross(y).Normalized();
            leg.Links[i].Transform = new Transform3D(new Basis(x, direction, z), (from + to) * .5f);
        }
        for (int i = 0; i < leg.Joints.Length; i++) leg.Joints[i].Position = p[i];
        leg.Foot.Position = p[5];
        // The pad stays horizontal as the linkage tucks around it.
        leg.Foot.Rotation = -leg.Root.Rotation;
    }
}

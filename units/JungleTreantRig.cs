using Godot;

// 고목 수호자(강한 정글몹): 나무껍질 몸통, 가지 팔, 잎 무성한 머리, 호박색 눈. 공격은 두 팔로 내리친다.
public partial class JungleTreantRig : JungleMonsterRig
{
    private Node3D _body, _crown, _armLeft, _armRight, _legLeft, _legRight;

    protected override float StrideScale => 2.6f;
    protected override float StrikeSeconds => .7f;

    protected override void Build()
    {
        _body = Joint(this, "Body", new Vector3(0, 1.0f, 0));
        Part(_body, Cylinder(.52f, .7f, 2.1f, 7), "6b4a2e", new Vector3(0, 1.0f, 0));
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5 + .3f;
            Part(_body, Box(.08f, 1.5f + (i % 2) * .3f, .1f), "3e2817",
                new Vector3(Mathf.Sin(angle) * .62f, .95f, Mathf.Cos(angle) * .62f), new Vector3(0, angle, .04f));
        }
        Part(_body, Blob(.32f, .3f), "6c8f3e", new Vector3(-.42f, 1.75f, .1f));
        Part(_body, Blob(.26f, .24f), "7aa04a", new Vector3(.4f, 1.35f, -.38f));
        foreach (float side in new[] { -1f, 1f })
            Part(_body, Box(.16f, .1f, .06f), "ffb347", new Vector3(side * .2f, 1.62f, -.56f), default, 3f);
        Part(_body, Box(.36f, .08f, .06f), "1e140c", new Vector3(0, 1.32f, -.6f));

        _crown = Joint(_body, "Crown", new Vector3(0, 2.15f, 0));
        Part(_crown, Blob(1.1f, 1.2f), "4f7a35", new Vector3(0, .3f, .05f));
        Part(_crown, Blob(.75f, .85f), "6a9a3f", new Vector3(-.7f, .05f, -.2f));
        Part(_crown, Blob(.72f, .8f), "3f6a2e", new Vector3(.7f, .1f, .15f));
        Part(_crown, Blob(.48f, .55f), "7fae4c", new Vector3(.1f, .72f, -.15f));
        Part(_crown, Blob(.12f, .12f), "e0763f", new Vector3(-.35f, .6f, -.62f));
        Part(_crown, Blob(.1f, .1f), "e0763f", new Vector3(.42f, .38f, -.6f));

        _armLeft = Arm("ArmLeft", -1);
        _armRight = Arm("ArmRight", 1);
        _legLeft = Leg("LegLeft", -1);
        _legRight = Leg("LegRight", 1);
    }

    private Node3D Arm(string name, float side)
    {
        var arm = Joint(_body, name, new Vector3(side * .62f, 1.7f, 0));
        Part(arm, Cylinder(.14f, .22f, 1.3f), "4f331e", new Vector3(side * .28f, -.55f, 0), new Vector3(0, 0, side * .35f));
        var hand = Joint(arm, "Hand", new Vector3(side * .5f, -1.15f, 0));
        for (int i = -1; i <= 1; i++)
            Part(hand, Spike(.07f, .42f), "3e2817", new Vector3(i * .1f, -.15f, i * .05f), new Vector3(Mathf.Pi + i * .25f, 0, side * .2f));
        Part(arm, Blob(.22f, .2f), "6a9a3f", new Vector3(side * .1f, .1f, .05f));
        return arm;
    }

    private Node3D Leg(string name, float side)
    {
        var leg = Joint(this, name, new Vector3(side * .38f, 1.05f, 0));
        Part(leg, Cylinder(.3f, .36f, 1.0f), "4a301c", new Vector3(0, -.5f, 0));
        for (int i = -1; i <= 1; i++)
            Part(leg, Spike(.12f, .45f), "3e2817", new Vector3(i * .2f, -.98f, -.12f + Mathf.Abs(i) * .1f),
                new Vector3(-Mathf.Pi / 2 - .25f, i * .5f, 0));
        return leg;
    }

    protected override void Pose(float time, float stride, float walk, float strike)
    {
        float step = Mathf.Sin(stride) * walk;
        _legLeft.Rotation = new Vector3(step * .38f, 0, 0);
        _legRight.Rotation = new Vector3(-step * .38f, 0, 0);
        _body.Position = new Vector3(0, 1.0f + Mathf.Abs(Mathf.Sin(stride)) * .06f * walk, -strike * .15f);
        _body.Rotation = new Vector3(-strike * .2f, 0, step * .05f + Mathf.Sin(time * .9f) * .015f);
        // 두 팔을 머리 위에서 앞으로 내리친다. 쉬는 동안은 가지가 바람에 흔들린다.
        float swing = Mathf.Sin(time * 1.4f) * .06f;
        _armLeft.Rotation = new Vector3(strike * 1.4f - step * .3f + swing, 0, 0);
        _armRight.Rotation = new Vector3(strike * 1.4f + step * .3f - swing, 0, 0);
        _crown.Rotation = new Vector3(Mathf.Sin(time * .7f) * .04f, 0, Mathf.Sin(time * 1.1f) * .05f);
    }
}

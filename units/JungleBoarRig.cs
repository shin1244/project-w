using Godot;

// 가시 멧돼지(약한 정글몹): 낮고 넓은 갈색 몸, 하얀 엄니, 등을 따라 붉은 가시. 공격은 머리로 들이받는다.
public partial class JungleBoarRig : JungleMonsterRig
{
    private Node3D _body, _head, _tail;
    private readonly Node3D[] _legs = new Node3D[4];

    protected override float StrideScale => 5.2f;
    protected override float StrikeSeconds => .45f;

    protected override void Build()
    {
        _body = Joint(this, "Body", new Vector3(0, .78f, 0));
        Part(_body, Box(.98f, .74f, 1.5f), "7a4f2e", Vector3.Zero, new Vector3(.05f, 0, 0));
        Part(_body, Box(.8f, .26f, 1.2f), "a47a50", new Vector3(0, -.38f, .02f));
        Part(_body, Box(1.08f, .52f, .72f), "5a3820", new Vector3(0, .38f, -.38f), new Vector3(.12f, 0, 0));
        for (int i = 0; i < 6; i++)
        {
            float z = -.62f + i * .24f, height = .5f - Mathf.Abs(i - 1.5f) * .07f;
            Part(_body, Spike(.13f, height), i % 2 == 0 ? "c64a33" : "e0763f",
                new Vector3(i % 2 == 0 ? -.08f : .08f, .56f + height * .4f - i * .03f, z), new Vector3(.45f, 0, 0));
        }
        _head = Joint(_body, "Head", new Vector3(0, .1f, -.86f));
        Part(_head, Box(.72f, .6f, .58f), "6b4428", new Vector3(0, 0, -.12f));
        Part(_head, Box(.46f, .36f, .4f), "8a5d3b", new Vector3(0, -.1f, -.55f));
        Part(_head, Box(.32f, .2f, .06f), "c48a7a", new Vector3(0, -.08f, -.77f));
        foreach (float side in new[] { -1f, 1f })
        {
            Part(_head, Spike(.07f, .42f), "efe6cf", new Vector3(side * .26f, -.12f, -.62f), new Vector3(-.9f, 0, side * -.35f));
            Part(_head, Box(.1f, .08f, .04f), "f4c64e", new Vector3(side * .2f, .12f, -.42f), default, 2.5f);
            Part(_head, Spike(.1f, .26f), "5a3820", new Vector3(side * .3f, .36f, -.02f), new Vector3(0, 0, side * -.4f));
        }
        string[] legNames = { "FrontLeft", "FrontRight", "HindLeft", "HindRight" };
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -.34f : .34f, z = i < 2 ? -.5f : .48f;
            _legs[i] = Joint(this, legNames[i], new Vector3(x, .6f, z));
            Part(_legs[i], Box(.24f, .56f, .26f), "5e3b22", new Vector3(0, -.28f, 0));
            Part(_legs[i], Box(.26f, .12f, .3f), "2d2018", new Vector3(0, -.56f, -.02f));
        }
        _tail = Joint(_body, "Tail", new Vector3(0, .2f, .76f));
        Part(_tail, Cylinder(.03f, .05f, .38f, 4), "4a2e1a", new Vector3(0, -.14f, .08f), new Vector3(.5f, 0, 0));
    }

    protected override void Pose(float time, float stride, float walk, float strike)
    {
        for (int i = 0; i < 4; i++)
        {
            float phase = Mathf.Sin(stride + (i is 0 or 3 ? 0 : Mathf.Pi)) * walk;
            _legs[i].Rotation = new Vector3(phase * .55f, 0, 0);
        }
        // 들이받기: 머리를 숙이며 몸을 앞으로 내민다.
        _body.Position = new Vector3(0, .78f + Mathf.Sin(time * 2.6f) * .015f + Mathf.Abs(Mathf.Sin(stride)) * .05f * walk, -strike * .22f);
        _body.Rotation = new Vector3(-strike * .12f, 0, Mathf.Sin(stride) * .03f * walk);
        _head.Rotation = new Vector3(-strike * .45f + Mathf.Sin(time * 1.3f) * .03f, Mathf.Sin(time * .8f) * .05f, 0);
        _tail.Rotation = new Vector3(0, Mathf.Sin(time * 6f) * .4f, 0);
    }
}

using UnityEngine;

/// <summary>Bounded spell-card movement, independent of transport and rendering.</summary>
public sealed class ArenaBossMotion
{
    private float elapsed, holdUntil;
    private int stage = 1;
    private Vector3 velocity;
    public Vector3 Position { get; private set; }
    public Vector3 Velocity => velocity;
    public ArenaBossMotion(Vector3 position) { Position = position; }
    public void SetStage(int value) { stage = Mathf.Clamp(value, 1, 3); elapsed = 0; }
    public void HoldUntil(float time) { holdUntil = Mathf.Max(holdUntil, time); }

    public Vector3 Tick(float now, float deltaTime, bool slowed)
    {
        if (deltaTime <= 0) return Position;
        if (now < holdUntil) { velocity = Vector3.zero; return Position; }
        float pace = slowed ? .45f : 1f;
        elapsed += deltaTime * pace;
        Vector3 target;
        float speed;
        switch (stage)
        {
            case 2:
                // Alternate between flanks, with a dwell at each waypoint.
                int waypoint = Mathf.FloorToInt(elapsed / 4.5f) % 3;
                target = waypoint == 0 ? new Vector3(-6, Position.y, 6) :
                    waypoint == 1 ? new Vector3(6, Position.y, 6) : new Vector3(0, Position.y, 8);
                speed = 2.8f;
                break;
            case 3:
                target = new Vector3(Mathf.Sin(elapsed * .55f) * 6.5f, Position.y,
                    6.5f + Mathf.Sin(elapsed * 1.1f) * .9f);
                speed = 3.2f;
                break;
            default:
                target = new Vector3(Mathf.Sin(elapsed * .45f) * 5, Position.y,
                    7 + Mathf.Sin(elapsed * .3f) * .4f);
                speed = 2.2f;
                break;
        }
        Position = Vector3.SmoothDamp(Position, target, ref velocity, .35f, speed * pace, deltaTime);
        Position = new Vector3(Mathf.Clamp(Position.x, -7.5f, 7.5f), Position.y,
            Mathf.Clamp(Position.z, 4.5f, 8.5f));
        return Position;
    }
}

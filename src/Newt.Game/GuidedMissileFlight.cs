using System.Numerics;

namespace Newt.Game;

internal sealed class GuidedMissileFlight
{
    internal const float FireIntervalSeconds = 0.5f;
    internal const float RocketPodFireIntervalSeconds = 0.1f;
    internal const float LaunchSeconds = 0.4f;
    internal const float CruiseSpeed = 24f;
    internal const int MaximumTrailPoints = 128;
    private Vector3 _direction;
    private float _trailAge;
    private readonly float _surfaceRadius;
    internal Vector3 Position { get; private set; }
    internal Vector3 Target { get; set; }
    internal float Age { get; private set; }
    internal bool Arrived { get; private set; }
    internal bool Guided { get; }
    internal bool Expired => !Guided && !Arrived && Age >= 20;
    internal List<Vector3> Trail { get; } = [];

    internal GuidedMissileFlight(Vector3 origin, Vector3 launchDirection, Vector3 target,
        float surfaceRadius = 0, bool guided = true)
    {
        Position = origin;
        Target = target;
        _direction = Vector3.Normalize(launchDirection);
        _surfaceRadius = surfaceRadius;
        Guided = guided;
        Trail.Add(origin);
    }

    // Small integration steps keep steering and arrival reliable at different frame rates.
    internal bool Advance(float seconds, Func<Vector3, bool>? hitsSurface = null)
    {
        if (Arrived || Expired) return false;
        while (seconds > 0)
        {
            var dt = Math.Min(seconds, 1f / 120);
            seconds -= dt;
            Age += dt;
            var delta = Target - Position;
            var distance = delta.Length();
            var step = (Guided && Age < LaunchSeconds ? 12f : CruiseSpeed) * dt;
            if (Guided && Age >= LaunchSeconds && distance > 0.0001f)
            {
                var desired = delta / distance;
                if (_surfaceRadius > 0 && Vector3.Dot(Vector3.Normalize(Position), Vector3.Normalize(Target)) < 0.98f)
                {
                    // Follow the globe rather than cutting through its interior.
                    var up = Vector3.Normalize(Position);
                    var tangent = delta - up * Vector3.Dot(delta, up);
                    if (tangent.LengthSquared() > 0.0001f)
                        desired = Vector3.Normalize(Vector3.Normalize(tangent) + up * 0.12f);
                }
                var steering = Vector3.Lerp(_direction, desired, 1 - MathF.Exp(-4 * dt));
                if (steering.LengthSquared() > 0.0001f) _direction = Vector3.Normalize(steering);
            }
            var previous = Position;
            var next = Position + _direction * step;
            // Sweep the actual flight segment, including during launch. Guidance
            // cannot snap to the target or keep a missile above intervening terrain.
            var samples = Math.Max(1, (int)MathF.Ceiling(step / 0.05f));
            for (var sample = 1; sample <= samples; sample++)
            {
                var point = Vector3.Lerp(previous, next, sample / (float)samples);
                var hit = hitsSurface is not null ? hitsSurface(point)
                    : Age >= LaunchSeconds && Vector3.DistanceSquared(point, Target) <= 0.15f * 0.15f;
                if (!hit) continue;
                Position = point;
                Arrived = true;
                AddTrailPoint();
                return true;
            }
            Position = next;
            if (Expired) return false;
            _trailAge += dt;
            if (_trailAge >= 0.04f)
            {
                _trailAge = 0;
                AddTrailPoint();
            }
        }
        return false;
    }

    private void AddTrailPoint()
    {
        if (Trail.Count == MaximumTrailPoints) Trail.RemoveAt(0);
        Trail.Add(Position);
    }
}

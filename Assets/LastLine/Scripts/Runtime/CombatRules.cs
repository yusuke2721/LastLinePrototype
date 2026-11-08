using System;

namespace LastLine
{
    // Engine-independent rules: these can be checked without starting Unity.
    public sealed class RoundState
    {
        public int Lives { get; private set; }
        public int Score { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; }
        public bool IsRunning { get; private set; } = true;
        public bool IsVictory { get; private set; }

        public RoundState(int lives, float duration)
        {
            if (lives < 1 || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException();
            Lives = lives;
            Duration = duration;
        }

        public void Tick(float delta)
        {
            if (!IsRunning || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            Elapsed = Math.Min(Duration, Elapsed + delta);
            if (Elapsed >= Duration) { IsRunning = false; IsVictory = true; }
        }

        public void Breach()
        {
            if (!IsRunning) return;
            Lives--;
            if (Lives == 0) IsRunning = false;
        }

        public void AwardKill(int points)
        {
            if (IsRunning && points > 0) Score += points;
        }
    }

    public static class CombatRules
    {
        public static float HorizontalTarget(float normalizedPointer, float halfWidth)
        {
            float width = Math.Max(0, halfWidth);
            float normalized = Math.Max(0, Math.Min(1, normalizedPointer));
            return (normalized * 2 - 1) * width;
        }

        public static float StepHorizontal(float current, float target, float speed, float delta, float halfWidth)
        {
            float width = Math.Max(0, halfWidth);
            current = Math.Max(-width, Math.Min(width, current));
            target = Math.Max(-width, Math.Min(width, target));
            float difference = target - current;
            float distance = Math.Max(0, speed) * Math.Max(0, delta);
            return current + Math.Sign(difference) * Math.Min(distance, Math.Abs(difference));
        }

        // Three waves scale with the round duration: 0/20/40 seconds for a 60s round.
        public static float GetSpawnInterval(float elapsed, float duration,
            float first, float middle, float last)
        {
            float interval = elapsed < duration / 3f ? first
                : elapsed < duration * (2f / 3f) ? middle : last;
            return Math.Max(0.05f, interval);
        }

        // First contact with a circle in the horizontal plane, t in [0, 1].
        // Sweeping the whole frame segment prevents fast bullets skipping enemies.
        public static bool SegmentCircle(float ax, float az, float bx, float bz,
            float cx, float cz, float radius, out float t)
        {
            t = 0;
            double ox = ax - cx, oz = az - cz;
            double dx = bx - ax, dz = bz - az;
            double c = ox * ox + oz * oz - radius * radius;
            if (c <= 0) return true;
            double a = dx * dx + dz * dz;
            if (a < 1e-12) return false;
            double b = ox * dx + oz * dz;
            double discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            double hit = (-b - Math.Sqrt(discriminant)) / a;
            if (hit < 0 || hit > 1) return false;
            t = (float)hit;
            return true;
        }
    }
}

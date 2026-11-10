using System;

namespace LastLine
{
    // Relative to the scene's tuning; bounded escalation avoids overflow and impossible rates.
    public readonly struct StageDifficulty
    {
        public readonly float SpawnPressure, InfantrySpeed, TankPressure, TankSpeed;
        public readonly int InfantryArmor, TankArmor;
        public StageDifficulty(int stage)
        {
            int step = Math.Max(1, Math.Min(15, stage)) - 1;
            SpawnPressure = 1 + step * 0.20f;
            InfantrySpeed = 1 + step * 0.06f;
            TankPressure = 1 + step * 0.1f;
            TankSpeed = 1 + step * 0.04f;
            InfantryArmor = step / 3;
            TankArmor = step * 4;
        }
    }

    // Engine-independent rules: these can be checked without starting Unity.
    public sealed class RoundState
    {
        public int Lives { get; private set; }
        public int Score { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; }
        public bool IsRunning { get; private set; } = true;
        public bool IsVictory { get; private set; }
        public const float ComboWindow = 4f, OverdriveDuration = 6f;
        public const int OverdriveThreshold = 8;
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public float ComboRemaining { get; private set; }
        public float OverdriveRemaining { get; private set; }
        public bool IsOverdrive => OverdriveRemaining > 0;
        public int ScoreMultiplier => IsOverdrive ? 3 : Combo >= 4 ? 2 : 1;
        public float FireRateMultiplier => IsRunning && IsOverdrive ? 2f : 1f;

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
            if (IsOverdrive)
            {
                OverdriveRemaining = Math.Max(0, OverdriveRemaining - delta);
                // An activation has a fixed duration; kills cannot extend it.
                if (!IsOverdrive) ResetCombo();
            }
            else
            {
                ComboRemaining = Math.Max(0, ComboRemaining - delta);
                if (ComboRemaining == 0) Combo = 0;
            }
            if (Elapsed >= Duration) { IsRunning = false; IsVictory = true; }
        }

        public void Breach()
        {
            if (!IsRunning) return;
            ResetCombo();
            Lives--;
            if (Lives == 0) IsRunning = false;
        }

        public void AwardKill(int points)
        {
            if (!IsRunning || points <= 0) return;
            Combo++;
            BestCombo = Math.Max(BestCombo, Combo);
            ComboRemaining = ComboWindow;
            if (!IsOverdrive && Combo >= OverdriveThreshold)
                OverdriveRemaining = OverdriveDuration;
            Score += points * ScoreMultiplier;
        }

        private void ResetCombo()
        {
            Combo = 0;
            ComboRemaining = OverdriveRemaining = 0;
        }
    }

    public static class CombatRules
    {
        // Round up the surviving half, always keeping the controllable soldier.
        public static int SquadAfterBreach(int soldiers)
        {
            int count = Math.Max(1, soldiers);
            return count / 2 + count % 2;
        }

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
                : elapsed < (duration / 3f) * 2f ? middle : last;
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

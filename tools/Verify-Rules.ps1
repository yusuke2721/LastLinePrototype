$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets/LastLine/Scripts/Runtime/CombatRules.cs') -Raw
$tests = @'
namespace LastLine.Validation
{
    public static class RuleTests
    {
        private static System.Collections.Generic.List<string> results;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new System.Exception("FAIL: " + name);
            results.Add("PASS: " + name);
        }

        public static string[] Run()
        {
            results = new System.Collections.Generic.List<string>();
            var round = new RoundState(3, 60);
            Check(round.IsRunning && round.Lives == 3 && round.Score == 0 && round.Elapsed == 0, "Initial state");
            round.AwardKill(10); round.AwardKill(-20);
            Check(round.Score == 10, "Positive kill score only");
            round.Breach();
            Check(round.Lives == 2 && round.IsRunning, "Breach costs one life");
            round.Breach(); round.Breach(); round.Breach();
            Check(round.Lives == 0 && !round.IsRunning && !round.IsVictory, "Loss stops at zero lives");
            round.AwardKill(10); round.Tick(99);
            Check(round.Score == 10 && round.Elapsed == 0, "Loss freezes score and clock");
            round = new RoundState(3, 60);
            round.Tick(59.9f);
            Check(round.IsRunning && !round.IsVictory, "Running before deadline");
            round.Tick(0.2f);
            Check(round.IsVictory && !round.IsRunning && round.Elapsed == 60, "Deadline clamps to 60 seconds");
            round.Breach(); round.AwardKill(30); round.Tick(10);
            Check(round.Lives == 3 && round.Score == 0 && round.Elapsed == 60, "Victory freezes changes");
            round = new RoundState(3, 60);
            round.Tick(-1); round.Tick(float.NaN); round.Tick(float.PositiveInfinity);
            Check(round.Elapsed == 0, "Invalid time deltas ignored");
            for (int i = 0; i < 3600; i++) round.Tick(1f / 60f);
            // Floating point accumulation may be a few milliseconds short; the next frame ends it.
            round.Tick(1f / 60f);
            Check(round.IsVictory, "60 FPS simulated round reaches victory");
            round = new RoundState(3, 60);
            Check(round.Lives == 3 && round.Score == 0 && round.Elapsed == 0 && round.IsRunning, "Fresh round resets state");
            bool rejected = false;
            try { new RoundState(0, 60); } catch (System.ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid lives rejected");
            rejected = false;
            try { new RoundState(3, float.NaN); } catch (System.ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid duration rejected");

            float t;
            Check(CombatRules.SegmentCircle(0, 0, 0, 40, 0, 20, 1, out t) && System.Math.Abs(t - 0.475f) < 0.00001f, "High speed sweep finds first contact");
            Check(!CombatRules.SegmentCircle(0, 0, 0, 40, 3, 20, 1, out t), "Lateral miss");
            Check(CombatRules.SegmentCircle(0, 0, 0, 10, 1, 5, 1, out t) && t == 0.5f, "Tangent contact");
            Check(CombatRules.SegmentCircle(0, 0, 0, 10, 0, 11, 1, out t) && t == 1, "Segment endpoint contact");
            Check(!CombatRules.SegmentCircle(0, 0, 0, 10, 0, 12, 1, out t), "Target beyond segment ignored");
            Check(!CombatRules.SegmentCircle(0, 0, 0, 10, 0, -5, 1, out t), "Target behind segment ignored");
            Check(CombatRules.SegmentCircle(0, 0, 0, 0, 0, 0, 1, out t) && t == 0, "Stationary bullet inside target");
            Check(!CombatRules.SegmentCircle(0, 0, 0, 0, 2, 0, 1, out t), "Stationary bullet outside target");
            Check(CombatRules.SegmentCircle(0, 0, 0, 10, 0, 0, 1, out t) && t == 0, "Bullet starting inside target");
            Check(!CombatRules.SegmentCircle(0, 2, 0, 10, 0, 0, 1, out t), "Moving away from target");
            Check(CombatRules.SegmentCircle(-10, 0, 10, 0, 0, 0, 1, out t) && System.Math.Abs(t - 0.45f) < 0.00001f, "Horizontal sweep");
            float near, far;
            CombatRules.SegmentCircle(0, 0, 0, 20, 0, 5, 1, out near);
            CombatRules.SegmentCircle(0, 0, 0, 20, 0, 10, 1, out far);
            Check(near < far, "Contact fractions order nearest enemy first");
            results.Add("PASS: " + results.Count + " assertions; actual CombatRules.cs compiled with Add-Type. Unity was not used.");
            return results.ToArray();
        }
    }
}
'@
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $tests)
[LastLine.Validation.RuleTests]::Run()

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
            Check(CombatRules.GetSpawnInterval(0, 60, 0.65f, 0.3f, 0.1f) == 0.65f, "First wave at round start");
            Check(CombatRules.GetSpawnInterval(19.999f, 60, 0.65f, 0.3f, 0.1f) == 0.65f, "First wave before 20 seconds");
            Check(CombatRules.GetSpawnInterval(20, 60, 0.65f, 0.3f, 0.1f) == 0.3f, "Middle wave at 20 seconds");
            Check(CombatRules.GetSpawnInterval(39.999f, 60, 0.65f, 0.3f, 0.1f) == 0.3f, "Middle wave before 40 seconds");
            Check(CombatRules.GetSpawnInterval(40, 60, 0.65f, 0.3f, 0.1f) == 0.1f, "Final wave at 40 seconds");
            Check(CombatRules.GetSpawnInterval(59.999f, 60, 0.65f, 0.3f, 0.1f) == 0.1f, "Final wave until end");
            Check(CombatRules.GetSpawnInterval(29, 90, 1, 0.5f, 0.2f) == 1 &&
                CombatRules.GetSpawnInterval(30, 90, 1, 0.5f, 0.2f) == 0.5f, "Wave boundaries scale with 90 second round");
            Check(CombatRules.GetSpawnInterval(60, 90, 1, 0.5f, 0.2f) == 0.2f, "Custom last-wave timing and interval");
            Check(CombatRules.GetSpawnInterval(0, 60, -1, 0, 0) == 0.05f, "Spawn interval has safe lower bound");
            round = new RoundState(3, 60); round.Tick(45);
            Check(CombatRules.GetSpawnInterval(round.Elapsed, round.Duration, 0.65f, 0.3f, 0.1f) == 0.1f,
                "Late-round state selects final wave");
            round = new RoundState(3, 60);
            Check(CombatRules.GetSpawnInterval(round.Elapsed, round.Duration, 0.65f, 0.3f, 0.1f) == 0.65f,
                "Retry returns to first wave");
            Check(CombatRules.HorizontalTarget(0.5f, 5.5f) == 0, "Pointer center maps to center lane");
            Check(CombatRules.HorizontalTarget(0, 5.5f) == -5.5f, "Left edge maps to left lane");
            Check(CombatRules.HorizontalTarget(1, 5.5f) == 5.5f, "Right edge maps to right lane");
            Check(CombatRules.HorizontalTarget(-1, 5.5f) == -5.5f && CombatRules.HorizontalTarget(2, 5.5f) == 5.5f,
                "Pointer target clamped to movement bounds");
            Check(CombatRules.StepHorizontal(0, 5.5f, 16, 0.1f, 5.5f) == 1.6f, "Shooter moves at configured speed");
            Check(CombatRules.StepHorizontal(0, 5.5f, 16, 1, 5.5f) == 5.5f, "Shooter cannot overshoot destination");
            Check(CombatRules.StepHorizontal(0, -99, 16, 1, 5.5f) == -5.5f, "Shooter cannot leave left boundary");
            Check(CombatRules.StepHorizontal(0, 99, 16, 1, 5.5f) == 5.5f, "Shooter cannot leave right boundary");
            Check(CombatRules.StepHorizontal(3, -5.5f, 16, 0.1f, 5.5f) == 1.4f, "Shooter reverses direction");
            Check(CombatRules.StepHorizontal(2, 5, 16, 0, 5.5f) == 2, "Zero delta does not move shooter");
            Check(CombatRules.StepHorizontal(2, 5, 0, 1, 5.5f) == 2, "Zero speed does not move shooter");
            Check(CombatRules.StepHorizontal(3, 5, 16, 1, 0) == 0, "Zero movement width locks to center");
            float x30 = 0, x60 = 0;
            for (int i = 0; i < 15; i++) x30 = CombatRules.StepHorizontal(x30, 5.5f, 8, 1f / 30, 5.5f);
            for (int i = 0; i < 30; i++) x60 = CombatRules.StepHorizontal(x60, 5.5f, 8, 1f / 60, 5.5f);
            Check(System.Math.Abs(x30 - x60) < 0.0001f && System.Math.Abs(x60 - 4) < 0.0001f,
                "Shooter movement is frame-rate independent");
            var stage1 = new StageDifficulty(1);
            var stage2 = new StageDifficulty(2);
            var stage4 = new StageDifficulty(4);
            Check(stage1.SpawnPressure == 1 && stage1.InfantrySpeed == 1 && stage1.TankArmor == 0 && stage1.InfantryArmor == 0,
                "Stage one preserves scene tuning");
            Check(stage2.SpawnPressure > stage1.SpawnPressure && stage2.InfantrySpeed > stage1.InfantrySpeed,
                "Next stage increases infantry pressure and speed");
            Check(stage2.TankPressure > 1 && stage2.TankSpeed > 1 && stage2.TankArmor == 4,
                "Next stage strengthens and accelerates armor");
            Check(stage4.InfantryArmor == 1 && stage4.TankArmor == 12, "Later stages increase infantry and tank durability");
            Check(new StageDifficulty(0).SpawnPressure == 1 && new StageDifficulty(int.MinValue).SpawnPressure == 1,
                "Invalid stage numbers clamp safely");
            Check(new StageDifficulty(int.MaxValue).TankArmor == 56 && Math.Abs(new StageDifficulty(999).SpawnPressure - 3.8f) < 0.001f,
                "Difficulty escalation has bounded safe limits");
            Check(CombatRules.SquadAfterBreach(5) == 3, "Five soldiers retain three after breach");
            Check(CombatRules.SquadAfterBreach(4) == 2, "Four soldiers retain two after breach");
            Check(CombatRules.SquadAfterBreach(3) == 2, "Odd squad halves round up");
            Check(CombatRules.SquadAfterBreach(2) == 1, "Two soldiers retain the player");
            Check(CombatRules.SquadAfterBreach(1) == 1, "Last soldier is never removed by halving");
            Check(CombatRules.SquadAfterBreach(0) == 1 && CombatRules.SquadAfterBreach(-10) == 1,
                "Invalid squad count keeps a controllable player");
            Check(CombatRules.SquadAfterBreach(int.MaxValue) == 1073741824, "Halving cannot overflow");
            results.Add("PASS: " + results.Count + " assertions; actual CombatRules.cs compiled with Add-Type. Unity was not used.");
            return results.ToArray();
        }
    }
}
'@
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $tests)
[LastLine.Validation.RuleTests]::Run()

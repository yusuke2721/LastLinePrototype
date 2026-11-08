using System;
using UnityEditor;
using UnityEngine;

namespace LastLine.Editor
{
    // No NUnit/Test Framework package required. Results are written to the Console.
    [InitializeOnLoad]
    public static class PrototypeChecks
    {
        private const string Pending = "LastLine.PlayChecks";
        private static double started;
        static PrototypeChecks() { EditorApplication.update += Poll; }

        [MenuItem("Last Line/Run Edit Mode Checks", false, 10)]
        public static void EditChecks()
        {
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            Check(game != null, "GameController exists");
            Check(game.enemies != null && game.enemies.prefab != null, "Enemy references");
            Check(game.gun != null && game.gun.bulletPrefab != null && game.gun.muzzle != null, "Weapon references");
            Check(game.gun.aimCamera != null && game.hud != null && game.hud.gameCamera != null, "Camera/UI references");
            Check(game.enemies.capacity >= 1 && game.enemies.capacity <= 100, "Enemy cap 1..100");
            Check(game.enemies.spawnZ > game.enemies.defenseZ, "Enemies spawn beyond line");
            Check(game.gun.muzzle.position.z < game.enemies.defenseZ, "Shooter behind line");
            Check(game.gun.poolCapacity >= Mathf.CeilToInt(game.gun.shotsPerSecond * game.gun.bulletPrefab.lifetime) + 1,
                "Bullet pool covers firing rate and lifetime");
            var state = new RoundState(2, 60);
            state.AwardKill(10); state.Breach(); state.Breach(); state.Breach(); state.AwardKill(20);
            Check(!state.IsRunning && state.Lives == 0 && state.Score == 10 && !state.IsVictory, "Loss freezes rules");
            state = new RoundState(2, 60); state.Tick(70); state.Breach();
            Check(state.IsVictory && state.Elapsed == 60 && state.Lives == 2, "Deadline clamps and freezes");
            Check(CombatRules.SegmentCircle(0, 0, 0, 40, 0, 20, 0.5f, out float t) && t > 0 && t < 1,
                "Swept hit at high speed");
            Check(CombatRules.GetSpawnInterval(20, 60, 0.65f, 0.3f, 0.1f) == 0.3f, "Middle wave boundary");
            Check(CombatRules.GetSpawnInterval(40, 60, 0.65f, 0.3f, 0.1f) == 0.1f, "Final wave boundary");
            Debug.Log("Last Line Edit Mode checks: PASS (13 checks).");
        }

        [MenuItem("Last Line/Run Play Mode Checks", false, 11)]
        public static void PlayChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditChecks();
            SessionState.SetBool(Pending, true);
            started = 0;
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 30)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogError("Last Line Play Mode checks: timeout waiting for scene initialization.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused || Time.deltaTime <= 0) return;
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            if (game == null || game.State == null) return;
            SessionState.SetBool(Pending, false);
            try
            {
                game.enemies.enabled = false;
                game.gun.enabled = false;
                game.RestartRound();
                Check(game.shooter != null, "Moving shooter initialized in existing scene");
                Check(game.gun.transform.Find("Shooter Visuals") != null, "Shooter character visuals created");
                Check(Vector3.Dot(game.gun.transform.forward, Vector3.forward) > 0.999f, "Weapon faces forward");
                var resetPosition = game.gun.transform.position;
                game.gun.transform.position += Vector3.right * 3;
                game.RestartRound();
                Check(Vector3.Distance(game.gun.transform.position, resetPosition) < 0.001f, "Retry returns shooter to starting lane");
                var effects = game.effects;
                Check(effects != null && effects.PoolSize > 0, "Effect pool initialized in existing scene");
                effects.ShowMuzzleFlash();
                Check(effects.IsFlashVisible, "Muzzle flash shown");
                effects.Clear();
                Check(!effects.IsFlashVisible && effects.ActiveBurstCount == 0, "Effects clear");
                var enemies = game.enemies;
                var straight = enemies.Spawn(new Vector3(3, 1, 8));
                game.gun.transform.position += Vector3.left * 3;
                var before = straight.transform.position;
                straight.SendMessage("Update");
                Check(straight.transform.position.x == before.x && straight.transform.position.y == before.y &&
                    straight.transform.position.z < before.z, "Enemy keeps spawn lane despite shooter movement");
                straight.Despawn();
                int livesBeforeBreach = game.State.Lives;
                var breacher = enemies.Spawn(new Vector3(-2, 1, enemies.defenseZ - 0.1f));
                breacher.SendMessage("Update");
                Check(!breacher.IsAlive && game.State.Lives == livesBeforeBreach - 1, "Crossing line costs one life and returns enemy to pool");
                breacher.SendMessage("Update");
                Check(game.State.Lives == livesBeforeBreach - 1 && game.State.Score == 0, "Breach counted once and awards no score");
                game.RestartRound();
                var first = enemies.Spawn(new Vector3(0, 1, 8));
                Check(first != null && enemies.ActiveCount == 1, "Spawn from pool");
                // HP reset on pool reuse, and a high-speed hit through two enemies.
                first.maxHealth = 2;
                first.Despawn();
                first = enemies.Spawn(new Vector3(0, 1, 8));
                var rear = enemies.Spawn(new Vector3(0, 1, 12));
                Check(first.Health == 2, "HP reset on reuse");
                Check(enemies.TryHit(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 1), "Swept hit");
                Check(first.Health == 1 && rear.Health == rear.maxHealth && game.State.Score == 0 && effects.ActiveBurstCount == 0, "Nearest hit only, no early score");
                enemies.TryHit(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 1);
                Check(effects.ActiveBurstCount == 1, "One kill creates one burst");
                int score = game.State.Score;
                first.TakeDamage(999);
                Check(!first.IsAlive && score == first.scoreValue && game.State.Score == score, "Kill scored once");
                Check(effects.ActiveBurstCount == 1, "Duplicate damage does not create another burst");
                for (int i = 1; i < effects.PoolSize; i++) Check(effects.PlayKill(Vector3.zero), "Burst pool slot " + i);
                Check(!effects.PlayKill(Vector3.zero) && effects.ActiveBurstCount == effects.PoolSize, "Burst pool hard cap");
                effects.ShowMuzzleFlash();
                game.RestartRound();
                for (int i = 0; i < enemies.capacity; i++) Check(enemies.Spawn(new Vector3(0, 1, 20)) != null, "Pool slot " + i);
                Check(enemies.Spawn(Vector3.zero) == null && enemies.ActiveCount == enemies.capacity, "Pool hard cap");
                for (int i = 0; i < game.startingLives + 2; i++) game.EnemyBreached();
                Check(!game.IsRunning && game.State.Lives == 0 && !game.State.IsVictory, "Game over");
                game.RestartRound();
                Check(!effects.IsFlashVisible && effects.ActiveBurstCount == 0, "Retry clears all effects");
                Check(enemies.ActiveCount == 0 && game.State.Score == 0 && game.State.Elapsed == 0 &&
                    game.State.Lives == game.startingLives && game.IsRunning, "Retry reset");
                game.State.Tick(game.roundSeconds + 1);
                Check(game.State.IsVictory && !game.IsRunning, "60 second survival");
                Debug.Log("Last Line Play Mode checks: PASS. Exiting Play Mode.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { EditorApplication.isPlaying = false; }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Last Line check failed: " + message);
        }
    }
}

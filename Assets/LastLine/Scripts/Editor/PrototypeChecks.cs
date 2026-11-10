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
        private const string Batch = "LastLine.BatchChecks";
        private const string Preview = "LastLine.DronePreview";
        private const string PreviewRunning = "LastLine.DronePreviewRunning";
        private static double started;
        static PrototypeChecks()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingPlayMode) return;
                SessionState.SetBool(Preview, false);
                if (!SessionState.GetBool(PreviewRunning, false)) return;
                Time.timeScale = SessionState.GetFloat("LastLine.PreviewTimeScale", 1);
                SessionState.SetBool(PreviewRunning, false);
            };
        }

        [MenuItem("Last Line/Preview Drone Strike", false, 12)]
        public static void PreviewDroneStrike()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditChecks();
            SessionState.SetBool(Preview, true);
            EditorApplication.isPlaying = true;
        }

        private static void PrepareDronePreview()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.deltaTime <= 0) return;
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            if (game == null || game.State == null) return;
            SessionState.SetBool(Preview, false);
            SessionState.SetBool(PreviewRunning, true);
            SessionState.SetFloat("LastLine.PreviewTimeScale", Time.timeScale);
            Time.timeScale = 0.2f;
            game.enemies.enabled = false;
            game.gun.enabled = false;
            game.support.firstTargetSeconds = 9999;
            game.StartCampaign();
            game.enemies.SpawnTank(new Vector3(0, 1, 12));
            game.enemies.Spawn(new Vector3(1.5f, 1, 12));
            game.enemies.Spawn(new Vector3(-1.5f, 1, 12));
            game.support.SpawnTarget(SupportKind.Drone, new Vector3(0, 1, 8));
            game.support.DamageTarget(999);
            Debug.Log("Drone preview: slow motion. Stop Play to restore normal settings; do not save the scene.");
        }

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

        // Command-line smoke checks against the existing generated scene; never saves it.
        public static void RunBatchChecks()
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/LastLine/Generated/Minimal/LastLine.unity");
                SessionState.SetBool(Batch, true);
                PlayChecks();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Poll()
        {
            if (SessionState.GetBool(Preview, false)) { PrepareDronePreview(); return; }
            if (!SessionState.GetBool(Pending, false)) return;
            if (started == 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started > 30)
            {
                SessionState.SetBool(Pending, false);
                Debug.LogError("Last Line Play Mode checks: timeout waiting for scene initialization.");
                EditorApplication.isPlaying = false;
                if (SessionState.GetBool(Batch, false)) EditorApplication.Exit(1);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPaused || Time.deltaTime <= 0) return;
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            if (game == null || game.State == null) return;
            SessionState.SetBool(Pending, false);
            bool passed = false;
            try
            {
                game.enemies.enabled = false;
                game.gun.enabled = false;
                game.support.enabled = false;
                Check(game.IsTitle && !game.IsRunning && game.State.Elapsed == 0 && game.enemies.ActiveCount == 0,
                    "Launch waits on title without spawning enemies or advancing time");
                game.SendMessage("Update");
                Check(game.State.Elapsed == 0 && !game.gun.gameObject.activeSelf, "Title freezes combat and hides shooter");
                game.StartCampaign();
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
                Check(straight.transform.Find("Enemy Visuals/Glove") != null &&
                    game.gun.transform.Find("Shooter Visuals/Glove") != null &&
                    game.transform.Find("Support Pool/Ally 1/Glove") != null, "Infantry, shooter and reinforcements use the two-handed rifle pose");
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
                var tank = enemies.SpawnTank(new Vector3(0, 1, 8));
                var behindTank = enemies.Spawn(new Vector3(0, 1, 12));
                Check(tank != null && tank.IsTank && tank.Health == enemies.tankHealth && tank.Health > 1,
                    "Heavy tank spawns with configured armor");
                Check(tank.transform.Find("Tank Visuals/Cannon") != null, "Tank geometry initialized");
                for (int i = 0; i < enemies.tankHealth - 1; i++)
                    enemies.TryHit(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 1);
                Check(tank.IsAlive && tank.Health == 1 && game.State.Score == 0 && behindTank.Health == behindTank.maxHealth,
                    "Armor takes repeated hits and shields the soldier behind it");
                enemies.TryHit(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 1);
                tank.TakeDamage(999);
                Check(!tank.IsAlive && game.State.Score == enemies.tankScore, "Tank dies on final hit and scores once");
                var reusedTank = enemies.SpawnTank(new Vector3(0, 1, 8));
                Check(reusedTank == tank && reusedTank.Health == enemies.tankHealth, "Tank pool reuse restores armor");
                int tankLives = game.State.Lives;
                reusedTank.transform.position = new Vector3(0, 1, enemies.defenseZ - 0.1f);
                reusedTank.SendMessage("Update");
                reusedTank.SendMessage("Update");
                Check(!reusedTank.IsAlive && game.State.Lives == tankLives - 1, "Tank breach counted once");
                game.RestartRound();
                enemies.SpawnTank(new Vector3(0, 1, 20));
                for (int i = 0; i < enemies.capacity - 1; i++) enemies.Spawn(new Vector3(0, 1, 20));
                Check(enemies.ActiveCount == enemies.capacity && enemies.SpawnTank(Vector3.zero) == null,
                    "Soldiers and tanks share the active enemy cap");
                game.RestartRound();
                Check(!tank.IsAlive && enemies.ActiveCount == 0, "Retry clears tanks and soldiers");
                game.State.Tick(Mathf.Max(0, enemies.firstTankSeconds - 0.1f));
                enemies.SendMessage("Update");
                Check(!tank.IsAlive, "No tank before first tank deadline");
                game.State.Tick(0.2f);
                enemies.SendMessage("Update");
                Check(!tank.IsAlive, "Opening sector keeps armor locked at the tank deadline");
                enemies.SendMessage("Update");
                int activeTanks = 0;
                foreach (var heavy in enemies.Tanks) if (heavy.IsAlive) activeTanks++;
                Check(activeTanks == 0, "Opening sector stays infantry-only");
                game.RestartRound();
                enemies.SendMessage("Update");
                Check(!tank.IsAlive, "Retry resets tank schedule");
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
                CheckSupport(game);
                CheckBreachSquad(game);
                CheckOverdrive(game);
                game.State.Tick(game.roundSeconds + 1);
                Check(game.State.IsVictory && !game.IsRunning, "60 second survival");
                CheckCampaign(game);
                CheckSectorWaves(game);
                passed = true;
                Debug.Log("Last Line Play Mode checks: PASS. Exiting Play Mode.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                EditorApplication.isPlaying = false;
                if (SessionState.GetBool(Batch, false))
                {
                    SessionState.SetBool(Batch, false);
                    EditorApplication.Exit(passed ? 0 : 1);
                }
            }
        }

        private static void CheckOverdrive(GameController game)
        {
            game.RestartRound();
            float normalRate = game.gun.EffectiveFireRate;
            for (int i = 0; i < RoundState.OverdriveThreshold; i++)
            {
                var enemy = game.enemies.Spawn(new Vector3(0, 1, 12));
                enemy.TakeDamage(999);
                enemy.TakeDamage(999);
            }
            Check(game.State.Combo == 8 && game.State.IsOverdrive && game.State.Score ==
                game.enemies.prefab.scoreValue * 14, "Real kills build combo, multiply rewards, and reject duplicate damage");
            Check(Mathf.Approximately(game.gun.EffectiveFireRate, normalRate * 2), "Overdrive doubles actual weapon fire rate");
            game.support.RestoreLoadout(SupportTargets.MaxAllies, SupportTargets.MaxWeaponLevel);
            Check(Mathf.Approximately(game.gun.EffectiveFireRate, game.gun.shotsPerSecond * 1.3f * 2),
                "Overdrive stacks with maximum weapon upgrade");
            game.gun.SendMessage("Fire");
            Check(game.gun.ActiveBulletCount == SupportTargets.MaxAllies + 1, "Boosted squad fires a complete volley");
            game.EnemyBreached();
            Check(game.State.Combo == 0 && !game.State.IsOverdrive && game.State.BestCombo == 8 &&
                Mathf.Approximately(game.gun.EffectiveFireRate, game.gun.shotsPerSecond * 1.3f),
                "Real breach cancels boosted rate while retaining weapon upgrade");
            game.RestartRound();
            Check(game.State.BestCombo == 0 && game.State.Combo == 0 && game.gun.ActiveBulletCount == 0 &&
                Mathf.Approximately(game.gun.EffectiveFireRate, normalRate), "Retry clears combo, boost, and bullets");
        }

        private static void CheckSupport(GameController game)
        {
            var support = game.support;
            var enemies = game.enemies;
            game.RestartRound();
            Check(support != null && support.AllyCount == 0 && support.WeaponLevel == 1, "Support starts at base strength");
            Check(support.SpawnTarget(SupportKind.Reinforcements, new Vector3(0, 1, 8)), "Shootable reward spawns");
            var blocker = enemies.Spawn(new Vector3(0, 1, 4));
            game.TryHitShot(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 999);
            Check(!blocker.IsAlive && support.TargetHealth == support.targetHealth && support.AllyCount == 0,
                "Closer enemy shields reward target");
            game.RestartRound();
            support.SpawnTarget(SupportKind.Reinforcements, new Vector3(0, 1, 4));
            var behind = enemies.Spawn(new Vector3(0, 1, 8));
            for (int i = 0; i < support.targetHealth - 1; i++)
                game.TryHitShot(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 1);
            Check(!support.ClaimedSupportThisRound && support.AllyCount == 0 && support.TargetHealth == 1, "Reward requires all target HP");
            game.TryHitShot(new Vector3(0, 1, 0), new Vector3(0, 1, 20), 0.1f, 1);
            support.DamageTarget(999);
            Check(support.ClaimedSupportThisRound && support.AllyCount == 1 && !support.TargetActive && behind.Health == behind.maxHealth,
                "Reward granted once without piercing enemy behind target");
            game.gun.ResetPool();
            game.gun.SendMessage("Fire");
            Check(game.gun.ActiveBulletCount == 2, "Reinforcement adds an actual firing lane");
            game.gun.ResetPool();
            for (int i = 0; i < 6; i++) GrantSupport(support, SupportKind.Reinforcements);
            Check(support.AllyCount == SupportTargets.MaxAllies, "Reinforcements have a hard cap");
            GrantSupport(support, SupportKind.Weapon);
            Check(support.WeaponLevel == 2 && game.gun.EffectiveDamage == game.gun.damage + 1 &&
                game.gun.EffectiveFireRate > game.gun.shotsPerSecond, "Upgrade increases actual damage and fire rate");
            for (int i = 0; i < 6; i++) GrantSupport(support, SupportKind.Weapon);
            Check(support.WeaponLevel == SupportTargets.MaxWeaponLevel, "Weapon level has a hard cap");
            game.gun.ResetPool();
            game.gun.SendMessage("Fire");
            Check(game.gun.ActiveBulletCount == 5, "Full squad fires five bullets per volley");
            game.RestartRound();
            Check(support.AllyCount == 0 && support.WeaponLevel == 1 && game.gun.EffectiveDamage == game.gun.damage &&
                game.gun.EffectiveFireRate == game.gun.shotsPerSecond && game.gun.ActiveBulletCount == 0,
                "Retry restores base weapon and removes allied fire");
            GrantSupport(support, SupportKind.Drone);
            support.SendMessage("Update");
            Check(support.QueuedDrones == 1 && support.DroneStrikes == 0, "Drone waits when no enemies exist");
            var tank = enemies.SpawnTank(new Vector3(0, 1, 8));
            var near = enemies.Spawn(new Vector3(1, 1, 8));
            var far = enemies.Spawn(new Vector3(-5, 1, 20));
            Check(enemies.FindStrikeTarget() == tank, "Drone prioritizes armor");
            support.SendMessage("Update");
            Check(support.DroneActive && tank.Health == tank.maxHealth && near.IsAlive,
                "Drone must approach and drop its bomb before damage is applied");
            bool sawBomb = support.BombVisible, sawExplosion = support.ExplosionVisible;
            int steps = Mathf.Min(10000, Mathf.CeilToInt(2.7f / Time.deltaTime) + 5);
            for (int i = 0; i < steps; i++)
            {
                support.SendMessage("Update");
                sawBomb |= support.BombVisible;
                sawExplosion |= support.ExplosionVisible;
            }
            Check(!tank.IsAlive && !near.IsAlive && far.IsAlive && support.DroneStrikes == 1 && support.QueuedDrones == 0,
                "Drone bombing applies local splash damage, including armor");
            Check(sawBomb && sawExplosion && !support.DroneActive && !support.BombVisible && !support.ExplosionVisible,
                "Drone shows bomb and explosion, then departs and clears effects");
            int score = game.State.Score;
            support.SendMessage("Update");
            Check(score == tank.scoreValue + near.scoreValue && game.State.Score == score, "Drone scores each kill once");
            game.RestartRound();
            support.SpawnTarget(SupportKind.Weapon, new Vector3(0, 1, enemies.defenseZ));
            support.SendMessage("Update");
            Check(!support.TargetActive && support.WeaponLevel == 1, "Missed target expires without granting reward");
            game.State.Tick(support.firstTargetSeconds + 0.1f);
            game.gun.transform.position += Vector3.right * 2;
            support.SendMessage("Update");
            Check(support.TargetActive && support.TargetKind == SupportKind.Reinforcements, "Reward schedule starts with reinforcements");
            Vector3 targetStart = support.ActiveTarget.position;
            Check(targetStart.z == enemies.spawnZ && targetStart.x < 0,
                "Reward enters from enemy spawn depth on the opposite side of the field");
            support.SendMessage("Update");
            Check(support.ActiveTarget.position.x == targetStart.x && support.ActiveTarget.position.y == targetStart.y &&
                support.ActiveTarget.position.z < targetStart.z, "Reward keeps its lane while approaching the player");
            game.State.Tick(8);
            support.SendMessage("Update");
            Check(support.TargetActive, "Reward no longer expires after the old seven-second timeout");
            support.DamageTarget(999);
            game.State.Tick(support.targetInterval + 0.1f);
            support.SendMessage("Update");
            Check(support.TargetKind == SupportKind.Weapon, "Reward schedule advances to weapon upgrade");
            support.DamageTarget(999);
            game.State.Tick(support.targetInterval + 0.1f);
            support.SendMessage("Update");
            Check(support.TargetKind == SupportKind.Reinforcements, "Opening sector cycles ground support only");
            game.State.Tick(game.roundSeconds + 1);
            support.DamageTarget(999);
            Check(support.QueuedDrones == 0, "Round end prevents reward collection");
            game.RestartRound();
            Check(!support.TargetActive && support.DroneStrikes == 0 && support.QueuedDrones == 0 && support.AllyCount == 0,
                "Retry clears rewards, targets and drones");
            GrantSupport(support, SupportKind.Drone);
            enemies.SpawnTank(new Vector3(0, 1, 8));
            support.SendMessage("Update");
            Check(support.DroneActive, "Drone strike in progress before reset");
            game.RestartRound();
            Check(!support.DroneActive && !support.BombVisible && !support.ExplosionVisible && support.QueuedDrones == 0,
                "Retry cancels active drone, bomb and explosion");
        }

        private static void CheckBreachSquad(GameController game)
        {
            game.StartCampaign();
            game.support.RestoreLoadout(4, 3);
            int lives = game.State.Lives;
            var breacher = game.enemies.Spawn(new Vector3(0, 1, game.enemies.defenseZ - 0.1f));
            breacher.SendMessage("Update");
            Check(game.support.AllyCount == 2 && game.State.Lives == lives - 1 && game.support.WeaponLevel == 3,
                "Infantry breach halves five soldiers to three, preserves weapon, and costs one life");
            Check(game.support.BreachWarningUntil > Time.unscaledTime, "Breach displays warning");
            int visible = 0;
            for (int i = 0; i < SupportTargets.MaxAllies; i++)
                if (game.transform.Find("Support Pool/Ally " + (i + 1)).gameObject.activeSelf) visible++;
            Check(visible == 2, "Lost allies are removed from visible formation");
            game.gun.ResetPool();
            game.gun.SendMessage("Fire");
            Check(game.gun.ActiveBulletCount == 3, "Squad loss reduces actual volley to three bullets");
            breacher.SendMessage("Update");
            Check(game.support.AllyCount == 2 && game.State.Lives == lives - 1, "Same enemy cannot halve squad twice");
            var tank = game.enemies.SpawnTank(new Vector3(0, 1, game.enemies.defenseZ - 0.1f));
            tank.SendMessage("Update");
            Check(game.support.AllyCount == 1 && game.State.Lives == lives - 2, "Tank breach halves three soldiers to two");
            game.EnemyBreached();
            Check(game.support.AllyCount == 0, "Further breach leaves only player");
            game.EnemyBreached();
            game.gun.ResetPool();
            game.gun.SendMessage("Fire");
            Check(game.support.AllyCount == 0 && game.gun.ActiveBulletCount == 1, "Last player remains controllable and fires alone");
            GrantSupport(game.support, SupportKind.Reinforcements);
            Check(game.support.AllyCount == 1, "Reinforcements can rebuild a depleted squad");
            game.State.Tick(game.roundSeconds + 1);
            lives = game.State.Lives;
            game.EnemyBreached();
            Check(game.support.AllyCount == 1 && game.State.Lives == lives, "Completed round ignores further breaches");
            game.RestartRound();
            Check(game.support.AllyCount == 0 && game.support.BreachWarningUntil == 0,
                "Retry restores entry squad and clears breach warning");
        }

        private static void CheckCampaign(GameController game)
        {
            game.StartCampaign();
            float originalInterval = game.enemies.CurrentSpawnInterval;
            game.NextStage();
            Check(game.CurrentStage == 1, "Cannot skip an unfinished stage");
            GrantSupport(game.support, SupportKind.Reinforcements);
            GrantSupport(game.support, SupportKind.Weapon);
            game.support.RestoreLoadout(4, 4);
            Check(game.gun.EffectiveDamage == game.gun.damage + 2 && Mathf.Approximately(game.gun.EffectiveFireRate, game.gun.shotsPerSecond * 1.3f), "Peak weapon output is bounded");
            game.State.AwardKill(35);
            game.State.Tick(game.State.Duration - 0.001f);
            const string key = "lastline.campaign.v1";
            string previous = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            try
            {
                game.SendMessage("Update");
                Check(game.SavedStage == 2 && PlayerPrefs.HasKey(key), "Victory saves before leaving the results screen");
                game.NextStage();
                Check(game.CurrentStage == 2 && game.CompletedScore == 35 && game.support.AllyCount == 2 && game.support.WeaponLevel == 2, "Deploy banks the cleared sector once");
                game.ReturnToTitle();
                game.ResumeCampaign();
                Check(game.CurrentStage == 2 && game.CompletedScore == 35 && game.support.AllyCount == 2 &&
                    game.support.WeaponLevel == 2 && game.State.Elapsed == 0, "Continue restores the next sector and loadout");
            }
            finally
            {
                if (previous == null) PlayerPrefs.DeleteKey(key); else PlayerPrefs.SetString(key, previous);
                PlayerPrefs.Save();
            }
            Check(game.CurrentStage == 2 && game.IsRunning && game.State.Elapsed == 0 && game.State.Lives == game.startingLives,
                "Clear advances to a fresh higher stage");
            Check(game.CompletedScore == 35 && game.CampaignScore == 35 && game.State.Score == 0,
                "Advance banks stage score once");
            Check(game.support.AllyCount == 2 && game.support.WeaponLevel == 2, "Advance carries squad and weapon upgrades");
            Check(game.enemies.CurrentSpawnInterval < originalInterval, "Higher stage increases actual spawn pressure");
            var tank = game.enemies.SpawnTank(new Vector3(0, 1, 12));
            Check(tank.Health == game.enemies.tankHealth + 4 && tank.MaxHealth == tank.Health,
                "Higher stage tank has increased actual armor");
            var before = tank.transform.position;
            var dust = tank.transform.Find("Track Dust 0");
            Check(dust != null && dust.localScale == Vector3.zero, "Tank starts with a clear dust trail");
            tank.SendMessage("Update");
            Check(dust.localScale.x > 0 && dust.localPosition.z > 1, "Moving tracks leave dust behind the hull");
            Check(before.z - tank.transform.position.z > game.enemies.tankSpeed * Time.deltaTime,
                "Higher stage tank moves faster");
            game.NextStage();
            Check(game.CurrentStage == 2 && game.CompletedScore == 35, "Repeated advance cannot double-bank or skip stage");
            GrantSupport(game.support, SupportKind.Reinforcements);
            GrantSupport(game.support, SupportKind.Weapon);
            for (int i = 0; i < game.startingLives; i++) game.EnemyBreached();
            game.NextStage();
            Check(game.CurrentStage == 2 && !game.IsRunning, "Defeat cannot advance");
            game.RestartRound();
            Check(game.CurrentStage == 2 && game.CompletedScore == 35 && game.State.Score == 0 &&
                game.support.AllyCount == 2 && game.support.WeaponLevel == 2 && game.enemies.ActiveCount == 0,
                "Retry restores this stage's entry loadout and clears combat");
            game.ReturnToTitle();
            Check(game.IsTitle && !game.IsRunning && game.CurrentStage == 1 && game.CampaignScore == 0 &&
                game.support.AllyCount == 0 && game.support.WeaponLevel == 1 && game.enemies.ActiveCount == 0,
                "Returning to title ends operation and clears battle state");
            game.StartCampaign();
            Check(game.CurrentStage == 1 && game.IsRunning && game.CompletedScore == 0 &&
                game.enemies.CurrentSpawnInterval == originalInterval, "New operation restores stage one difficulty");
            Check(game.presentation != null && game.transform.Find("Field Environment/Textured Terrain") != null,
                "Grounded battlefield generated for existing scene");
        }

        private static void CheckSectorWaves(GameController game)
        {
            game.StartCampaign();
            for (int sector = 1; sector <= 6; sector++)
            {
                game.State.Tick(game.enemies.FirstTankAt + 1.1f);
                game.enemies.SendMessage("Update");
                int armor = 0;
                foreach (var tank in game.enemies.Tanks) if (tank.IsAlive) armor++;
                Check(armor == (sector == 1 ? 0 : sector >= 5 ? 2 : 1), "Scheduled armor in sector " + sector);
                game.support.SendMessage("Update");
                Check(game.support.TargetActive && game.support.TargetKind ==
                    (sector == 3 ? SupportKind.Drone : SupportKind.Reinforcements), "Opening support in sector " + sector);
                if (sector >= 4)
                {
                    game.RestartRound();
                    game.State.Tick(4.5f);
                    game.enemies.SendMessage("Update");
                    Check(game.enemies.ActiveCount == (sector >= 6 ? 2 : 1), "Counterattack wave in sector " + sector);
                }
                game.State.Tick(game.roundSeconds + 1);
                game.NextStage();
            }
            game.ReturnToTitle();
        }

        private static void GrantSupport(SupportTargets support, SupportKind kind)
        {
            Check(support.SpawnTarget(kind, new Vector3(0, 1, 8)), "Reward slot reusable: " + kind);
            support.DamageTarget(999);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Last Line check failed: " + message);
        }
    }
}

using UnityEngine;

namespace LastLine
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameController : MonoBehaviour
    {
        [Header("Round")]
        [Min(1)] public int startingLives = 10;
        [Min(1)] public float roundSeconds = 60;
        [Header("Scene references (assigned by scene generator)")]
        public EnemySpawner enemies;
        public MachineGun gun;
        public GameHud hud;
        public CombatEffects effects;
        public ShooterController shooter;
        public SupportTargets support;
        public BattlefieldPresentation presentation;

        public RoundState State { get; private set; }
        public string StartupError { get; private set; }
        public bool IsTitle { get; private set; } = true;
        public int CurrentStage { get; private set; } = 1;
        public int CompletedScore { get; private set; }
        public int CampaignScore => CompletedScore + (IsTitle || State == null ? 0 : State.Score);
        public StageDifficulty Difficulty => new StageDifficulty(CurrentStage);
        public bool IsRunning => !IsTitle && State != null && State.IsRunning;
        private int checkpointAllies, checkpointWeapon = 1;
        private const string SaveKey = "lastline.campaign.v1";
        [System.Serializable]
        private sealed class Checkpoint
        {
            public int stage, score, allies, weapon;
        }
        private Checkpoint saved;
        public int SavedStage => saved == null ? 0 : saved.stage;

        private void Start()
        {
            // Keep diagnostics visible even when runtime geometry fails before title setup.
            if (hud != null) hud.Initialize(this);
            try { InitializeGame(); }
            catch (System.Exception exception)
            {
                StartupError = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void InitializeGame()
        {
            if (enemies == null || gun == null || hud == null)
            {
                throw new System.InvalidOperationException("Last Line: generate the scene via Last Line/Create Minimal Scene.");
            }
            enemies.Initialize(this);
            gun.Initialize(this, enemies);
            hud.Initialize(this);
            if (shooter == null) shooter = gun.GetComponent<ShooterController>();
            if (shooter == null) shooter = gun.gameObject.AddComponent<ShooterController>();
            shooter.Initialize(this, gun);
            // Existing scenes work immediately; new scenes serialize this component.
            if (effects == null) effects = GetComponent<CombatEffects>();
            if (effects == null) effects = gameObject.AddComponent<CombatEffects>();
            effects.Initialize(gun.muzzle, gun.bulletPrefab.GetComponent<Renderer>().sharedMaterial,
                enemies.prefab.GetComponent<Renderer>().sharedMaterial);
            effects.InitializeAudio(this);
            if (presentation == null) presentation = GetComponent<BattlefieldPresentation>();
            if (presentation == null) presentation = gameObject.AddComponent<BattlefieldPresentation>();
            presentation.Initialize(this);
            if (support == null) support = GetComponent<SupportTargets>();
            if (support == null) support = gameObject.AddComponent<SupportTargets>();
            support.Initialize(this);
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try { saved = JsonUtility.FromJson<Checkpoint>(PlayerPrefs.GetString(SaveKey)); }
                catch (System.ArgumentException) { saved = null; }
                if (saved != null && (saved.stage < 2 || saved.stage > 999 || saved.score < 0 ||
                    saved.allies < 0 || saved.allies > SupportTargets.MaxAllies ||
                    saved.weapon < 1 || saved.weapon > SupportTargets.MaxWeaponLevel)) saved = null;
            }
            ReturnToTitle();
        }

        private void Update()
        {
            // Deadline wins over movement on the frame reaching 60 seconds.
            if (!IsRunning) return;
            State.Tick(Time.deltaTime);
            if (State.IsVictory)
            {
                saved = new Checkpoint { stage = Mathf.Min(999, CurrentStage + 1), score = CampaignScore,
                    allies = support.AllyCount, weapon = support.WeaponLevel };
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saved));
                PlayerPrefs.Save();
            }
        }

        public void ResumeCampaign()
        {
            if (!IsTitle || saved == null) return;
            CurrentStage = saved.stage;
            CompletedScore = saved.score;
            checkpointAllies = saved.allies;
            checkpointWeapon = saved.weapon;
            RestartRound();
        }

        public void RestartRound()
        {
            IsTitle = false;
            gun.gameObject.SetActive(true);
            enemies.ApplyStage(CurrentStage);
            ClearBattle();
            support.RestoreLoadout(checkpointAllies, checkpointWeapon);
            State = new RoundState(Mathf.Max(1, startingLives), Mathf.Max(1, roundSeconds));
            presentation.SetStage(CurrentStage);
        }

        private void ClearBattle()
        {
            if (effects != null) effects.Clear();
            enemies.ResetPool();
            gun.ResetPool();
            if (shooter != null) shooter.ResetPosition();
            if (support != null) support.ResetRound();
        }

        public void StartCampaign()
        {
            CurrentStage = 1;
            CompletedScore = checkpointAllies = 0;
            checkpointWeapon = 1;
            RestartRound();
        }

        public void NextStage()
        {
            if (IsTitle || State == null || !State.IsVictory) return;
            CompletedScore += State.Score;
            checkpointAllies = support.AllyCount;
            checkpointWeapon = support.WeaponLevel;
            CurrentStage = Mathf.Min(999, CurrentStage + 1);
            RestartRound();
        }

        public void ReturnToTitle()
        {
            ClearBattle();
            IsTitle = true;
            CurrentStage = 1;
            CompletedScore = checkpointAllies = 0;
            checkpointWeapon = 1;
            State = new RoundState(Mathf.Max(1, startingLives), Mathf.Max(1, roundSeconds));
            gun.gameObject.SetActive(false);
            presentation.SetStage(1);
        }

        public void EnemyKilled(int points, Vector3 position)
        {
            if (!IsRunning) return;
            State.AwardKill(points);
            effects.PlayKill(position);
        }
        public void EnemyBreached()
        {
            if (!IsRunning) return;
            if (support != null) support.ApplyBreachLoss();
            State.Breach();
        }

        public bool TryHitShot(Vector3 start, Vector3 end, float radius, int damage)
        {
            if (!IsRunning) return false;
            bool hitEnemy = enemies.TryFindHit(start, end, radius, out EnemyAgent enemy, out float enemyT);
            if (support != null && support.TryFindHit(start, end, radius, out float supportT) && (!hitEnemy || supportT < enemyT))
            {
                support.DamageTarget(damage);
                return true;
            }
            if (!hitEnemy) return false;
            enemy.TakeDamage(damage);
            return true;
        }
    }
}

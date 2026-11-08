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

        public RoundState State { get; private set; }
        public bool IsRunning => State != null && State.IsRunning;

        private void Start()
        {
            if (enemies == null || gun == null || hud == null)
            {
                Debug.LogError("Last Line: generate the scene via Last Line/Create Minimal Scene.", this);
                enabled = false;
                return;
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
            RestartRound();
        }

        private void Update()
        {
            // Deadline wins over movement on the frame reaching 60 seconds.
            if (IsRunning) State.Tick(Time.deltaTime);
        }

        public void RestartRound()
        {
            if (effects != null) effects.Clear();
            enemies.ResetPool();
            gun.ResetPool();
            if (shooter != null) shooter.ResetPosition();
            State = new RoundState(Mathf.Max(1, startingLives), Mathf.Max(1, roundSeconds));
        }

        public void EnemyKilled(int points, Vector3 position)
        {
            if (!IsRunning) return;
            State.AwardKill(points);
            effects.PlayKill(position);
        }
        public void EnemyBreached() { State.Breach(); }
    }
}

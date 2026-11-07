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
            RestartRound();
        }

        private void Update()
        {
            // Deadline wins over movement on the frame reaching 60 seconds.
            if (IsRunning) State.Tick(Time.deltaTime);
        }

        public void RestartRound()
        {
            enemies.ResetPool();
            gun.ResetPool();
            State = new RoundState(Mathf.Max(1, startingLives), Mathf.Max(1, roundSeconds));
        }

        public void EnemyKilled(int points) { State.AwardKill(points); }
        public void EnemyBreached() { State.Breach(); }
    }
}

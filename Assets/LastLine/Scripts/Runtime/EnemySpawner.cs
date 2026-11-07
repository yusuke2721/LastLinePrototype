using UnityEngine;

namespace LastLine
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        public EnemyAgent prefab;
        [Range(1, 100)] public int capacity = 100;
        [Header("Spawn rate: first / middle / last third of the round")]
        public bool increaseOverTime = true;
        [Min(0.05f)] public float spawnInterval = 0.65f;
        [Min(0.05f)] public float middleSpawnInterval = 0.3f;
        [Min(0.05f)] public float finalSpawnInterval = 0.1f;
        [Min(0.1f)] public float spawnHalfWidth = 5.5f;
        public float spawnZ = 25;
        public float defenseZ = 0;
        private EnemyAgent[] pool;
        private GameController game;
        private float countdown;
        public float CurrentSpawnInterval => game != null && game.State != null && increaseOverTime
            ? CombatRules.GetSpawnInterval(game.State.Elapsed, game.State.Duration,
                spawnInterval, middleSpawnInterval, finalSpawnInterval)
            : Mathf.Max(0.05f, spawnInterval);

        public int ActiveCount
        {
            get
            {
                int count = 0;
                if (pool != null) foreach (var enemy in pool) if (enemy.IsAlive) count++;
                return count;
            }
        }

        public void Initialize(GameController controller)
        {
            game = controller;
            pool = new EnemyAgent[Mathf.Clamp(capacity, 1, 100)];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(prefab, transform);
                pool[i].name = "Enemy " + i;
                pool[i].Despawn();
            }
        }

        private void Update()
        {
            if (game == null || !game.IsRunning) return;
            countdown -= Time.deltaTime;
            if (countdown > 0) return;
            // Carry frame overshoot to keep the rate accurate. At most one spawn per
            // frame prevents a large catch-up burst after a stall or a full pool.
            countdown = Mathf.Max(0, countdown + CurrentSpawnInterval);
            Spawn(new Vector3(Random.Range(-spawnHalfWidth, spawnHalfWidth), 1, spawnZ));
        }

        public EnemyAgent Spawn(Vector3 position)
        {
            if (pool == null || !game.IsRunning) return null;
            foreach (var enemy in pool)
            {
                if (enemy.IsAlive) continue;
                enemy.Launch(this, game, position);
                return enemy;
            }
            return null; // Hard cap: never Instantiate during combat.
        }

        public bool TryHit(Vector3 start, Vector3 end, float bulletRadius, int damage)
        {
            EnemyAgent nearest = null;
            float nearestT = float.PositiveInfinity;
            foreach (var enemy in pool)
            {
                if (!enemy.IsAlive) continue;
                Vector3 p = enemy.transform.position;
                if (CombatRules.SegmentCircle(start.x, start.z, end.x, end.z,
                    p.x, p.z, enemy.hitRadius + bulletRadius, out float t) && t < nearestT)
                {
                    nearestT = t;
                    nearest = enemy;
                }
            }
            if (nearest == null) return false;
            nearest.TakeDamage(damage);
            return true;
        }

        public void ResetPool()
        {
            if (pool != null) foreach (var enemy in pool) enemy.Despawn();
            countdown = 0;
        }
    }
}

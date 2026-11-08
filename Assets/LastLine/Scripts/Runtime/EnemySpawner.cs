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
        [Header("Occasional heavy tank")]
        public bool tanksEnabled = true;
        [Min(0)] public float firstTankSeconds = 12;
        [Min(1)] public float tankInterval = 14;
        [Range(1, 5)] public int tankCapacity = 3;
        [Min(2)] public int tankHealth = 18;
        [Min(0.1f)] public float tankSpeed = 1.2f;
        [Min(0.1f)] public float tankHitRadius = 1.05f;
        [Min(1)] public int tankScore = 100;
        private EnemyAgent[] pool;
        private EnemyAgent[] tankPool;
        private Material uniform, skin, equipment, armor;
        private GameController game;
        private float countdown;
        private float nextTankAt;
        private StageDifficulty difficulty = new StageDifficulty(1);
        public float InfantrySpeedScale => difficulty.InfantrySpeed;
        public float FirstTankAt => Mathf.Max(0, firstTankSeconds / difficulty.TankPressure);
        public System.Collections.Generic.IReadOnlyList<EnemyAgent> Tanks => tankPool;
        public float CurrentSpawnInterval => game != null && game.State != null && increaseOverTime
            ? CombatRules.GetSpawnInterval(game.State.Elapsed, game.State.Duration,
                spawnInterval, middleSpawnInterval, finalSpawnInterval)
                / difficulty.SpawnPressure
            : Mathf.Max(0.05f, spawnInterval) / difficulty.SpawnPressure;

        public void ApplyStage(int stage)
        {
            difficulty = new StageDifficulty(stage);
            StagePalette palette = StagePalette.ForStage(stage);
            if (uniform != null) uniform.color = palette.Uniform;
            if (armor != null) armor.color = palette.Armor;
            if (pool != null) foreach (var enemy in pool) enemy.ConfigureStage(difficulty.InfantryArmor, difficulty.InfantrySpeed);
            if (tankPool != null) foreach (var tank in tankPool) tank.ConfigureStage(difficulty.TankArmor, difficulty.TankSpeed);
        }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                if (pool != null) foreach (var enemy in pool) if (enemy.IsAlive) count++;
                if (tankPool != null) foreach (var enemy in tankPool) if (enemy.IsAlive) count++;
                return count;
            }
        }

        public void Initialize(GameController controller)
        {
            game = controller;
            Material original = prefab.GetComponent<Renderer>().sharedMaterial;
            uniform = ColoredMaterial(original, "Soldier / Stage Uniform", StagePalette.ForStage(1).Uniform);
            skin = ColoredMaterial(original, "Soldier / Skin", new Color(0.72f, 0.55f, 0.4f));
            equipment = ColoredMaterial(original, "Equipment / Charcoal", new Color(0.12f, 0.15f, 0.12f));
            armor = ColoredMaterial(original, "Tank / Stage Armor", StagePalette.ForStage(1).Armor);
            pool = new EnemyAgent[Mathf.Clamp(capacity, 1, 100)];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(prefab, transform);
                pool[i].name = "Enemy " + i;
                pool[i].DressSoldier(uniform, skin, equipment);
                pool[i].Despawn();
            }
            tankPool = new EnemyAgent[Mathf.Clamp(tankCapacity, 1, 5)];
            for (int i = 0; i < tankPool.Length; i++)
            {
                tankPool[i] = Instantiate(prefab, transform);
                tankPool[i].name = "Tank " + i;
                tankPool[i].BuildTank(armor, equipment, tankHealth, tankSpeed, tankHitRadius, tankScore);
                tankPool[i].Despawn();
            }
            nextTankAt = Mathf.Max(0, firstTankSeconds);
        }

        private void Update()
        {
            if (game == null || !game.IsRunning) return;
            if (tanksEnabled && game.State.Elapsed >= nextTankAt)
            {
                float width = Mathf.Max(0, spawnHalfWidth - 1.1f);
                SpawnTank(new Vector3(Random.Range(-width, width), 1, spawnZ));
                nextTankAt = game.State.Elapsed + Mathf.Max(1, tankInterval / difficulty.TankPressure);
            }
            // Short breaks in the opening sector let the player leave the firing lane for support.
            if (game.CurrentStage == 1 && game.State.Elapsed % 8f >= 5f) return;
            countdown -= Time.deltaTime;
            if (countdown > 0) return;
            // Carry frame overshoot to keep the rate accurate. At most one spawn per
            // frame prevents a large catch-up burst after a stall or a full pool.
            countdown = Mathf.Max(0, countdown + CurrentSpawnInterval);
            float lane;
            if (game.CurrentStage == 3)
            {
                float side = Mathf.FloorToInt(game.State.Elapsed / 5f) % 2 == 0 ? -1f : 1f;
                lane = side * Random.Range(spawnHalfWidth * 0.25f, spawnHalfWidth);
            }
            else lane = Random.Range(-spawnHalfWidth, spawnHalfWidth);
            Spawn(new Vector3(lane, 1, spawnZ));
        }

        public EnemyAgent Spawn(Vector3 position)
        {
            return SpawnFrom(pool, position);
        }

        public EnemyAgent SpawnTank(Vector3 position)
        {
            return SpawnFrom(tankPool, position);
        }

        private EnemyAgent SpawnFrom(EnemyAgent[] source, Vector3 position)
        {
            if (source == null || !game.IsRunning || ActiveCount >= Mathf.Clamp(capacity, 1, 100)) return null;
            foreach (var enemy in source)
            {
                if (enemy.IsAlive) continue;
                enemy.Launch(this, game, position);
                return enemy;
            }
            return null; // Hard cap: never Instantiate during combat.
        }

        public bool TryHit(Vector3 start, Vector3 end, float bulletRadius, int damage)
        {
            if (!TryFindHit(start, end, bulletRadius, out EnemyAgent nearest, out float nearestT)) return false;
            nearest.TakeDamage(damage);
            return true;
        }

        public bool TryFindHit(Vector3 start, Vector3 end, float bulletRadius, out EnemyAgent nearest, out float nearestT)
        {
            nearest = null;
            nearestT = float.PositiveInfinity;
            FindNearest(pool, start, end, bulletRadius, ref nearest, ref nearestT);
            FindNearest(tankPool, start, end, bulletRadius, ref nearest, ref nearestT);
            return nearest != null;
        }

        public EnemyAgent FindStrikeTarget()
        {
            // Prefer armor, then the closest infantry threat to the defense line.
            EnemyAgent target = ClosestToLine(tankPool);
            return target != null ? target : ClosestToLine(pool);
        }

        private static EnemyAgent ClosestToLine(EnemyAgent[] source)
        {
            EnemyAgent nearest = null;
            if (source != null) foreach (var enemy in source)
                if (enemy.IsAlive && (nearest == null || enemy.transform.position.z < nearest.transform.position.z)) nearest = enemy;
            return nearest;
        }

        public void DamageArea(Vector3 center, float radius, int damage)
        {
            if (game == null || !game.IsRunning || damage <= 0) return;
            DamageArea(pool, center, radius, damage);
            DamageArea(tankPool, center, radius, damage);
        }

        private static void DamageArea(EnemyAgent[] source, Vector3 center, float radius, int damage)
        {
            if (source == null) return;
            foreach (var enemy in source)
            {
                if (!enemy.IsAlive) continue;
                Vector3 offset = enemy.transform.position - center;
                float reach = Mathf.Max(0, radius) + enemy.hitRadius;
                if (offset.x * offset.x + offset.z * offset.z <= reach * reach) enemy.TakeDamage(damage);
            }
        }

        private static void FindNearest(EnemyAgent[] source, Vector3 start, Vector3 end, float bulletRadius,
            ref EnemyAgent nearest, ref float nearestT)
        {
            if (source == null) return;
            foreach (var enemy in source)
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
        }

        public void ResetPool()
        {
            if (pool != null) foreach (var enemy in pool) enemy.Despawn();
            if (tankPool != null) foreach (var enemy in tankPool) enemy.Despawn();
            countdown = 0;
            nextTankAt = FirstTankAt;
        }

        private static Material ColoredMaterial(Material source, string name, Color color)
        {
            var material = new Material(source) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
            return material;
        }

        private void OnDestroy()
        {
            // Four shared materials per spawner, rather than one material per body part.
            if (uniform != null) Destroy(uniform);
            if (skin != null) Destroy(skin);
            if (equipment != null) Destroy(equipment);
            if (armor != null) Destroy(armor);
        }
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace LastLine
{
    public sealed class MachineGun : MonoBehaviour
    {
        public Transform muzzle;
        public Bullet bulletPrefab;
        [Tooltip("Camera used by ShooterController to map pointer position to a movement lane.")]
        public Camera aimCamera;
        [Min(1)] public int poolCapacity = 32;
        [Min(0.1f)] public float shotsPerSecond = 2;
        [Min(1)] public int damage = 1;

        [Tooltip("Optional: assign AudioSource.Play or another response in the Inspector.")]
        public UnityEvent onShot = new UnityEvent();
        private Bullet[] pool;
        private GameController game;
        private EnemySpawner enemies;
        private float cooldown;
        public float EffectiveFireRate => shotsPerSecond * (1 + 0.1f * ((game != null && game.support != null ? game.support.WeaponLevel : 1) - 1))
            * (game != null && game.State != null ? game.State.FireRateMultiplier : 1f);
        public int EffectiveDamage => damage + (game != null && game.support != null ? game.support.WeaponLevel / 2 : 0);
        public int ActiveBulletCount
        {
            get
            {
                int count = 0;
                if (pool != null) foreach (var bullet in pool) if (bullet.IsFlying) count++;
                return count;
            }
        }

        public void Initialize(GameController controller, EnemySpawner spawner)
        {
            game = controller;
            enemies = spawner;
            int supportCapacity = Mathf.CeilToInt(shotsPerSecond * 1.3f * 2f * (SupportTargets.MaxAllies + 1) * bulletPrefab.lifetime) + 5;
            pool = new Bullet[Mathf.Clamp(Mathf.Max(poolCapacity, supportCapacity), 1, 256)];
            // Bullets stay independent of the moving shooter after firing.
            var root = new GameObject("Bullet Pool");
            root.transform.SetParent(controller.transform, false);
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = Instantiate(bulletPrefab, root.transform);
                pool[i].name = "Bullet " + i;
                pool[i].Despawn();
            }
        }

        private void Update()
        {
            if (game == null || !game.IsRunning) return;

            cooldown -= Time.deltaTime;
            // Bounded catch-up preserves the configured rate without a stall burst.
            int fired = 0;
            while (cooldown <= 0 && fired < 3)
            {
                Fire();
                cooldown += 1f / Mathf.Max(0.1f, EffectiveFireRate);
                fired++;
            }
            if (cooldown < 0) cooldown = 0;
        }

        private void Fire()
        {
            if (FireFrom(muzzle.position))
            {
                game.effects.ShowMuzzleFlash();
                onShot.Invoke();
            }
            int allies = game.support == null ? 0 : game.support.AllyCount;
            for (int i = 0; i < allies; i++) FireFrom(game.support.AllyMuzzle(i));
        }

        private bool FireFrom(Vector3 position)
        {
            foreach (var bullet in pool)
            {
                if (bullet.IsFlying) continue;
                bullet.Launch(game, enemies, position, Vector3.forward, EffectiveDamage);
                return true;
            }
            return false;
        }

        public void ResetPool()
        {
            if (pool != null) foreach (var bullet in pool) bullet.Despawn();
            cooldown = 0.1f; // A small delay prevents the retry click immediately shooting.
            transform.rotation = Quaternion.identity;
        }
    }
}

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
        [Min(0.1f)] public float shotsPerSecond = 6;
        [Min(1)] public int damage = 1;

        [Tooltip("Optional: assign AudioSource.Play or another response in the Inspector.")]
        public UnityEvent onShot = new UnityEvent();
        private Bullet[] pool;
        private GameController game;
        private EnemySpawner enemies;
        private float cooldown;

        public void Initialize(GameController controller, EnemySpawner spawner)
        {
            game = controller;
            enemies = spawner;
            pool = new Bullet[Mathf.Clamp(poolCapacity, 1, 256)];
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
            // Bounded catch-up preserves 10 Hz at typical frame rates, without a stall burst.
            int fired = 0;
            while (cooldown <= 0 && fired < 3)
            {
                Fire();
                cooldown += 1f / Mathf.Max(0.1f, shotsPerSecond);
                fired++;
            }
            if (cooldown < 0) cooldown = 0;
        }

        private void Fire()
        {
            foreach (var bullet in pool)
            {
                if (bullet.IsFlying) continue;
                bullet.Launch(game, enemies, muzzle.position, Vector3.forward, damage);
                game.effects.ShowMuzzleFlash();
                onShot.Invoke();
                return;
            }
        }

        public void ResetPool()
        {
            if (pool != null) foreach (var bullet in pool) bullet.Despawn();
            cooldown = 0.1f; // A small delay prevents the retry click immediately shooting.
            transform.rotation = Quaternion.identity;
        }
    }
}

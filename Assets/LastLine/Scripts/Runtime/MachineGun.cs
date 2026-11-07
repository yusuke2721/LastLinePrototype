using UnityEngine;
using UnityEngine.Events;

namespace LastLine
{
    public sealed class MachineGun : MonoBehaviour
    {
        public Transform muzzle;
        public Bullet bulletPrefab;
        public Camera aimCamera;
        [Min(1)] public int poolCapacity = 32;
        [Min(0.1f)] public float shotsPerSecond = 10;
        [Min(1)] public int damage = 1;
        [Range(1, 60)] public float maxAimDegrees = 25;
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
            // Pool under a separate object: bullets must not rotate with the turret.
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
            if (PointerAim.TryGetPosition(out Vector2 pointer) && aimCamera.pixelRect.Contains(pointer))
            {
                float x = Mathf.InverseLerp(aimCamera.pixelRect.xMin, aimCamera.pixelRect.xMax, pointer.x);
                transform.rotation = Quaternion.Euler(0, Mathf.Lerp(-maxAimDegrees, maxAimDegrees, x), 0);
            }
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
                bullet.Launch(game, enemies, muzzle.position, transform.forward, damage);
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

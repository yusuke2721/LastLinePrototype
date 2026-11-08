using UnityEngine;

namespace LastLine
{
    [DefaultExecutionOrder(100)]
    public sealed class Bullet : MonoBehaviour
    {
        [Min(1)] public float speed = 40;
        [Min(0.1f)] public float lifetime = 1.2f;
        [Min(0)] public float radius = 0.1f;
        private EnemySpawner enemies;
        private GameController game;
        private Vector3 direction;
        private float remaining;
        private int damage;
        public bool IsFlying { get; private set; }

        public void Launch(GameController controller, EnemySpawner spawner,
            Vector3 position, Vector3 forward, int shotDamage)
        {
            game = controller;
            enemies = spawner;
            direction = forward.normalized;
            damage = shotDamage;
            remaining = lifetime;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            IsFlying = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsFlying || !game.IsRunning) return;
            float step = Mathf.Min(Time.deltaTime, remaining);
            Vector3 start = transform.position;
            Vector3 end = start + direction * (speed * step);
            if (game.TryHitShot(start, end, radius, damage)) { Despawn(); return; }
            transform.position = end;
            remaining -= step;
            if (remaining <= 0) Despawn();
        }

        public void Despawn()
        {
            IsFlying = false;
            gameObject.SetActive(false);
        }
    }
}

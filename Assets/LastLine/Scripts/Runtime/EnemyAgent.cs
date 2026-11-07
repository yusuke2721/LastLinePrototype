using UnityEngine;

namespace LastLine
{
    public sealed class EnemyAgent : MonoBehaviour
    {
        [Min(1)] public int maxHealth = 1;
        [Min(0.1f)] public float moveSpeed = 2.3f;
        [Min(0.1f)] public float hitRadius = 0.5f;
        [Min(1)] public int scoreValue = 10;

        private EnemySpawner owner;
        private GameController game;
        private int health;
        private Vector3 target;
        public bool IsAlive { get; private set; }
        public int Health => health;

        public void Launch(EnemySpawner spawner, GameController controller, Vector3 position)
        {
            owner = spawner;
            game = controller;
            health = Mathf.Max(1, maxHealth);
            transform.position = position;
            target = new Vector3(position.x * 0.35f, position.y, owner.defenseZ - 2);
            IsAlive = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsAlive || !game.IsRunning) return;
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            if (transform.position.z <= owner.defenseZ)
            {
                Despawn();
                game.EnemyBreached();
            }
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || !game.IsRunning || damage <= 0) return;
            health -= damage;
            if (health > 0) return;
            game.EnemyKilled(scoreValue);
            // A small optional hit effect can be added here after the minimal build is checked.
            Despawn();
        }

        public void Despawn()
        {
            IsAlive = false;
            gameObject.SetActive(false);
        }
    }
}

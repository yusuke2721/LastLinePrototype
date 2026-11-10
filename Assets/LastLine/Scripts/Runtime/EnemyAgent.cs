using UnityEngine;
using UnityEngine.Rendering;

namespace LastLine
{
    public sealed class EnemyAgent : MonoBehaviour
    {
        [Min(1)] public int maxHealth = 1;
        [Min(0.1f)] public float moveSpeed = 2.3f;
        [Min(0.1f)] public float hitRadius = 0.5f;
        [Min(1)] public int scoreValue = 10;

        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        private EnemySpawner owner;
        private GameController game;
        private int health;
        private float stride;
        private bool isTank;
        private Transform tankBody;
        private Transform[] trackDust;
        private float dustTravel;
        private float armorHitUntil;
        private int armorBonus;
        private float speedScale = 1;

        public bool IsAlive { get; private set; }
        public int Health => health;
        public int MaxHealth => Mathf.Max(1, maxHealth + armorBonus);
        public bool IsTank => isTank;
        public bool ArmorHitVisible => isTank && Time.time < armorHitUntil;
        public float SecondsToBreach => Mathf.Max(0, transform.position.z - owner.defenseZ)
            / Mathf.Max(0.01f, moveSpeed * speedScale);
        public void ConfigureStage(int extraArmor, float speedMultiplier)
        {
            armorBonus = Mathf.Max(0, extraArmor);
            speedScale = Mathf.Max(0.1f, speedMultiplier);
        }

        public void DressSoldier(Material uniform, Material skin, Material equipment)
        {
            BuildVisuals(uniform);
            foreach (var part in visualRoot.GetComponentsInChildren<Renderer>(true))
                part.sharedMaterial = part.name == "Head" ? skin : uniform;
            if (visualRoot.Find("Helmet") != null) return;
            Part("Helmet", PrimitiveType.Sphere, visualRoot, new Vector3(0, 0.79f, 0),
                new Vector3(0.5f, 0.28f, 0.5f), uniform);
            Part("Vest", PrimitiveType.Cube, visualRoot, new Vector3(0, 0.12f, 0.23f),
                new Vector3(0.49f, 0.5f, 0.12f), equipment);
            Part("Rifle", PrimitiveType.Cube, visualRoot, new Vector3(0.32f, -0.06f, 0.4f),
                new Vector3(0.12f, 0.15f, 0.85f), equipment);
            Part("Pack", PrimitiveType.Cube, visualRoot, new Vector3(0, 0.1f, -0.28f), new Vector3(0.46f, 0.5f, 0.22f), equipment);
            Part("Goggles", PrimitiveType.Cube, visualRoot, new Vector3(0, 0.72f, 0.2f), new Vector3(0.33f, 0.1f, 0.08f), equipment);
            Part("Left Boot", PrimitiveType.Cube, leftLeg, new Vector3(0, -0.61f, 0.06f), new Vector3(0.25f, 0.14f, 0.4f), equipment);
            Part("Right Boot", PrimitiveType.Cube, rightLeg, new Vector3(0, -0.61f, 0.06f), new Vector3(0.25f, 0.14f, 0.4f), equipment);
            Part("Barrel", PrimitiveType.Cylinder, visualRoot, new Vector3(0.32f, -0.06f, 0.9f),
                new Vector3(0.07f, 0.25f, 0.07f), equipment).localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = -1; i <= 1; i += 2)
                Part("Magazine Pouch", PrimitiveType.Cube, visualRoot, new Vector3(i * 0.13f, -0.02f, 0.32f), new Vector3(0.15f, 0.2f, 0.1f), uniform);
            BattlefieldPresentation.FitSoldier(visualRoot, uniform, equipment, true);
        }

        public void BuildTank(Material armor, Material tracks, int hp, float speed, float radius, int points, Material dust = null)
        {
            isTank = true;
            maxHealth = Mathf.Max(2, hp);
            moveSpeed = Mathf.Max(0.1f, speed);
            hitRadius = Mathf.Max(0.1f, radius);
            scoreValue = Mathf.Max(1, points);
            var capsule = GetComponent<Renderer>();
            if (capsule != null) capsule.enabled = false;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            var tank = new GameObject("Tank Visuals").transform;
            tank.SetParent(transform, false);
            tankBody = tank;
            if (dust != null)
            {
                trackDust = new Transform[4];
                for (int i = 0; i < trackDust.Length; i++)
                {
                    var puff = Part("Track Dust " + i, PrimitiveType.Sphere, transform, Vector3.zero, Vector3.zero, dust);
                    var renderer = puff.GetComponent<Renderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    trackDust[i] = puff;
                }
            }
            Part("Hull", PrimitiveType.Cube, tank, new Vector3(0, -0.33f, 0),
                new Vector3(1.7f, 0.55f, 2.2f), armor);
            Part("Left Track", PrimitiveType.Cube, tank, new Vector3(-0.88f, -0.63f, 0),
                new Vector3(0.4f, 0.65f, 2.5f), tracks);
            Part("Right Track", PrimitiveType.Cube, tank, new Vector3(0.88f, -0.63f, 0),
                new Vector3(0.4f, 0.65f, 2.5f), tracks);
            Part("Turret", PrimitiveType.Sphere, tank, new Vector3(0, 0.14f, -0.1f),
                new Vector3(1.25f, 0.62f, 1.35f), armor);
            Part("Cannon", PrimitiveType.Cylinder, tank, new Vector3(0, 0.16f, -1.27f),
                new Vector3(0.16f, 0.9f, 0.16f), tracks).localRotation = Quaternion.Euler(90, 0, 0);
            Part("Hatch", PrimitiveType.Cylinder, tank, new Vector3(0, 0.43f, 0),
                new Vector3(0.48f, 0.06f, 0.48f), armor);
            Part("Sloped Front Armor", PrimitiveType.Cube, tank, new Vector3(0, -0.22f, -0.85f),
                new Vector3(1.65f, 0.36f, 0.6f), armor).localRotation = Quaternion.Euler(22, 0, 0);
            Part("Muzzle Brake", PrimitiveType.Cube, tank, new Vector3(0, 0.16f, -2.2f), new Vector3(0.23f, 0.2f, 0.25f), tracks);
            Part("Antenna", PrimitiveType.Cylinder, tank, new Vector3(0.42f, 0.85f, 0.35f), new Vector3(0.02f, 0.5f, 0.02f), tracks);
            for (int side = -1; side <= 1; side += 2) for (int wheel = 0; wheel < 5; wheel++)
                Part("Road Wheel", PrimitiveType.Cylinder, tank, new Vector3(side * 1.1f, -0.64f, -0.9f + wheel * 0.45f),
                    new Vector3(0.38f, 0.07f, 0.38f), armor).localRotation = Quaternion.Euler(0, 0, 90);
        }

        public void Launch(EnemySpawner spawner, GameController controller, Vector3 position)
        {
            owner = spawner;
            game = controller;
            health = MaxHealth;
            transform.position = position;
            armorHitUntil = 0;
            dustTravel = 0;
            if (trackDust != null) foreach (var puff in trackDust) puff.localScale = Vector3.zero;
            if (tankBody != null) tankBody.localRotation = Quaternion.identity;
            ResetPose();

            IsAlive = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsAlive || !game.IsRunning) return;
            // Keep the spawn lane and height; the shooter does not affect enemy movement.
            float distance = moveSpeed * speedScale * Time.deltaTime;
            transform.position += Vector3.back * distance;
            if (isTank && tankBody != null)
            {
                stride += distance * 6;
                float recoil = Mathf.Clamp01((armorHitUntil - Time.time) / 0.18f);
                tankBody.localRotation = Quaternion.Euler(-4f * recoil, 0, Mathf.Sin(stride) * 1.2f);
                UpdateTrackDust(distance);
            }
            if (visualRoot != null && !isTank)
            {
                stride += distance * 4.5f;
                float swing = Mathf.Sin(stride) * 25;
                leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
                rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
                leftArm.localRotation = Quaternion.Euler(-swing * 0.7f, 0, -8);
                rightArm.localRotation = Quaternion.Euler(swing * 0.7f, 0, 8);
                visualRoot.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(stride)) * 0.025f);
            }
            if (transform.position.z <= owner.defenseZ)
            {
                Despawn();
                game.EnemyBreached();
            }
        }

        private void UpdateTrackDust(float distance)
        {
            if (trackDust == null) return;
            const float trailLength = 1.8f;
            dustTravel += distance;
            for (int i = 0; i < trackDust.Length; i++)
            {
                float travel = dustTravel - i * trailLength / trackDust.Length;
                if (travel < 0) continue;
                float age = Mathf.Repeat(travel / trailLength, 1f);
                float side = i % 2 == 0 ? -1f : 1f;
                var puff = trackDust[i];
                puff.localPosition = new Vector3(side * (0.88f + age * 0.22f), -0.88f + age * 0.38f,
                    1.05f + age * trailLength);
                puff.localScale = new Vector3(0.64f, 0.36f, 0.78f) * Mathf.Sin(Mathf.PI * age);
            }
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || !game.IsRunning || damage <= 0) return;
            health -= damage;
            if (isTank)
            {
                armorHitUntil = Time.time + 0.18f;
                game.effects.PlayArmorHit();
            }
            if (health > 0) return;
            game.EnemyKilled(scoreValue, transform.position);
            Despawn();
        }

        public void Despawn()
        {
            IsAlive = false;
            gameObject.SetActive(false);
        }

        // Called during pool creation (also for saved capsule prefabs), never during a spawn.
        // Keep the root renderer/material available to CombatEffects, but hide the capsule.
        public void BuildVisuals(Material uniform)
        {
            if (visualRoot != null) return;
            var capsule = GetComponent<Renderer>();
            if (capsule != null) capsule.enabled = false;
            visualRoot = new GameObject("Enemy Visuals").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localRotation = Quaternion.Euler(0, 180, 0);
            Part("Torso", PrimitiveType.Capsule, visualRoot, new Vector3(0, 0.08f, 0),
                new Vector3(0.65f, 0.35f, 0.4f), uniform);
            Part("Head", PrimitiveType.Sphere, visualRoot, new Vector3(0, 0.68f, 0),
                Vector3.one * 0.44f, uniform);
            leftLeg = Limb("Left Leg", new Vector3(-0.19f, -0.27f, 0),
                new Vector3(0.24f, 0.64f, 0.3f), uniform);
            rightLeg = Limb("Right Leg", new Vector3(0.19f, -0.27f, 0),
                new Vector3(0.24f, 0.64f, 0.3f), uniform);
            leftArm = Limb("Left Arm", new Vector3(-0.43f, 0.38f, 0),
                new Vector3(0.18f, 0.62f, 0.23f), uniform);
            rightArm = Limb("Right Arm", new Vector3(0.43f, 0.38f, 0),
                new Vector3(0.18f, 0.62f, 0.23f), uniform);
            ResetPose();
        }

        private void ResetPose()
        {
            stride = 0;
            if (visualRoot == null) return;
            visualRoot.localPosition = Vector3.zero;
            leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
            leftArm.localRotation = Quaternion.Euler(0, 0, -8);
            rightArm.localRotation = Quaternion.Euler(0, 0, 8);
        }

        private Transform Limb(string name, Vector3 pivot, Vector3 size, Material material)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(visualRoot, false);
            joint.localPosition = pivot;
            Part("Mesh", PrimitiveType.Cube, joint, Vector3.down * (size.y / 2), size, material);
            return joint;
        }

        private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return obj.transform;
        }
    }
}

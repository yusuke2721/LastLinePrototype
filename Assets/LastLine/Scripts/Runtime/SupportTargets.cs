using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace LastLine
{
    public enum SupportKind { Reinforcements = 0, Weapon = 1, Drone = 2 }

    // Allies, incoming targets and the complete drone strike are pooled at startup.
    public sealed class SupportTargets : MonoBehaviour
    {
        public const int MaxAllies = 4;
        public const int MaxWeaponLevel = 4;
        [Min(0)] public float firstTargetSeconds = 3;
        [Min(1)] public float targetInterval = 5;
        [Min(1)] public int targetHealth = 3;
        [Min(0.1f)] public float targetSpeedMultiplier = 1;
        [FormerlySerializedAs("missileDamage"), Min(1)] public int droneDamage = 24;
        [FormerlySerializedAs("missileRadius"), Min(0.1f)] public float droneRadius = 3.2f;

        private GameController game;
        private readonly Transform[] allies = new Transform[MaxAllies];
        private readonly Transform[] targets = new Transform[3];
        private readonly Material[] materials = new Material[5];
        private readonly Transform[] rotors = new Transform[4];
        private readonly Transform[] smoke = new Transform[3];
        private Transform drone, bomb, blast, flash, shockwave;
        private float nextTargetAt, phaseTime, blastRemaining;
        private int nextKind, health, queuedDrones;
        private Vector3 strikePoint, droneEntry, droneExit;
        private EnemyAgent strikeTarget;
        private enum StrikePhase { Idle, Approach, Drop, Depart }
        private StrikePhase phase;

        public int AllyCount { get; private set; }
        public int WeaponLevel { get; private set; } = 1;
        public int DroneStrikes { get; private set; }
        public int QueuedDrones => queuedDrones;
        public bool DroneActive => phase != StrikePhase.Idle;
        public bool BombVisible => bomb != null && bomb.gameObject.activeSelf;
        public bool ExplosionVisible => blast != null && blast.gameObject.activeSelf;
        public bool TargetActive => health > 0;
        public int TargetHealth => health;
        public SupportKind TargetKind { get; private set; }
        public bool ClaimedSupportThisRound { get; private set; }
        public Transform ActiveTarget => TargetActive ? targets[(int)TargetKind] : null;
        public string Notice { get; private set; }
        public float NoticeUntil { get; private set; }
        public string BreachNotice { get; private set; }
        public float BreachWarningUntil { get; private set; }

        public void Initialize(GameController controller)
        {
            game = controller;
            Material source = game.gun.bulletPrefab.GetComponent<Renderer>().sharedMaterial;
            Color[] colors = { new Color(0.2f, 0.95f, 0.65f), new Color(1f, 0.8f, 0.2f), new Color(1f, 0.4f, 0.2f) };
            var root = new GameObject("Support Pool").transform;
            root.SetParent(game.transform, false);
            for (int i = 0; i < 3; i++)
            {
                materials[i] = new Material(source) { name = "Support " + i, color = colors[i], enableInstancing = true };
                targets[i] = new GameObject(((SupportKind)i) + " Target").transform;
                targets[i].SetParent(root, false);
                Shape("Beacon", PrimitiveType.Cube, targets[i], Vector3.zero, new Vector3(1.3f, 0.18f, 1.3f), materials[i]);
                if (i == 2)
                {
                    Shape("Drone Icon", PrimitiveType.Cube, targets[i], Vector3.up * 0.3f,
                        new Vector3(0.35f, 0.18f, 0.45f), materials[i]);
                    for (int j = 0; j < 2; j++)
                        Shape("Icon Frame", PrimitiveType.Cube, targets[i], Vector3.up * 0.3f,
                            new Vector3(1, 0.09f, 0.09f), materials[i]).localRotation = Quaternion.Euler(0, j == 0 ? 45 : -45, 0);
                    for (int j = 0; j < 4; j++)
                        Shape("Icon Rotor", PrimitiveType.Cylinder, targets[i],
                            new Vector3(j % 2 == 0 ? -0.34f : 0.34f, 0.35f, j < 2 ? -0.34f : 0.34f),
                            new Vector3(0.3f, 0.025f, 0.3f), materials[i]);
                }
                else
                {
                    Shape("Marker A", PrimitiveType.Cube, targets[i], new Vector3(0, 0.28f, 0),
                        i == 0 ? new Vector3(0.7f, 0.15f, 0.2f) : new Vector3(0.18f, 0.4f, 0.8f), materials[i]);
                    Shape("Marker B", PrimitiveType.Cube, targets[i],
                        i == 1 ? new Vector3(0.35f, 0.28f, 0) : new Vector3(0, 0.28f, 0.3f),
                        i == 0 ? new Vector3(0.2f, 0.15f, 0.8f) : Vector3.one * 0.3f, materials[i]);
                }
                targets[i].gameObject.SetActive(false);
            }
            var original = game.gun.transform.Find("Shooter Visuals");
            for (int i = 0; i < MaxAllies; i++)
            {
                allies[i] = Instantiate(original, root);
                allies[i].name = "Ally " + (i + 1);
                allies[i].localScale = Vector3.one * 0.85f;
                allies[i].gameObject.SetActive(false);
            }
            materials[3] = new Material(source) { name = "Drone / Graphite", color = new Color(0.14f, 0.2f, 0.25f), enableInstancing = true };
            materials[4] = new Material(source) { name = "Blast / Smoke", color = new Color(0.3f, 0.32f, 0.35f), enableInstancing = true };
            drone = new GameObject("Support Quadcopter").transform;
            drone.SetParent(root, false);
            Shape("Fuselage", PrimitiveType.Cube, drone, Vector3.zero, new Vector3(0.65f, 0.25f, 0.85f), materials[3]);
            Shape("Camera", PrimitiveType.Sphere, drone, new Vector3(0, -0.2f, -0.3f), Vector3.one * 0.24f, materials[0]);
            for (int i = 0; i < 2; i++)
                Shape("Frame", PrimitiveType.Cube, drone, Vector3.zero, new Vector3(2, 0.1f, 0.12f), materials[3])
                    .localRotation = Quaternion.Euler(0, i == 0 ? 45 : -45, 0);
            for (int i = 0; i < rotors.Length; i++)
            {
                Vector3 position = new Vector3(i % 2 == 0 ? -0.68f : 0.68f, 0.1f, i < 2 ? -0.68f : 0.68f);
                Shape("Motor", PrimitiveType.Cylinder, drone, position, new Vector3(0.3f, 0.08f, 0.3f), materials[3]);
                rotors[i] = Shape("Rotor", PrimitiveType.Cube, drone, position + Vector3.up * 0.1f,
                    new Vector3(0.85f, 0.035f, 0.1f), materials[0]);
            }
            bomb = new GameObject("Drone Bomb").transform;
            bomb.SetParent(root, false);
            Shape("Payload", PrimitiveType.Capsule, bomb, Vector3.zero, new Vector3(0.28f, 0.32f, 0.28f), materials[2]);
            Shape("Tail", PrimitiveType.Cube, bomb, Vector3.up * 0.3f, new Vector3(0.55f, 0.1f, 0.12f), materials[3]);
            blast = new GameObject("Drone Blast").transform;
            blast.SetParent(root, false);
            flash = Shape("Fireball", PrimitiveType.Sphere, blast, Vector3.up * 0.4f, Vector3.one, materials[2]);
            shockwave = Shape("Shockwave", PrimitiveType.Cylinder, blast, Vector3.zero, Vector3.one, materials[1]);
            for (int i = 0; i < smoke.Length; i++)
                smoke[i] = Shape("Smoke", PrimitiveType.Sphere, blast, Vector3.zero, Vector3.one, materials[4]);
            drone.gameObject.SetActive(false);
            bomb.gameObject.SetActive(false);
            blast.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (game == null || !game.IsRunning) return;
            if (TargetActive)
            {
                float speed = Mathf.Max(0.1f, game.enemies.prefab.moveSpeed * game.enemies.InfantrySpeedScale * targetSpeedMultiplier);
                ActiveTarget.position += Vector3.back * (speed * Time.deltaTime);
                if (ActiveTarget.position.z <= game.enemies.defenseZ) ClearTarget();
            }
            else if (game.State.Elapsed >= nextTargetAt)
            {
                // A fixed cycle makes all three rewards available in every round.
                // Place the reward across the field from the player so aiming for it is a choice.
                float width = Mathf.Max(0, Mathf.Min(game.enemies.spawnHalfWidth - 0.8f, game.shooter.moveHalfWidth));
                float side = Mathf.Abs(game.gun.transform.position.x) < 0.1f ? (Random.value < 0.5f ? -1 : 1)
                    : -Mathf.Sign(game.gun.transform.position.x);
                float x = side * Random.Range(width * 0.6f, width);
                SpawnTarget((SupportKind)nextKind, new Vector3(x, 1.1f, game.enemies.spawnZ));
                nextKind = (nextKind + 1) % 3;
                nextTargetAt = game.State.Elapsed + Mathf.Max(1, targetInterval);
            }
            UpdateDrone();
            for (int i = 0; i < AllyCount; i++)
            {
                allies[i].position = AllyMuzzle(i) - Vector3.forward * 1.4f;
                allies[i].rotation = Quaternion.identity;
            }
        }

        public Vector3 AllyMuzzle(int index)
        {
            float offset = (index % 2 == 0 ? -1 : 1) * (index / 2 + 1) * 0.9f;
            Vector3 position = game.gun.muzzle.position + new Vector3(offset, 0, -0.35f * (index / 2 + 1));
            position.x = Mathf.Clamp(position.x, -game.shooter.moveHalfWidth, game.shooter.moveHalfWidth);
            return position;
        }

        public bool SpawnTarget(SupportKind kind, Vector3 position)
        {
            if (game == null || !game.IsRunning || TargetActive || (int)kind < 0 || (int)kind >= targets.Length) return false;
            TargetKind = kind;
            health = Mathf.Max(1, targetHealth);
            ActiveTarget.position = position;
            ActiveTarget.gameObject.SetActive(true);
            return true;
        }

        public bool TryFindHit(Vector3 start, Vector3 end, float radius, out float t)
        {
            t = 0;
            if (!TargetActive) return false;
            Vector3 p = ActiveTarget.position;
            return CombatRules.SegmentCircle(start.x, start.z, end.x, end.z, p.x, p.z, 0.8f + radius, out t);
        }

        public void DamageTarget(int damage)
        {
            if (!TargetActive || !game.IsRunning || damage <= 0) return;
            health -= damage;
            if (health > 0) return;
            // Disable before granting the reward so simultaneous bullets cannot claim it twice.
            targets[(int)TargetKind].gameObject.SetActive(false);
            ClaimedSupportThisRound = true;
            health = 0;
            switch (TargetKind)
            {
                case SupportKind.Reinforcements:
                    if (AllyCount < MaxAllies)
                    {
                        allies[AllyCount].position = AllyMuzzle(AllyCount) - Vector3.forward * 1.4f;
                        allies[AllyCount++].gameObject.SetActive(true);
                        ShowNotice("REINFORCEMENTS +1");
                    }
                    else ShowNotice("SQUAD AT FULL STRENGTH");
                    break;
                case SupportKind.Weapon:
                    WeaponLevel = Mathf.Min(MaxWeaponLevel, WeaponLevel + 1);
                    ShowNotice("WEAPON UPGRADED / LV." + WeaponLevel);
                    break;
                case SupportKind.Drone:
                    queuedDrones = Mathf.Min(3, queuedDrones + 1);
                    ShowNotice("DRONE READY / AUTO STRIKE");
                    break;
            }
        }

        private void UpdateDrone()
        {
            if (blastRemaining > 0)
            {
                blastRemaining -= Time.deltaTime;
                float progress = Mathf.Clamp01(1 - blastRemaining / 0.8f);
                float size = Mathf.Lerp(0.2f, droneRadius * 2, progress);
                shockwave.localScale = new Vector3(size, 0.025f, size);
                flash.gameObject.SetActive(progress < 0.25f);
                flash.localScale = Vector3.one * Mathf.Lerp(0.5f, droneRadius, Mathf.Clamp01(progress * 4));
                for (int i = 0; i < smoke.Length; i++)
                {
                    smoke[i].localPosition = new Vector3((i - 1) * 0.6f, 0.5f + progress * (1.6f + i * 0.4f), 0);
                    smoke[i].localScale = Vector3.one * (0.3f + progress * 1.4f);
                }
                if (blastRemaining <= 0) blast.gameObject.SetActive(false);
            }
            if (phase == StrikePhase.Idle && queuedDrones > 0)
            {
                var target = game.enemies.FindStrikeTarget();
                if (target != null)
                {
                    queuedDrones--;
                    DroneStrikes++;
                    phaseTime = 0;
                    phase = StrikePhase.Approach;
                    strikeTarget = target;
                    strikePoint = target.transform.position;
                    droneEntry = new Vector3(strikePoint.x >= 0 ? -9 : 9, 6.5f, strikePoint.z + 6);
                    drone.position = droneEntry;
                    drone.rotation = Quaternion.identity;
                    drone.gameObject.SetActive(true);
                    ShowNotice("DRONE INBOUND");
                }
            }
            if (phase == StrikePhase.Idle) return;
            for (int i = 0; i < rotors.Length; i++)
                rotors[i].localRotation = Quaternion.Euler(0, Time.time * 1800 * (i % 2 == 0 ? 1 : -1), 0);
            phaseTime += Time.deltaTime;
            if (phase == StrikePhase.Approach)
            {
                // Track the enemy during approach; the drop commits to its last position.
                if (strikeTarget != null && strikeTarget.IsAlive) strikePoint = strikeTarget.transform.position;
                float t = Mathf.Clamp01(phaseTime);
                drone.position = Vector3.Lerp(droneEntry, strikePoint + Vector3.up * 5.5f, Mathf.SmoothStep(0, 1, t));
                if (t < 1) return;
                phase = StrikePhase.Drop;
                phaseTime = 0;
                bomb.position = drone.position - Vector3.up * 0.3f;
                bomb.gameObject.SetActive(true);
                return;
            }
            if (phase == StrikePhase.Drop)
            {
                float t = Mathf.Clamp01(phaseTime / 0.6f);
                bomb.position = Vector3.Lerp(drone.position - Vector3.up * 0.3f, strikePoint, t * t);
                if (t < 1) return;
                Impact();
                phase = StrikePhase.Depart;
                phaseTime = 0;
                droneExit = drone.position + new Vector3(10, 1, -3);
                return;
            }
            float departure = Mathf.Clamp01(phaseTime / 0.7f);
            drone.position = Vector3.Lerp(strikePoint + Vector3.up * 5.5f, droneExit, departure);
            if (departure >= 1)
            {
                phase = StrikePhase.Idle;
                drone.gameObject.SetActive(false);
                strikeTarget = null;
            }
        }

        private void Impact()
        {
            bomb.gameObject.SetActive(false);
            game.enemies.DamageArea(strikePoint, droneRadius, droneDamage);
            blast.position = new Vector3(strikePoint.x, 0.1f, strikePoint.z);
            // Child shapes animate independently; the root stays at unit scale.
            blast.localScale = Vector3.one;
            flash.gameObject.SetActive(true);
            flash.localScale = Vector3.one * 0.5f;
            shockwave.localScale = new Vector3(0.2f, 0.025f, 0.2f);
            for (int i = 0; i < smoke.Length; i++)
            {
                smoke[i].localPosition = new Vector3((i - 1) * 0.6f, 0.5f, 0);
                smoke[i].localScale = Vector3.one * 0.3f;
            }
            blast.gameObject.SetActive(true);
            blastRemaining = 0.8f;
            ShowNotice("DRONE STRIKE / IMPACT");
        }

        public void ResetRound()
        {
            ClaimedSupportThisRound = false;
            ClearTarget();
            foreach (var ally in allies) if (ally != null) ally.gameObject.SetActive(false);
            AllyCount = 0;
            WeaponLevel = 1;
            queuedDrones = DroneStrikes = nextKind = 0;
            nextTargetAt = Mathf.Max(0, firstTargetSeconds);
            if (game.CurrentStage == 2 && game.enemies.tanksEnabled)
            {
                nextKind = (int)SupportKind.Drone;
                nextTargetAt = game.enemies.FirstTankAt + 1f;
            }
            phase = StrikePhase.Idle;
            phaseTime = 0;
            strikeTarget = null;
            blastRemaining = 0;
            if (drone != null) drone.gameObject.SetActive(false);
            if (bomb != null) bomb.gameObject.SetActive(false);
            if (blast != null) blast.gameObject.SetActive(false);
            Notice = null;
            NoticeUntil = 0;
            BreachNotice = null;
            BreachWarningUntil = 0;
        }

        public void ApplyBreachLoss()
        {
            if (game == null || !game.IsRunning) return;
            int before = AllyCount + 1;
            int survivors = CombatRules.SquadAfterBreach(before);
            RestoreLoadout(survivors - 1, WeaponLevel);
            BreachNotice = before > 1 ? $"LINE BREACHED / SQUAD {before} > {survivors}"
                : "LINE BREACHED / LAST SOLDIER";
            BreachWarningUntil = Time.unscaledTime + 2;
        }

        public void RestoreLoadout(int allyCount, int weaponLevel)
        {
            AllyCount = Mathf.Clamp(allyCount, 0, MaxAllies);
            WeaponLevel = Mathf.Clamp(weaponLevel, 1, MaxWeaponLevel);
            for (int i = 0; i < allies.Length; i++)
            {
                allies[i].position = AllyMuzzle(i) - Vector3.forward * 1.4f;
                allies[i].gameObject.SetActive(i < AllyCount);
            }
        }

        private void ClearTarget()
        {
            if (TargetActive) ActiveTarget.gameObject.SetActive(false);
            health = 0;
        }

        private void ShowNotice(string message) { Notice = message; NoticeUntil = Time.unscaledTime + 2; }

        private static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }

        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}

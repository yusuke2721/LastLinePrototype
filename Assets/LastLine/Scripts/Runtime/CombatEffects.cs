using UnityEngine;
using UnityEngine.Rendering;

namespace LastLine
{
    // Primitive-only feedback. All geometry is created once, then reused.
    public sealed class CombatEffects : MonoBehaviour
    {
        public bool muzzleFlashEnabled = true;
        public bool killBurstsEnabled = true;
        [Min(0.01f)] public float flashSeconds = 0.035f;
        [Range(1, 32)] public int burstCapacity = 16;
        [Min(0.05f)] public float burstSeconds = 0.32f;
        [Min(0.05f)] public float shardSize = 0.25f;

        private sealed class Burst
        {
            public GameObject root;
            public readonly Transform[] shards = new Transform[6];
            public readonly Vector3[] velocities = new Vector3[6];
            public float remaining;
            public float duration;
        }

        private Burst[] bursts;
        private GameObject flash;
        private float flashUntil;
        private GameController game;
        private AudioSource tracks, impacts;
        private AudioClip trackClip, impactClip;
        private float nextImpact;
        private const string VolumeKey = "lastline.effects.volume";
        public int VolumeStep { get; private set; }

        public void InitializeAudio(GameController controller)
        {
            game = controller;
            tracks = gameObject.AddComponent<AudioSource>();
            impacts = gameObject.AddComponent<AudioSource>();
            tracks.playOnAwake = impacts.playOnAwake = false;
            tracks.loop = true;
            trackClip = MakeTankSound(false);
            impactClip = MakeTankSound(true);
            tracks.clip = trackClip;
            VolumeStep = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, 2), 0, 4);
        }

        private static AudioClip MakeTankSound(bool impact)
        {
            const int rate = 22050;
            var samples = new float[impact ? rate / 5 : rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float phase = 2f * Mathf.PI * t;
                samples[i] = impact
                    ? Mathf.Exp(-35f * t) * (0.5f * Mathf.Sin(phase * 970) + 0.3f * Mathf.Sin(phase * 1553))
                    : 0.24f * Mathf.Sin(phase * 65) + Mathf.Exp(-6f * (t * 9f % 1f)) *
                        (0.26f * Mathf.Sin(phase * 171) + 0.16f * Mathf.Sin(phase * 324));
            }
            var clip = AudioClip.Create(impact ? "Armor strike" : "Tank tracks", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void CycleVolume()
        {
            VolumeStep = (VolumeStep + 1) % 5;
            PlayerPrefs.SetInt(VolumeKey, VolumeStep);
            PlayerPrefs.Save();
            if (VolumeStep > 0) impacts.PlayOneShot(impactClip, VolumeStep * 0.18f);
            else impacts.Stop();
        }

        public void PlayArmorHit()
        {
            if (VolumeStep == 0 || Time.time < nextImpact) return;
            nextImpact = Time.time + 0.07f;
            impacts.PlayOneShot(impactClip, VolumeStep * 0.18f);
        }

        private void UpdateTrackSound()
        {
            float nearest = float.PositiveInfinity;
            if (game.IsRunning && VolumeStep > 0)
            {
                var tanks = game.enemies.Tanks;
                for (int i = 0; i < tanks.Count; i++)
                    if (tanks[i].IsAlive) nearest = Mathf.Min(nearest, tanks[i].SecondsToBreach);
            }
            if (float.IsInfinity(nearest))
            {
                if (tracks.isPlaying) tracks.Stop();
                return;
            }
            tracks.volume = VolumeStep * 0.16f * Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(nearest / 20f));
            if (!tracks.isPlaying) tracks.Play();
        }
        public bool IsFlashVisible => flash != null && flash.activeSelf;
        public int ActiveBurstCount
        {
            get
            {
                int count = 0;
                if (bursts != null) foreach (var burst in bursts) if (burst.root.activeSelf) count++;
                return count;
            }
        }
        public int PoolSize => bursts == null ? 0 : bursts.Length;

        public void Initialize(Transform muzzle, Material shotMaterial, Material enemyMaterial)
        {
            if (bursts != null) return;
            flash = new GameObject("Muzzle Flash");
            flash.transform.SetParent(muzzle, false);
            flash.transform.localPosition = new Vector3(0, 0, 0.15f);
            CreateShard("Flash Horizontal", flash.transform, shotMaterial).localScale = new Vector3(0.65f, 0.16f, 0.5f);
            CreateShard("Flash Vertical", flash.transform, shotMaterial).localScale = new Vector3(0.16f, 0.65f, 0.5f);
            flash.SetActive(false);
            bursts = new Burst[Mathf.Clamp(burstCapacity, 1, 32)];
            for (int i = 0; i < bursts.Length; i++)
            {
                var burst = new Burst { root = new GameObject("Kill Burst " + i) };
                burst.root.transform.SetParent(transform, false);
                for (int j = 0; j < burst.shards.Length; j++)
                    burst.shards[j] = CreateShard("Shard " + j, burst.root.transform, j % 2 == 0 ? enemyMaterial : shotMaterial);
                burst.root.SetActive(false);
                bursts[i] = burst;
            }
        }

        private static Transform CreateShard(string name, Transform parent, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider); // Startup only; no physics or Instantiate/Destroy during combat.
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }

        public void ShowMuzzleFlash()
        {
            if (!muzzleFlashEnabled || flash == null) return;
            flashUntil = Time.time + Mathf.Max(0.01f, flashSeconds);
            flash.SetActive(true);
        }

        public bool PlayKill(Vector3 position)
        {
            if (!killBurstsEnabled || bursts == null) return false;
            foreach (var burst in bursts)
            {
                if (burst.root.activeSelf) continue;
                burst.duration = Mathf.Max(0.05f, burstSeconds);
                burst.remaining = burst.duration;
                burst.root.transform.position = position;
                for (int i = 0; i < burst.shards.Length; i++)
                {
                    var shard = burst.shards[i];
                    shard.localPosition = Vector3.zero;
                    shard.localRotation = Random.rotation;
                    shard.localScale = Vector3.one * shardSize;
                    var direction = Random.insideUnitSphere;
                    direction.y = Mathf.Abs(direction.y) + 0.5f;
                    burst.velocities[i] = direction.normalized * Random.Range(2f, 4f);
                }
                burst.root.SetActive(true);
                return true;
            }
            return false; // Effects may be dropped when full; gameplay is unaffected.
        }

        private void Update()
        {
            if (tracks != null) UpdateTrackSound();
            if (flash != null && flash.activeSelf && Time.time >= flashUntil) flash.SetActive(false);
            if (bursts == null) return;
            foreach (var burst in bursts)
            {
                if (!burst.root.activeSelf) continue;
                burst.remaining -= Time.deltaTime;
                if (burst.remaining <= 0) { burst.root.SetActive(false); continue; }
                for (int i = 0; i < burst.shards.Length; i++)
                {
                    burst.velocities[i] += Vector3.down * (3f * Time.deltaTime);
                    burst.shards[i].localPosition += burst.velocities[i] * Time.deltaTime;
                    burst.shards[i].localScale = Vector3.one * (shardSize * burst.remaining / burst.duration);
                }
            }
        }

        public void Clear()
        {
            if (tracks != null) tracks.Stop();
            if (impacts != null) impacts.Stop();
            nextImpact = 0;
            if (flash != null) flash.SetActive(false);
            if (bursts != null) foreach (var burst in bursts) burst.root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (trackClip != null) Destroy(trackClip);
            if (impactClip != null) Destroy(impactClip);
        }
    }
}

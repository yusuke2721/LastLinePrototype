using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastLine
{
    // Fictional stage colors, shared by equipment, terrain and operation UI.
    public readonly struct StagePalette
    {
        public readonly string Name, Sector;
        public readonly Color Accent, Uniform, Armor, Earth;
        private StagePalette(string name, string sector, uint accent, uint uniform, uint armor, uint earth)
        {
            Name = name; Sector = sector;
            Accent = Rgb(accent); Uniform = Rgb(uniform); Armor = Rgb(armor); Earth = Rgb(earth);
        }
        private static Color Rgb(uint value) => new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
        public static StagePalette ForStage(int stage)
        {
            switch ((Mathf.Max(1, stage) - 1) % 6)
            {
                case 1: return new StagePalette("CRIMSON", "INDUSTRIAL APPROACH", 0xED6C57, 0xA8322E, 0x913B32, 0x705349);
                case 2: return new StagePalette("SAND", "DUST CORRIDOR", 0xD3B477, 0x8A7952, 0x8C774C, 0x8A7651);
                case 3: return new StagePalette("NAVY", "COASTAL FORT", 0x8CB9D1, 0x3D5368, 0x394D61, 0x596166);
                case 4: return new StagePalette("ASH", "ASHEN RIDGE", 0xB4C1BD, 0x616969, 0x565C5C, 0x66665E);
                case 5: return new StagePalette("DUSK", "NIGHT OUTPOST", 0xBEA0CD, 0x5F4C67, 0x56435D, 0x615460);
                default: return new StagePalette("OLIVE", "OUTER PERIMETER", 0xA3C282, 0x576638, 0x4A5C33, 0x6B614A);
            }
        }
    }

    // A grounded procedural art pass. No downloaded art or changes to combat geometry.
    public sealed class BattlefieldPresentation : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private Material soil;
        private Texture2D soilTexture;
        private Mesh terrainMesh;
        private Light sun;
        private Camera view;

        public static string SectorName(int stage)
        {
            return StagePalette.ForStage(stage).Sector;
        }

        public void Initialize(GameController game)
        {
            var source = game.enemies.prefab.GetComponent<Renderer>().sharedMaterial;
            view = game.hud.gameCamera;
            soil = Surface(source, "Field / Earth", new Color(0.42f, 0.38f, 0.29f));
            var sand = Surface(source, "Field / Canvas", new Color(0.43f, 0.4f, 0.29f));
            var steel = Surface(source, "Field / Weathered Steel", new Color(0.18f, 0.2f, 0.19f));
            var dark = Surface(source, "Field / Trench", new Color(0.12f, 0.12f, 0.09f));
            var marking = Surface(source, "Field / Defense Marker", new Color(0.67f, 0.56f, 0.25f));
            var original = GameObject.Find("Battlefield");
            if (original != null)
                foreach (var renderer in original.GetComponentsInChildren<Renderer>())
                    if (renderer.name == "Ground" || renderer.name == "Boundary" || renderer.name == "Defense Line") renderer.enabled = false;

            var root = new GameObject("Field Environment").transform;
            root.SetParent(game.transform, false);
            MakeTerrain(root);
            for (int side = -1; side <= 1; side += 2)
            {
                Shape("Drainage Trench", PrimitiveType.Cube, root, new Vector3(side * 7.1f, -0.01f, 12), new Vector3(0.7f, 0.04f, 34), dark);
                for (int row = 0; row < 6; row++)
                {
                    float z = row * 5;
                    for (int bag = 0; bag < 5; bag++)
                        Shape("Sandbag", PrimitiveType.Sphere, root,
                            new Vector3(side * (7.8f + (bag % 3) * 0.45f), bag < 3 ? 0.18f : 0.45f, z),
                            new Vector3(0.7f, 0.35f, 0.65f), sand);
                    if (row % 2 == 0)
                        for (int beam = 0; beam < 3; beam++)
                            Shape("Steel Obstacle", PrimitiveType.Cube, root, new Vector3(side * 9.5f, 0.6f, z + 2),
                                new Vector3(0.16f, 1.9f, 0.16f), steel).localRotation = Quaternion.Euler(beam * 60, 0, 45);
                }
            }
            for (int i = 0; i < 13; i++)
                Shape("Defense Marker", PrimitiveType.Cube, root, new Vector3(i - 6, 0.015f, 0), new Vector3(0.55f, 0.025f, 0.12f), marking);
            for (int i = 0; i < 24; i++)
            {
                float x = Mathf.Sin(i * 71.3f) * 5.8f;
                float z = 2 + (i * 7.7f) % 26;
                float size = 0.35f + (i % 4) * 0.18f;
                Shape("Shell Scarring", PrimitiveType.Sphere, root, new Vector3(x, -0.02f, z), new Vector3(size, 0.045f, size * 1.2f), dark);
            }
            for (int i = -6; i <= 6; i++)
                Shape("Rear Sandbag", PrimitiveType.Sphere, root, new Vector3(i, 0.2f, -4.2f), new Vector3(0.95f, 0.4f, 0.55f), sand);

            var soldier = game.gun.transform.Find("Shooter Visuals");
            var friendly = Surface(source, "Friendly / Slate Uniform", new Color(0.24f, 0.32f, 0.34f));
            var skin = Surface(source, "Friendly / Skin", new Color(0.71f, 0.54f, 0.4f));
            foreach (var renderer in soldier.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = renderer.name == "Head" ? skin : renderer.name == "Machine Gun" ? steel : friendly;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            Shape("Helmet", PrimitiveType.Sphere, soldier, new Vector3(0, 0.79f, 0), new Vector3(0.49f, 0.27f, 0.49f), friendly);
            Shape("Body Armor", PrimitiveType.Cube, soldier, new Vector3(0, 0.08f, 0.25f), new Vector3(0.48f, 0.5f, 0.12f), steel);
            Shape("Pack", PrimitiveType.Cube, soldier, new Vector3(0, 0.05f, -0.28f), new Vector3(0.46f, 0.5f, 0.23f), friendly);
            FitSoldier(soldier, friendly, steel, false);

            sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.65f;
                sun.shadowBias = 0.04f;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.45f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 38;
            RenderSettings.fogEndDistance = 80;
            SetStage(1);
        }

        public void SetStage(int stage)
        {
            int theme = (Mathf.Max(1, stage) - 1) % 6;
            Color earth = StagePalette.ForStage(stage).Earth;
            if (soil != null) soil.color = earth;
            Color haze = Color.Lerp(earth, new Color(0.25f, 0.29f, 0.3f), 0.55f);
            RenderSettings.fogColor = haze;
            if (view != null) view.backgroundColor = Color.Lerp(haze, Color.black, 0.55f);
            if (sun != null)
            {
                sun.color = theme == 2 ? new Color(1, 0.89f, 0.7f) : new Color(0.92f, 0.95f, 1);
                sun.transform.rotation = Quaternion.Euler(theme == 3 ? 38 : 52, -30 + theme * 18, 0);
            }
        }

        private void MakeTerrain(Transform parent)
        {
            const int columns = 40, rows = 55;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int z = 0; z <= rows; z++) for (int x = 0; x <= columns; x++)
            {
                int index = z * (columns + 1) + x;
                float px = -22 + x * 1.1f, pz = -10 + z;
                float edge = Mathf.Clamp01((Mathf.Abs(px) - 6.5f) / 4);
                vertices[index] = new Vector3(px, -0.06f + (Mathf.PerlinNoise(px * 0.13f + 4, pz * 0.12f) - 0.5f) * edge * 1.8f, pz);
                uv[index] = new Vector2(x / (float)columns, z / (float)rows);
            }
            int t = 0;
            for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
            {
                int a = z * (columns + 1) + x, b = a + columns + 1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
            terrainMesh = new Mesh { name = "Procedural Field", vertices = vertices, uv = uv, triangles = triangles };
            terrainMesh.RecalculateNormals();
            terrainMesh.RecalculateBounds();
            var ground = new GameObject("Textured Terrain", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(parent, false);
            ground.GetComponent<MeshFilter>().sharedMesh = terrainMesh;
            ground.GetComponent<MeshRenderer>().sharedMaterial = soil;
            soilTexture = new Texture2D(128, 128, TextureFormat.RGB24, false) { name = "Earth Grain", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float grain = ((x * 73 + y * 179 + x * y * 17) % 101) / 100f;
                float value = 0.55f + Mathf.PerlinNoise(x * 0.13f, y * 0.13f) * 0.3f + grain * 0.15f;
                pixels[y * 128 + x] = new Color(value, value, value);
            }
            soilTexture.SetPixels(pixels);
            soilTexture.Apply(false, true);
            soil.mainTexture = soilTexture;
            soil.mainTextureScale = new Vector2(16, 22);
        }

        private Material Surface(Material source, string name, Color color)
        {
            var material = new Material(source) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            materials.Add(material);
            return material;
        }

        // Both armies use the same shoulder, elbow and grip positions in model space (+Z forward).
        internal static void FitSoldier(Transform soldier, Material cloth, Material kit, bool infantry)
        {
            var head = soldier.Find("Head");
            head.localScale = new Vector3(0.32f, 0.41f, 0.35f);
            head.localPosition = new Vector3(0.035f, 0.6f, 0.04f);
            var helmet = soldier.Find("Helmet");
            helmet.localPosition = head.localPosition + new Vector3(0, 0.13f, -0.015f);
            helmet.localScale = new Vector3(0.405f, 0.235f, 0.43f);
            var gun = soldier.Find(infantry ? "Rifle" : "Machine Gun");
            gun.localPosition = new Vector3(0.15f, 0.22f, 0.4f);
            gun.localScale = new Vector3(0.11f, 0.14f, 0.78f);
            var barrel = infantry ? soldier.Find("Barrel") :
                Shape("Barrel", PrimitiveType.Cylinder, soldier, Vector3.zero, Vector3.one, kit);
            barrel.localPosition = new Vector3(0.15f, 0.245f, 0.89f);
            barrel.localScale = new Vector3(0.055f, 0.16f, 0.055f);
            barrel.localRotation = Quaternion.Euler(90, 0, 0);
            if (infantry)
                soldier.Find("Goggles").localPosition = head.localPosition + new Vector3(0, 0.045f, 0.16f);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = soldier.Find(side < 0 ? "Left Arm" : "Right Arm");
                var sleeve = infantry ? arm.Find("Mesh") : arm;
                sleeve.GetComponent<Renderer>().enabled = false;
                var shoulder = new Vector3(side * 0.28f, 0.34f, 0);
                var elbow = new Vector3(side * 0.39f, 0.015f, side < 0 ? 0.24f : 0.03f);
                var grip = new Vector3(side < 0 ? 0.12f : 0.16f, 0.19f, side < 0 ? 0.58f : 0.24f);
                Sleeve(soldier, shoulder, elbow, 0.205f, cloth);
                Sleeve(soldier, elbow, grip, 0.155f, cloth);
                Shape("Glove", PrimitiveType.Sphere, soldier, grip, new Vector3(0.135f, 0.12f, 0.17f), kit);
            }
        }

        private static void Sleeve(Transform parent, Vector3 start, Vector3 end, float width, Material cloth)
        {
            Vector3 span = end - start;
            var sleeve = Shape("Sleeve", PrimitiveType.Sphere, parent, (start + end) * 0.5f,
                new Vector3(width, span.magnitude + width * 0.45f, width), cloth);
            sleeve.localRotation = Quaternion.FromToRotation(Vector3.up, span);
        }

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
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return obj.transform;
        }

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
            if (soilTexture != null) Destroy(soilTexture);
            if (terrainMesh != null) Destroy(terrainMesh);
        }
    }
}

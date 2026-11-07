using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastLine.Editor
{
    public static class MinimalSceneBuilder
    {
        private const string Generated = "Assets/LastLine/Generated";

        [MenuItem("Last Line/Create Minimal Scene", false, 0)]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder("Assets/LastLine", "Generated");
            string folder = AssetDatabase.GenerateUniqueAssetPath(Generated + "/Minimal");
            AssetDatabase.CreateFolder(Generated, Path.GetFileName(folder));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = Material(folder, "Ground", new Color(0.08f, 0.14f, 0.19f));
            var cyan = Material(folder, "Turret", new Color(0.08f, 0.75f, 0.85f));
            var enemyMaterial = Material(folder, "Enemy", new Color(0.94f, 0.3f, 0.18f));
            var bulletMaterial = Material(folder, "Bullet", new Color(1f, 0.9f, 0.35f));
            var lineMaterial = Material(folder, "DefenseLine", new Color(0.22f, 0.95f, 0.55f));

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(PortraitCamera));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 26, -15);
            camera.transform.LookAt(new Vector3(0, 0, 12));
            camera.orthographic = true;
            camera.orthographicSize = 17;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.075f);
            camera.allowHDR = false;

            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50, -25, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            var field = new GameObject("Battlefield").transform;
            Shape("Ground", PrimitiveType.Cube, field, new Vector3(0, -0.3f, 12), new Vector3(13, 0.5f, 34), ground);
            Shape("Defense Line", PrimitiveType.Cube, field, new Vector3(0, 0.02f, 0), new Vector3(13, 0.08f, 0.18f), lineMaterial);
            for (int i = -1; i <= 1; i += 2)
                Shape("Boundary", PrimitiveType.Cube, field, new Vector3(i * 6.5f, 0, 12), new Vector3(0.08f, 0.12f, 34), cyan);

            var enemyObject = Shape("Enemy", PrimitiveType.Capsule, null, Vector3.zero, Vector3.one, enemyMaterial);
            var enemy = enemyObject.AddComponent<EnemyAgent>();
            enemyObject.SetActive(false);
            var enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyObject, folder + "/Enemy.prefab").GetComponent<EnemyAgent>();
            Object.DestroyImmediate(enemyObject);
            var bulletObject = Shape("Bullet", PrimitiveType.Cube, null, Vector3.zero, new Vector3(0.14f, 0.14f, 0.55f), bulletMaterial);
            bulletObject.AddComponent<Bullet>();
            bulletObject.SetActive(false);
            var bulletPrefab = PrefabUtility.SaveAsPrefabAsset(bulletObject, folder + "/Bullet.prefab").GetComponent<Bullet>();
            Object.DestroyImmediate(bulletObject);

            var root = new GameObject("GameRoot");
            var controller = root.AddComponent<GameController>();
            var spawner = new GameObject("Enemy Pool").AddComponent<EnemySpawner>();
            spawner.transform.SetParent(root.transform, false);
            spawner.prefab = enemyPrefab;
            var turret = new GameObject("Turret").AddComponent<MachineGun>();
            turret.transform.SetParent(root.transform, false);
            turret.transform.position = new Vector3(0, 1, -2);
            Shape("Base", PrimitiveType.Cylinder, turret.transform, new Vector3(0, -0.5f, 0), new Vector3(1.8f, 0.4f, 1.8f), cyan);
            Shape("Body", PrimitiveType.Cube, turret.transform, Vector3.zero, new Vector3(0.85f, 0.65f, 1), cyan);
            Shape("Barrel", PrimitiveType.Cube, turret.transform, new Vector3(0, 0, 0.8f), new Vector3(0.25f, 0.25f, 1), bulletMaterial);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turret.transform, false);
            muzzle.localPosition = new Vector3(0, 0, 1.4f);
            turret.muzzle = muzzle;
            turret.aimCamera = camera;
            turret.bulletPrefab = bulletPrefab;
            var hud = root.AddComponent<GameHud>();
            hud.gameCamera = camera;
            controller.enemies = spawner;
            controller.gun = turret;
            controller.hud = hud;

            EditorSceneManager.SaveScene(scene, folder + "/LastLine.unity");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Last Line scene created: " + scene.path + ". Set Game view to 9:16, then press Play.");
        }

        private static UnityEngine.Material Material(string folder, string name, Color color)
        {
            string shaderName = GraphicsSettings.currentRenderPipeline == null ? "Standard" : "Universal Render Pipeline/Lit";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Use a Built-in or URP 3D project. Missing shader: " + shaderName);
            var material = new UnityEngine.Material(shader) { name = name, color = color, enableInstancing = true };
            AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
            return material;
        }

        private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, UnityEngine.Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); // Collision uses horizontal swept circles.
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj;
        }

        [MenuItem("Last Line/Build Windows (Current Scene)", false, 30)]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (Object.FindFirstObjectByType<GameController>() == null || string.IsNullOrEmpty(scene.path))
                throw new System.InvalidOperationException("Open the generated LastLine scene first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string output = EditorUtility.SaveFolderPanel("Windows build output", "", "LastLineBuild");
            if (string.IsNullOrEmpty(output)) return;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene.path }, target = BuildTarget.StandaloneWindows64,
                locationPathName = Path.Combine(output, "LastLine.exe"), options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Build failed. See Console: " + report.summary.result);
            Debug.Log("Last Line Windows build: " + output);
        }
    }
}

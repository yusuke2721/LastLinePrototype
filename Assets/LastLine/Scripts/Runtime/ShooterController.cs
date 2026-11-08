using UnityEngine;
using UnityEngine.Rendering;

namespace LastLine
{
    // Moves the person across the defense line; the weapon always faces world +Z.
    [DefaultExecutionOrder(-10)]
    public sealed class ShooterController : MonoBehaviour
    {
        [Min(0)] public float moveHalfWidth = 5.5f;
        [Min(0)] public float moveSpeed = 16;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        private GameController game;
        private MachineGun weapon;
        private Vector3 startPosition;
        private float targetX;
        private float stride;
        private float stepAmount;

        public void Initialize(GameController controller, MachineGun gun)
        {
            game = controller;
            weapon = gun;
            startPosition = transform.position;
            if (visualRoot == null)
            {
                // Adapt existing saved turret scenes without replacing their tuning or references.
                var oldBody = transform.Find("Body");
                var uniform = oldBody != null ? oldBody.GetComponent<Renderer>().sharedMaterial
                    : gun.bulletPrefab.GetComponent<Renderer>().sharedMaterial;
                foreach (string name in new[] { "Base", "Body", "Barrel" })
                {
                    var oldPart = transform.Find(name);
                    if (oldPart != null) oldPart.gameObject.SetActive(false);
                }
                BuildVisuals(uniform, gun.bulletPrefab.GetComponent<Renderer>().sharedMaterial);
            }
            gameObject.name = "Shooter";
            ResetPosition();
        }

        private void Update()
        {
            if (game == null || !game.IsRunning) return;
            if (PointerAim.TryGetPosition(out Vector2 pointer) && weapon.aimCamera.pixelRect.Contains(pointer))
            {
                float normalized = Mathf.InverseLerp(weapon.aimCamera.pixelRect.xMin, weapon.aimCamera.pixelRect.xMax, pointer.x);
                targetX = CombatRules.HorizontalTarget(normalized, moveHalfWidth);
            }
            float previousX = transform.position.x;
            float x = CombatRules.StepHorizontal(previousX, targetX, moveSpeed, Time.deltaTime, moveHalfWidth);
            transform.SetPositionAndRotation(new Vector3(x, startPosition.y, startPosition.z), Quaternion.identity);
            float distance = x - previousX;
            stride += Mathf.Abs(distance) * 5f;
            stepAmount = Mathf.MoveTowards(stepAmount, Mathf.Abs(distance) > 0.0001f ? 1 : 0, Time.deltaTime * 12);
            float swing = Mathf.Sin(stride) * 28 * stepAmount;
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            visualRoot.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(stride)) * 0.04f * stepAmount, 0);
            // Lean is visual only. Bullet direction and spawn height remain stable.
            visualRoot.localRotation = Quaternion.Euler(0, 0, -Mathf.Sign(distance) * 4 * stepAmount);
        }

        public void ResetPosition()
        {
            targetX = Mathf.Clamp(startPosition.x, -moveHalfWidth, moveHalfWidth);
            transform.SetPositionAndRotation(new Vector3(targetX, startPosition.y, startPosition.z), Quaternion.identity);
            stride = 0;
            stepAmount = 0;
            if (visualRoot != null) { visualRoot.localPosition = Vector3.zero; visualRoot.localRotation = Quaternion.identity; }
            if (leftLeg != null) leftLeg.localRotation = Quaternion.identity;
            if (rightLeg != null) rightLeg.localRotation = Quaternion.identity;
        }

        // Used both by the scene generator and by the runtime adapter for older scenes.
        public void BuildVisuals(Material uniform, Material gunMaterial)
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("Shooter Visuals").transform;
            visualRoot.SetParent(transform, false);
            Part("Torso", PrimitiveType.Capsule, new Vector3(0, 0.02f, 0), new Vector3(0.65f, 0.48f, 0.5f), uniform);
            Part("Head", PrimitiveType.Sphere, new Vector3(0, 0.68f, 0), Vector3.one * 0.42f, uniform);
            leftLeg = Part("Left Leg", PrimitiveType.Cube, new Vector3(-0.2f, -0.65f, 0), new Vector3(0.23f, 0.6f, 0.28f), uniform);
            rightLeg = Part("Right Leg", PrimitiveType.Cube, new Vector3(0.2f, -0.65f, 0), new Vector3(0.23f, 0.6f, 0.28f), uniform);
            Part("Left Arm", PrimitiveType.Cube, new Vector3(-0.33f, 0.02f, 0.28f), new Vector3(0.18f, 0.22f, 0.65f), uniform);
            Part("Right Arm", PrimitiveType.Cube, new Vector3(0.33f, 0.02f, 0.28f), new Vector3(0.18f, 0.22f, 0.65f), uniform);
            Part("Machine Gun", PrimitiveType.Cube, new Vector3(0, 0.04f, 0.65f), new Vector3(0.3f, 0.25f, 1.1f), gunMaterial);
        }

        private Transform Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(visualRoot, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }
    }
}

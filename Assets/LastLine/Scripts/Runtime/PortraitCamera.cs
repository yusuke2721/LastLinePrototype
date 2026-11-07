using UnityEngine;

namespace LastLine
{
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-50)]
    public sealed class PortraitCamera : MonoBehaviour
    {
        private Camera view;
        private void Awake() { view = GetComponent<Camera>(); }
        private void Update()
        {
            float target = 9f / 16f;
            float actual = Screen.width / (float)Mathf.Max(1, Screen.height);
            view.rect = actual > target
                ? new Rect((1 - target / actual) * 0.5f, 0, target / actual, 1)
                : new Rect(0, (1 - actual / target) * 0.5f, 1, actual / target);
        }
    }
}

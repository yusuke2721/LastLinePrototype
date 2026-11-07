using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LastLine
{
    public static class PointerAim
    {
        public static bool TryGetPosition(out Vector2 position)
        {
            position = default;
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
            if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0) { position = Input.GetTouch(0).position; return true; }
            if (Input.mousePresent) { position = Input.mousePosition; return true; }
#endif
            return false;
        }
    }
}

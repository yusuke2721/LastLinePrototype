using UnityEngine;

namespace LastLine
{
    // IMGUI intentionally keeps the first prototype independent of UI packages/EventSystem.
    // Replace with Canvas/UI Toolkit after the core loop is confirmed.
    public sealed class GameHud : MonoBehaviour
    {
        public Camera gameCamera;
        private GameController game;
        private GUIStyle heading, body, button;
        public void Initialize(GameController controller) { game = controller; }

        private void OnGUI()
        {
            if (game == null || game.State == null || gameCamera == null) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 44, alignment = TextAnchor.MiddleCenter };
                body = new GUIStyle(GUI.skin.label) { fontSize = 27, alignment = TextAnchor.MiddleCenter };
                button = new GUIStyle(GUI.skin.button) { fontSize = 30 };
            }
            Rect viewport = gameCamera.pixelRect;
            float scale = viewport.width / 540f;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, Screen.height - viewport.yMax, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            var state = game.State;
            GUI.Box(new Rect(14, 14, 512, 130), GUIContent.none);
            GUI.Label(new Rect(20, 18, 500, 56), "LAST LINE", heading);
            GUI.Label(new Rect(20, 78, 500, 44),
                $"SCORE {state.Score}    LIFE {state.Lives}    {state.Elapsed:00.0}s", body);
            GUI.Label(new Rect(20, 868, 500, 48), "MOVE MOUSE / DRAG TO STEP", body);
            if (!state.IsRunning)
            {
                GUI.color = new Color(1, 1, 1, 0.97f);
                GUI.Box(new Rect(35, 320, 470, 290), GUIContent.none);
                GUI.Label(new Rect(40, 345, 460, 70), state.IsVictory ? "SURVIVED!" : "GAME OVER", heading);
                GUI.Label(new Rect(40, 418, 460, 55), $"SCORE {state.Score} / {state.Elapsed:0.0}s", body);
                if (GUI.Button(new Rect(120, 500, 300, 76), "RETRY", button)) game.RestartRound();
            }
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }
    }
}

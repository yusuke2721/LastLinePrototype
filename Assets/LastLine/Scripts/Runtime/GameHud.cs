using UnityEngine;

namespace LastLine
{
    // Package-free tactical HUD; existing scenes require no regeneration.
    public sealed class GameHud : MonoBehaviour
    {
        public Camera gameCamera;
        private GameController game;
        private static readonly Color Ink = new Color(0.045f, 0.055f, 0.045f, 0.96f);
        private static readonly Color Border = new Color(0.24f, 0.28f, 0.22f);
        private static readonly Color White = new Color(0.9f, 0.9f, 0.83f);
        private static readonly Color Muted = new Color(0.59f, 0.63f, 0.55f);
        private static readonly Color Cyan = new Color(0.64f, 0.76f, 0.51f);
        private static readonly Color Amber = new Color(0.83f, 0.69f, 0.39f);
        private static readonly Color Red = new Color(0.89f, 0.35f, 0.27f);
        private GUIStyle title, small, eyebrow, number, centered, resultTitle, resultNumber, button, secondary, hero;
        private Color StageAccent => StagePalette.ForStage(game.CurrentStage).Accent;
        public void Initialize(GameController controller) { game = controller; }

        private void OnGUI()
        {
            if (game == null) return;
            if (!string.IsNullOrEmpty(game.StartupError))
            {
                GUI.Box(new Rect(12, 12, Screen.width - 24, Screen.height - 24), "LAST LINE / STARTUP ERROR");
                GUI.Label(new Rect(24, 60, Screen.width - 48, Screen.height - 90),
                    "Version " + Application.version + "\nPlease share this screen.\n\n" + game.StartupError);
                return;
            }
            if (game.State == null || gameCamera == null) return;
            EnsureStyles();
            // Camera/safeArea use bottom-left coordinates; IMGUI uses top-left.
            Rect viewport = gameCamera.pixelRect;
            Rect safe = Screen.safeArea;
            float left = Mathf.Max(viewport.xMin, safe.xMin);
            float right = Mathf.Min(viewport.xMax, safe.xMax);
            float bottom = Mathf.Max(viewport.yMin, safe.yMin);
            float top = Mathf.Min(viewport.yMax, safe.yMax);
            float scale = Mathf.Min((right - left) / 540f, (top - bottom) / 960f);
            if (scale <= 0) return;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            Color oldContent = GUI.contentColor;
            Color oldBackground = GUI.backgroundColor;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
                // A viewport camera does not clear its letterbox region between frames.
                Fill(new Rect(0, 0, viewport.xMin, Screen.height), Color.black);
                Fill(new Rect(viewport.xMax, 0, Screen.width - viewport.xMax, Screen.height), Color.black);
                Fill(new Rect(viewport.xMin, 0, viewport.width, Screen.height - viewport.yMax), Color.black);
                Fill(new Rect(viewport.xMin, Screen.height - viewport.yMin, viewport.width, viewport.yMin), Color.black);
                var state = game.State;
                if (game.IsTitle || !state.IsRunning)
                    Fill(new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height),
                        new Color(0.01f, 0.025f, 0.045f, 0.8f));
                GUI.matrix = Matrix4x4.TRS(new Vector3(left + (right - left - 540 * scale) / 2,
                    Screen.height - top + (top - bottom - 960 * scale) / 2, 0),
                    Quaternion.identity, new Vector3(scale, scale, 1));
                if (game.IsTitle) DrawTitle();
                else
                {
                    if (game.IsRunning) DrawTankHealth();
                    if (game.IsRunning) DrawSupport();
                    DrawHeader(state);
                    DrawDefense(state);
                    if (!state.IsRunning) DrawResult(state);
                }
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.contentColor = oldContent;
                GUI.backgroundColor = oldBackground;
            }
        }

        private void DrawTankHealth()
        {
            var tanks = game.enemies.Tanks;
            if (tanks == null) return;
            Matrix4x4 inverse = GUI.matrix.inverse;
            foreach (var tank in tanks)
            {
                if (!tank.IsAlive) continue;
                Vector3 screen = gameCamera.WorldToScreenPoint(tank.transform.position + Vector3.up * 0.95f);
                if (screen.z <= 0 || !gameCamera.pixelRect.Contains(new Vector2(screen.x, screen.y))) continue;
                Vector3 point = inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0));
                if (point.y < 215 || point.y > 795) continue;
                float x = Mathf.Clamp(point.x - 50, 20, 420);
                int seconds = Mathf.CeilToInt(tank.SecondsToBreach);
                Color armorColor = tank.ArmorHitVisible ? Color.white : Amber;
                Panel(new Rect(x, point.y - 44, 100, 49));
                Label(new Rect(x, point.y - 44, 100, 21), $"LINE {seconds}s", centered, seconds <= 5 ? Red : Amber);
                Label(new Rect(x, point.y - 25, 100, 21), $"ARMOR {tank.Health}/{tank.MaxHealth}", centered, armorColor);
                Fill(new Rect(x + 6, point.y - 2, 88, 3), Border);
                Fill(new Rect(x + 6, point.y - 2, 88 * Mathf.Clamp01(tank.Health / (float)tank.MaxHealth), 3), armorColor);
            }
        }

        private void DrawSupport()
        {
            var support = game.support;
            if (support == null) return;
            Panel(new Rect(20, 780, 500, 32));
            Label(new Rect(26, 781, 488, 30), $"SQUAD {support.AllyCount + 1}/5     WEAPON LV.{support.WeaponLevel}     DRONES {support.QueuedDrones}", centered, Cyan);
            bool breached = Time.unscaledTime < support.BreachWarningUntil;
            if (breached || Time.unscaledTime < support.NoticeUntil)
            {
                Panel(new Rect(55, 199, 430, 31));
                Label(new Rect(55, 199, 430, 31), breached ? support.BreachNotice : support.Notice, centered, breached ? Red : Amber);
            }
            if (!support.TargetActive) return;
            Vector3 screen = gameCamera.WorldToScreenPoint(support.ActiveTarget.position + Vector3.up * 0.7f);
            if (screen.z <= 0 || !gameCamera.pixelRect.Contains(new Vector2(screen.x, screen.y))) return;
            Vector3 point = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0));
            if (point.y < 240 || point.y > 750) return;
            float x = Mathf.Clamp(point.x - 80, 20, 360);
            string reward = support.TargetKind == SupportKind.Reinforcements ? "SQUAD +1"
                : support.TargetKind == SupportKind.Weapon ? "WEAPON UP" : "DRONE STRIKE";
            Color accent = support.TargetKind == SupportKind.Reinforcements ? Cyan
                : support.TargetKind == SupportKind.Weapon ? Amber : Red;
            Panel(new Rect(x, point.y - 25, 160, 43));
            Label(new Rect(x, point.y - 25, 160, 23), reward, centered, accent);
            Label(new Rect(x, point.y - 3, 160, 19), $"SHOOT / {support.TargetHealth} HP", centered, White);
        }

        private void DrawHeader(RoundState state)
        {
            Panel(new Rect(20, 20, 500, 166));
            Fill(new Rect(20, 20, 3, 58), StageAccent);
            Label(new Rect(38, 30, 306, 18), $"OP {game.CurrentStage:00} / {StagePalette.ForStage(game.CurrentStage).Name} / {BattlefieldPresentation.SectorName(game.CurrentStage)}", eyebrow, StageAccent);
            Label(new Rect(38, 51, 290, 32), "LAST LINE", title, White);
            Label(new Rect(354, 31, 146, 18), "STAGE SCORE", small, Muted);
            Label(new Rect(354, 49, 146, 38), state.Score.ToString("D5"), number, White);
            Fill(new Rect(38, 98, 464, 1), Border);
            float remaining = Mathf.Max(0, state.Duration - state.Elapsed);
            int seconds = Mathf.CeilToInt(remaining);
            Label(new Rect(38, 111, 156, 18), "TIME REMAINING", small, Muted);
            Label(new Rect(38, 132, 156, 36), $"{seconds / 60:00}:{seconds % 60:00}", number,
                remaining <= 10 ? Amber : White);
            Label(new Rect(218, 111, 282, 18), "SURVIVAL PROGRESS", small, Muted);
            Fill(new Rect(218, 143, 282, 5), Border);
            Fill(new Rect(218, 143, 282 * Mathf.Clamp01(state.Elapsed / state.Duration), 5), Cyan);
            for (int i = 1; i < 3; i++) Fill(new Rect(218 + 94 * i, 141, 2, 9), Ink);
            Label(new Rect(218, 156, 282, 20), "SURVIVE TO ZERO", small, Muted);
        }

        private void DrawDefense(RoundState state)
        {
            float integrity = Mathf.Clamp01(state.Lives / (float)Mathf.Max(1, game.startingLives));
            bool critical = integrity <= 0.3f;
            bool breached = game.support != null && Time.unscaledTime < game.support.BreachWarningUntil;
            Color accent = critical || breached ? Red : Cyan;
            Panel(new Rect(20, 824, 500, 112));
            Fill(new Rect(20, 824, 3, 112), accent);
            Label(new Rect(38, 837, 340, 20), breached ? "DEFENSE LINE / BREACHED"
                : critical ? "DEFENSE LINE / CRITICAL" : "DEFENSE LINE / INTEGRITY", eyebrow, accent);
            Label(new Rect(392, 833, 110, 28), $"{state.Lives:00} / {Mathf.Max(1, game.startingLives):00}", centered, White);
            Fill(new Rect(38, 870, 464, 8), Border);
            Fill(new Rect(38, 870, 464 * integrity, 8), accent);
            int segments = Mathf.Clamp(game.startingLives, 1, 20);
            for (int i = 1; i < segments; i++) Fill(new Rect(38 + 464f * i / segments, 870, 3, 8), Ink);
            Label(new Rect(38, 895, 464, 25), state.IsRunning ? ControlHint() : "ROUND COMPLETE", centered, Muted);
        }

        private string ControlHint()
        {
            var support = game.support;
            if (game.CurrentStage != 1 || support.ClaimedSupportThisRound)
                return "HOLD THE LINE UNTIL TIME RUNS OUT";
            if (!support.TargetActive)
                return "MOVE SIDEWAYS TO AIM / FIRE IS AUTOMATIC";
            float offset = support.ActiveTarget.position.x - game.gun.muzzle.position.x;
            if (Mathf.Abs(offset) <= 0.8f) return "HOLD THIS LANE TO CLAIM SUPPORT";
            return offset < 0 ? "<   MOVE LEFT TO SHOOT SUPPORT" : "MOVE RIGHT TO SHOOT SUPPORT   >";
        }

        private void DrawResult(RoundState state)
        {
            Color accent = state.IsVictory ? StageAccent : Red;
            Panel(new Rect(40, 272, 460, 452));
            Fill(new Rect(40, 272, 460, 3), accent);
            Label(new Rect(64, 298, 412, 22), $"OPERATION {game.CurrentStage:00} / AFTER ACTION REPORT", centered, accent);
            Label(new Rect(64, 334, 412, 54), state.IsVictory ? "SECTOR SECURED" : "DEFENSE LOST", resultTitle, White);
            Label(new Rect(64, 399, 412, 25), state.IsVictory
                ? $"NEXT / {StagePalette.ForStage(Mathf.Min(999, game.CurrentStage + 1)).Name} SECTOR"
                : "Regroup and retry the current sector.", centered, Muted);
            Fill(new Rect(72, 440, 396, 1), Border);
            Label(new Rect(72, 456, 180, 18), "STAGE SCORE", centered, Muted);
            Label(new Rect(288, 456, 180, 18), "OPERATION TOTAL", centered, Muted);
            Label(new Rect(72, 482, 180, 42), state.Score.ToString("D5"), resultNumber, White);
            Label(new Rect(288, 482, 180, 42), game.CampaignScore.ToString("D5"), resultNumber, White);
            Label(new Rect(64, 541, 412, 26), state.IsVictory ? $"NEXT SECTOR / SQUAD {game.NextAllies + 1} / WEAPON LV.{game.NextWeapon}"
                : $"TIME HELD / {state.Elapsed:0.0} SECONDS", centered, Muted);
            if (Action(new Rect(72, 589, 396, 64), state.IsVictory ? $"DEPLOY TO STAGE {game.CurrentStage + 1:00}   >" : "RETRY SECTOR   >", true))
            {
                if (state.IsVictory) game.NextStage(); else game.RestartRound();
            }
            if (Action(new Rect(72, 668, 396, 32), "RETURN TO COMMAND", false)) game.ReturnToTitle();
        }

        private void DrawTitle()
        {
            Fill(new Rect(0, 0, 540, 960), new Color(0.035f, 0.045f, 0.035f, 0.94f));
            Color grid = new Color(0.22f, 0.27f, 0.2f, 0.24f);
            for (int x = 20; x < 540; x += 40) Fill(new Rect(x, 20, 1, 900), grid);
            for (int y = 20; y < 940; y += 40) Fill(new Rect(20, y, 500, 1), grid);
            for (int ring = 0; ring < 5; ring++)
            {
                Vector2 previous = Vector2.zero;
                for (int step = 0; step <= 32; step++)
                {
                    float angle = step / 32f * Mathf.PI * 2;
                    float radius = 45 + ring * 30 + Mathf.Sin(angle * 3 + ring) * 9;
                    Vector2 point = new Vector2(388 + Mathf.Cos(angle) * radius, 292 + Mathf.Sin(angle) * radius * 1.25f);
                    point.x = Mathf.Clamp(point.x, 20, 520);
                    if (step > 0) Line(previous, point, grid);
                    previous = point;
                }
            }
            Label(new Rect(36, 36, 460, 23), "FIELD OPERATIONS / COMMAND LINK", eyebrow, Cyan);
            Fill(new Rect(36, 78, 468, 1), Border);
            Label(new Rect(36, 133, 460, 24), "SECTOR DEFENSE PROGRAM", small, Muted);
            Label(new Rect(30, 190, 474, 105), "LAST", hero, White);
            Label(new Rect(30, 282, 474, 105), "LINE", hero, White);
            Fill(new Rect(36, 410, 64, 3), Amber);
            Label(new Rect(36, 435, 468, 36), "HOLD YOUR GROUND.", title, White);
            Label(new Rect(36, 481, 468, 25), "Every sector cleared opens a harder front.", small, Muted);
            Panel(new Rect(34, 544, 472, 165));
            Label(new Rect(54, 560, 430, 25), "MISSION BRIEF / OUTER PERIMETER", eyebrow, Amber);
            Label(new Rect(54, 597, 430, 24), $"01 / HOLD FOR {game.roundSeconds:0} SECONDS", small, White);
            Label(new Rect(54, 631, 430, 24), "02 / CHOOSE SUPPORT OR STOP THE ADVANCE", small, White);
            Label(new Rect(54, 665, 430, 24), "03 / CLEAR THE SECTOR AND ADVANCE", small, White);
            if (game.SavedStage > 0)
            {
                if (Action(new Rect(34, 720, 472, 62), $"CONTINUE / STAGE {game.SavedStage:00}   >", true)) game.ResumeCampaign();
                if (Action(new Rect(34, 790, 472, 40), "NEW OPERATION", false)) game.StartCampaign();
            }
            else if (Action(new Rect(34, 752, 472, 68), "BEGIN OPERATION   >", true)) game.StartCampaign();
            Label(new Rect(34, 838, 472, 28), "MOVE MOUSE OR DRAG TO CHANGE LANES", centered, Muted);
            Fill(new Rect(36, 897, 468, 1), Border);
            string volume = game.effects.VolumeStep == 0 ? "OFF" : $"{game.effects.VolumeStep * 25}%";
            if (Action(new Rect(36, 903, 468, 46), $"SOUND / {volume}", false)) game.effects.CycleVolume();
        }

        private bool Action(Rect rect, string text, bool primary)
        {
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color accent = game.IsTitle ? Amber : StageAccent;
            Fill(rect, primary ? (hovered ? Color.Lerp(accent, White, 0.2f) : accent) : Ink);
            if (!primary) Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), hovered ? Amber : Border);
            return GUI.Button(rect, text, primary ? button : secondary);
        }

        private static void Line(Vector2 start, Vector2 end, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(new Vector3(start.x, start.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg), Vector3.one);
            Fill(new Rect(0, 0, Vector2.Distance(start, end), 1), color);
            GUI.matrix = previous;
        }

        private void EnsureStyles()
        {
            if (title != null) return;
            title = TextStyle(23, TextAnchor.MiddleLeft, true);
            eyebrow = TextStyle(13, TextAnchor.MiddleLeft, true);
            small = TextStyle(14, TextAnchor.MiddleLeft);
            number = TextStyle(32, TextAnchor.MiddleLeft, true);
            centered = TextStyle(14, TextAnchor.MiddleCenter);
            resultTitle = TextStyle(31, TextAnchor.MiddleCenter, true);
            resultNumber = TextStyle(32, TextAnchor.MiddleCenter, true);
            hero = TextStyle(100, TextAnchor.MiddleLeft, true);
            button = new GUIStyle()
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(), margin = new RectOffset(), padding = new RectOffset()
            };
            foreach (GUIStyleState style in new[] { button.normal, button.hover, button.active, button.focused,
                button.onNormal, button.onHover, button.onActive, button.onFocused })
            {
                style.background = null;
                style.textColor = Ink;
            }
            button.focused.textColor = Color.white;
            button.active.textColor = Color.white;
            secondary = new GUIStyle(button);
            secondary.normal.textColor = Muted;
            secondary.hover.textColor = White;
        }

        private static GUIStyle TextStyle(int size, TextAnchor alignment, bool bold = false)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size, alignment = alignment,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(), margin = new RectOffset(), wordWrap = false
            };
        }

        private static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static void Panel(Rect rect)
        {
            Fill(rect, Ink);
            Fill(new Rect(rect.x, rect.y, rect.width, 1), Border);
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), Border);
            Fill(new Rect(rect.x, rect.y, 1, rect.height), Border);
            Fill(new Rect(rect.xMax - 1, rect.y, 1, rect.height), Border);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

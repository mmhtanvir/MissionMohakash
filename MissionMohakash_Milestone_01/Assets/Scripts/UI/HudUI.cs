using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mohakash
{
    /// <summary>
    /// TEMPORARY IMGUI HUD (OnGUI). Chosen so Milestone 1 needs no Canvas/EventSystem/serialized UI assets.
    /// Display only; keyboard is read through InputReader. To be replaced by uGUI/UI Toolkit later.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        GUIStyle label, title, body;
        string report;

        static readonly Color Panel = new Color(0f, 0f, 0f, 0.58f);

        void Update()
        {
            var m = MissionState.Instance;
            if (m == null) return;
            if (m.Phase != MissionPhase.Playing && report == null) report = m.BuildReport();
            if (m.Phase != MissionPhase.Playing && InputReader.RestartPressed) Restart();
        }

        void Restart()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex < 0)
            {
                Debug.LogError("Scene is not in Build Settings. Use menu: Mission Mohakash > Create Milestone 1 Scene, or File > Build Profiles > Add Open Scenes.");
                return;
            }
            SceneManager.LoadScene(scene.buildIndex);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Text(Rect r, string t, GUIStyle st, Color col)
        {
            st.normal.textColor = col;
            GUI.Label(r, t, st);
        }

        void Bar(Rect r, float frac, Color c)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.15f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), c);
        }

        void OnGUI()
        {
            var m = MissionState.Instance;
            if (m == null) return;
            float s = Mathf.Max(0.6f, Screen.height / 900f);

            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label);
                title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { wordWrap = true };
            }
            label.fontSize = Mathf.RoundToInt(17 * s);
            title.fontSize = Mathf.RoundToInt(22 * s);
            body.fontSize = Mathf.RoundToInt(18 * s);

            DrawStatus(m, s);
            DrawPrompt(m, s);
            DrawControls(m, s);
            if (m.Phase != MissionPhase.Playing) DrawReport(s);
        }

        void DrawStatus(MissionState m, float s)
        {
            float x = 20 * s, y = 20 * s, w = 400 * s;
            float h = (190 + 26 * m.Objectives.Count) * s;
            Fill(new Rect(x - 8 * s, y - 8 * s, w + 16 * s, h), Panel);

            Text(new Rect(x, y, w, 28 * s), "MISSION MOHAKASH  |  PROTOTYPE (placeholders)", label, new Color(0.7f, 0.9f, 1f));
            y += 30 * s;

            Text(new Rect(x, y, w, 24 * s), "OXYGEN  " + m.oxygen.ToString("0") + " / 100 GAME UNITS", label, Color.white);
            y += 24 * s;
            Bar(new Rect(x, y, w, 12 * s), m.oxygen / MissionState.MaxOxygen, m.oxygen < 25f ? new Color(1f, 0.3f, 0.25f) : new Color(0.35f, 0.8f, 1f));
            y += 18 * s;

            Text(new Rect(x, y, w, 24 * s), "POWER  " + m.power.ToString("0") + " / 100 GAME UNITS", label, Color.white);
            y += 24 * s;
            Bar(new Rect(x, y, w, 12 * s), m.power / MissionState.MaxPower, new Color(1f, 0.82f, 0.25f));
            y += 20 * s;

            Text(new Rect(x, y, w, 24 * s), "SCIENCE DATA  " + m.science.ToString("0") + " GAME UNITS", label, new Color(0.6f, 1f, 0.6f));
            y += 28 * s;

            foreach (var o in m.Objectives)
            {
                Text(new Rect(x, y, w, 24 * s), (o.done ? "[x] " : "[  ] ") + o.label, label,
                     o.done ? new Color(0.5f, 1f, 0.55f) : (o.optional ? new Color(0.8f, 0.8f, 0.8f) : Color.white));
                y += 24 * s;
            }

            var next = NextTarget(m);
            if (next != null && PlayerController.Instance != null && PlayerController.Instance.cam != null)
            {
                var cam = PlayerController.Instance.cam.transform;
                Vector3 to = next.transform.position - PlayerController.Instance.transform.position;
                float dist = to.magnitude;
                Vector3 flatTo = Vector3.ProjectOnPlane(to, Vector3.up);
                Vector3 flatFwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                float a = Vector3.SignedAngle(flatFwd, flatTo, Vector3.up);
                string dirWord = Mathf.Abs(a) < 45f ? "ahead" : (Mathf.Abs(a) > 135f ? "behind you" : (a > 0f ? "to your right" : "to your left"));
                Text(new Rect(x, y + 4 * s, w, 24 * s), "NEXT: " + next.displayName + " - " + dist.ToString("0") + " m " + dirWord, label, new Color(0.5f, 0.95f, 1f));
            }
            y += 30 * s;
            int sec = Mathf.FloorToInt(m.elapsed);
            Text(new Rect(x, y, w, 24 * s), "EVA TIME  " + (sec / 60) + ":" + (sec % 60).ToString("00"), label, new Color(0.85f, 0.85f, 0.85f));
        }

        Interactable NextTarget(MissionState m)
        {
            if (PlayerController.Instance == null) return null;
            Vector3 p = PlayerController.Instance.transform.position;
            bool ready = m.ReadyToReturn;
            Interactable best = null;
            float bd = float.MaxValue;
            foreach (var it in Interactable.All)
            {
                if (it.Used) continue;
                bool want = ready ? it.isFinish : (!it.isOptional && !it.isFinish);
                if (!want) continue;
                float d = Vector3.Distance(p, it.transform.position);
                if (d < bd) { bd = d; best = it; }
            }
            return best;
        }

        void DrawPrompt(MissionState m, float s)
        {
            float w = 760 * s;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 150 * s;

            string toast = m.Toast;
            if (!string.IsNullOrEmpty(toast))
            {
                Fill(new Rect(x, y - 44 * s, w, 36 * s), Panel);
                label.alignment = TextAnchor.MiddleCenter;
                Text(new Rect(x, y - 44 * s, w, 36 * s), toast, label, new Color(1f, 0.92f, 0.5f));
                label.alignment = TextAnchor.UpperLeft;
            }

            var p = PlayerController.Instance;
            if (m.Phase == MissionPhase.Playing && p != null && p.Nearest != null)
            {
                Fill(new Rect(x, y, w, 40 * s), Panel);
                label.alignment = TextAnchor.MiddleCenter;
                Text(new Rect(x, y, w, 40 * s), "[E] " + p.Nearest.prompt + p.Nearest.CostText(), label, Color.white);
                label.alignment = TextAnchor.UpperLeft;
            }
        }

        void DrawControls(MissionState m, float s)
        {
            string t = "WASD move   Shift sprint   Space jump   E interact   Mouse look   Esc release cursor / click to re-lock";
            var cam = PlayerController.Instance != null ? PlayerController.Instance.cam : null;
            if (cam != null && !cam.CursorLocked && m.Phase == MissionPhase.Playing) t = "CURSOR RELEASED - click in the Game view to resume mouse look.   " + t;
            Rect r = new Rect(20 * s, Screen.height - 36 * s, Screen.width - 40 * s, 28 * s);
            Text(r, t, label, new Color(1f, 1f, 1f, 0.75f));
        }

        void DrawReport(float s)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f));
            float w = Screen.width * 0.62f, h = Screen.height * 0.82f;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(r, new Color(0.05f, 0.07f, 0.12f, 0.95f));
            Text(new Rect(r.x + 24 * s, r.y + 16 * s, r.width - 48 * s, 36 * s), "MISSION REPORT", title, new Color(0.7f, 0.9f, 1f));
            Text(new Rect(r.x + 24 * s, r.y + 60 * s, r.width - 48 * s, r.height - 120 * s), report ?? "", body, Color.white);
            Text(new Rect(r.x + 24 * s, r.yMax - 44 * s, r.width - 48 * s, 32 * s), "Press R to restart", title, new Color(1f, 0.92f, 0.5f));
        }
    }
}

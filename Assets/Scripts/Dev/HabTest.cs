using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mohakash
{
    /// <summary>
    /// Habitat check without a game window: "-batchmode -habtest &lt;dir&gt;" skips the menus, then records the airlock
    /// entry (hatch, ladder, chamber cycle, suit off), a view of every room, suiting up and going back out. Frames are
    /// rendered from the game camera into PNGs every 0.5 s.
    /// </summary>
    public class HabTest : MonoBehaviour
    {
        string dir; int frame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-habtest");
            if (i < 0) return;
            var go = new GameObject("HabTest"); DontDestroyOnLoad(go);
            var t = go.AddComponent<HabTest>();
            t.dir = i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "habtest");
            Directory.CreateDirectory(t.dir);
            PlayerPrefs.DeleteAll();
        }

        void Shot(string name)
        {
            var cam = Camera.main; if (cam == null) return;
            var rt = RenderTexture.GetTemporary(1280, 800, 24);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(dir, $"{frame++:000}_{name}.png"), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[HABTEST] shot " + name);
        }

        IEnumerator Record(Mission1 m, string name, float max = 90f)
        {
            yield return null; yield return null;
            float next = 0f;
            for (float t = 0f; m.InCinematic && t < max; t += Time.unscaledDeltaTime)
            {
                if (t >= next) { Shot($"{name}_{t:00.0}s"); next += 0.5f; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Shot(name + "_end");
        }

        IEnumerator Start()
        {
            while (SceneManager.GetActiveScene().name != Game.MenuScene)
            {
                if (SceneManager.GetActiveScene().name == Game.IntroScene) Game.Load(Game.MenuScene);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(1f);
            FindAnyObjectByType<MainMenu>().StartMission();
            Mission1 m = null;
            while ((m = FindAnyObjectByType<Mission1>()) == null) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            for (float t0 = Time.realtimeSinceStartup; m.Current != Mission1.Phase.Operations && Time.realtimeSinceStartup - t0 < 60f; )
            {
                var advance = m.Ui.TestAdvance; m.Ui.TestAdvance = null;
                if (advance != null) { advance(null); yield return new WaitForSecondsRealtime(1.5f); } else yield return null;
            }
            m.Model.Tasks.AutoPass = true;
            yield return new WaitForSecondsRealtime(2f);

            m.TestTeleport(m.World.AirlockPoint + new Vector3(1.5f, 0f, 0f));
            m.Cam.Snap(70f, 8f); yield return new WaitForSecondsRealtime(1f);
            Shot("outside_airlock");
            m.TestEnterCinematic();
            yield return Record(m, "enter");

            // every room, from the hub doorways (camera behind the astronaut looking along the arm)
            foreach (var (name, p, yaw) in new[] { ("hub", new Vector3(3.2f, 0f, 1.2f), -135f), ("airlock_room", new Vector3(5f, 0f, 0f), 90f),
                                                   ("crew_quarters", new Vector3(-5f, 0f, 0f), -90f), ("galley", new Vector3(0f, 0f, 5f), 0f),
                                                   ("lab", new Vector3(0f, 0f, -5f), 180f) })
            {
                m.Player.Teleport(HabitatInterior.Origin + p + Vector3.up * 0.15f);
                m.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f); m.Cam.Snap(yaw, 10f);
                yield return new WaitForSecondsRealtime(1.2f);
                Shot("room_" + name);
            }
            m.Player.Teleport(HabitatInterior.DockStand + new Vector3(0f, 0.15f, 0.8f)); yield return new WaitForSecondsRealtime(0.8f);
            m.SuitUp();
            yield return Record(m, "suit_on");
            m.ExitHabitat();
            yield return Record(m, "exit");
            Debug.Log("[HABTEST] DONE");
            Application.Quit();
        }
    }
}

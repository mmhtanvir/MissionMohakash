using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Mohakash
{
    /// <summary>
    /// Scripted end-to-end playthrough for verification. Only runs when the player is launched with
    /// "-autotest &lt;screenshotDir&gt;". Drives the real game code (input injection + mission APIs) and saves screenshots.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-autotest");
            if (i < 0) return;
            var go = new GameObject("AutoTest");
            DontDestroyOnLoad(go);
            var t = go.AddComponent<AutoTest>();
            t.dir = i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(t.dir);
            PlayerPrefs.DeleteAll();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            Debug.Log("[AUTOTEST] shot " + name);
            yield return null;
        }

        IEnumerator Tap(Key k) { GameInput.SimTap(k); yield return null; yield return null; }
        IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

        IEnumerator Start()
        {
            yield return Wait(3f);
            if (SceneManager.GetActiveScene().name == Game.IntroScene) yield return Shot("00_intro_video");
            while (SceneManager.GetActiveScene().name != Game.MenuScene) yield return null;
            yield return Wait(2.5f);
            yield return Shot("01_menu");
            var menu = FindAnyObjectByType<MainMenu>();
            menu.ShowMissionSelect(); yield return Wait(0.8f);
            yield return Shot("01b_mission_select");
            menu.StartMission();

            yield return RunMission(null, true);   // null = the simple loadout's default choice

            Debug.Log("[AUTOTEST] --- failure scenario: no OGS, no scrubber, no battery ---");
            var bad = Catalog.RequiredOnly(); bad.Set("SEIS", 1); bad.Set("SOL-S", 1);
            var old = FindAnyObjectByType<Mission1>();
            Game.Load(Game.Mission1Scene);
            while (old != null) yield return null;   // destroyed when the new scene loads
            yield return RunMission(bad, false);

            Debug.Log("[AUTOTEST] DONE");
            Application.Quit();
        }

        IEnumerator RunMission(Loadout kit, bool full)
        {
            Mission1 m = null;
            while ((m = FindAnyObjectByType<Mission1>()) == null) yield return null;
            yield return Wait(2f);
            // briefing -> loadout -> 72 h planner -> launch. Each screen's hook fires once (a stale hook would launch twice).
            int screen = 0;
            for (float t0 = Time.realtimeSinceStartup; m.Current != Mission1.Phase.Operations && Time.realtimeSinceStartup - t0 < 60f; )
            {
                var advance = m.Ui.TestAdvance; m.Ui.TestAdvance = null;
                if (advance == null) { yield return null; continue; }   // fading between screens
                if (full) yield return Shot($"0{2 + screen++}_prelaunch");
                advance(kit);   // only the loadout uses the kit; briefing and planner ignore it
                yield return Wait(1.5f);
            }
            m.Model.Tasks.AutoPass = true;   // scripted runs skip the guided procedures (Tasks A-G) so the objectives can be reached by teleport
            yield return Wait(2.5f);

            if (full)
            {
                yield return Shot("04_landed");
                yield return Tap(Key.H); yield return Wait(0.5f);
                yield return Shot("04b_controls");
                yield return Tap(Key.H); yield return Wait(0.3f);
                GameInput.SimMove = new Vector2(0.2f, 1f); yield return Wait(2.2f);
                yield return Shot("05_walking");
                GameInput.SimSprint = true; yield return Wait(1.2f);
                yield return Tap(Key.Space); yield return Wait(0.5f);
                yield return Shot("06_sprint_jump");
                GameInput.SimSprint = false; GameInput.SimMove = Vector2.zero;

                m.TestTeleport(m.World.SeisCratePoint + new Vector3(-1.5f, 0, 0));
                yield return Wait(0.5f); yield return Tap(Key.E); yield return Wait(1.5f);
                yield return Shot("07a_crouching_for_seis");
                yield return Wait(3.2f);                      // walk-up + crouch + lift animation
                yield return Shot("07_carrying_seis");

                if (m.Rover != null)
                {
                    m.TestTeleport(m.Rover.transform.position + new Vector3(-3f, 0, 0));
                    yield return Wait(0.4f); yield return Tap(Key.R);
                    GameInput.SimMove = new Vector2(0f, 1f); GameInput.SimSprint = true;
                    yield return Wait(4f);
                    yield return Shot("08_rover_driving");
                    GameInput.SimMove = Vector2.zero; GameInput.SimSprint = false;
                    yield return Wait(1.5f); yield return Tap(Key.R); yield return Wait(0.5f);
                }

                m.TestTeleport(new Vector3(70f, 0, -70f));
                yield return Wait(0.8f);
                yield return Shot("09_deploy_prompt");
                yield return Tap(Key.E); yield return Wait(3.4f);                    // lower the crate + let go
                yield return Shot("10_seis_deployed");

                m.TestTeleport(new Vector3(6f, 0, -10.5f)); yield return Wait(0.5f); yield return Tap(Key.E); yield return Wait(0.3f);
                foreach (var site in m.SampleSites)
                {
                    m.TestTeleport(site.transform.position + new Vector3(1.2f, 0, 0));
                    yield return Wait(0.5f); yield return Tap(Key.E); yield return Wait(3.8f);   // crouch, grab the rock, bag it
                }
                yield return Shot("11_sample_site");
                m.TestTeleport(m.World.AirlockPoint + new Vector3(1.5f, 0, 0)); yield return Wait(0.5f);
                yield return Tap(Key.E); yield return Wait(0.5f);
                for (float w = 0f; m.InCinematic && w < 40f; w += Time.unscaledDeltaTime) yield return null;   // hatch, airlock, suit off
                yield return Wait(0.5f);
                yield return Shot("12_inside_habitat");
                yield return Tap(Key.Tab); yield return Wait(0.6f);
                yield return Shot("13_systems_panel");
                yield return Tap(Key.Tab);
            }
            else
            {
                m.TestTeleport(m.World.AirlockPoint + new Vector3(1.5f, 0, 0)); yield return Wait(0.5f);
                yield return Tap(Key.E); yield return Wait(0.5f);
                for (float w = 0f; m.InCinematic && w < 40f; w += Time.unscaledDeltaTime) yield return null;
            }

            m.TestSpeed = 40f;
            bool shadowShot = false;
            while (m.Current == Mission1.Phase.Operations)
            {
                if (m.Model.Inside && m.Speed != 4) m.SetSpeed(4);
                if (m.Model.Inside) { m.Model.EatMeal(out _); m.Model.Drink(out _); }   // scripted crew eats and drinks
                if (full && !shadowShot && m.Model.Hour > 41f) { shadowShot = true; m.TestSpeed = 0f; yield return Wait(1f); yield return Shot("14_shadow_window"); m.TestSpeed = 40f; }
                yield return null;
            }
            yield return Wait(3.5f);
            var mo = m.Model;
            Debug.Log($"[AUTOTEST] RESULT failed={mo.Failed} cause='{mo.FailCause}' hour={mo.Hour:0.0} score={m.Report.Score} primary={mo.PrimaryDone}/3 " +
                      $"seisGood={mo.SeisGoodHours:0.0} rad={mo.RadReadings} samples={mo.SamplesDelivered} o2={mo.CabinO2:0.00} water={mo.Water:0.0} " +
                      $"battery={mo.Battery:0.0} quake={mo.QuakeRecorded} dose={mo.Dose:0.0} events={mo.Events.Count}");
            foreach (var c in mo.CauseChain) Debug.Log("[AUTOTEST] cause: " + c);
            yield return Shot(full ? "15_report" : "16_failure_report");
        }
    }
}

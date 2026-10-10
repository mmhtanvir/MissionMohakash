using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Mohakash
{
    /// <summary>
    /// First-launch intro: plays the team's MP4 from StreamingAssets/Intro full-screen (aspect preserved, letterboxed),
    /// shows Skip after 2 s, then loads the main menu. Plays once per launch. If no video is present or it fails, goes straight to the menu.
    /// </summary>
    public class IntroPlayer : MonoBehaviour
    {
        public const float SkipDelay = 2f;
        static readonly string[] Names = { "VIDEO-2026-10-08-16-07-16.mp4", "intro.mp4" };

        VideoPlayer vp;
        bool leaving, left;

        IEnumerator Start()
        {
            if (Game.IntroShown) { Leave(); yield break; }
            Game.IntroShown = true;

            string url = FindVideo();
            if (url == null) { Debug.Log("[Intro] No intro video in StreamingAssets/Intro - skipping to menu."); Leave(); yield break; }

            var cam = SceneKit.MakeCamera();
            cam.backgroundColor = Color.black;
            vp = cam.gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.url = url;
            vp.renderMode = VideoRenderMode.CameraNearPlane;
            vp.targetCamera = cam;
            vp.aspectRatio = VideoAspectRatio.FitInside;   // preserve aspect ratio, black bars
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.isLooping = false;
            vp.errorReceived += (_, msg) => { Debug.LogWarning("[Intro] Video error: " + msg); Leave(); };
            vp.loopPointReached += _ => Leave();
            vp.Prepare();

            var root = Ui.CreateRoot();
            var skip = Ui.Button(root, "Skip  >", Leave, "btn-big", "skip");
            Ui.Show(skip, false);
            Game.SetCursorFree(true);

            float t = 0;
            while (!vp.isPrepared && t < 8f && !leaving) { t += Time.unscaledDeltaTime; yield return null; }
            if (!vp.isPrepared) { Debug.LogWarning("[Intro] Video did not prepare in time."); Leave(); yield break; }
            vp.Play();
            Debug.Log($"[Intro] Playing {Path.GetFileName(url)} ({vp.width}x{vp.height}, {vp.length:0.0} s)");

            float shown = 0;
            while (!leaving)
            {
                shown += Time.unscaledDeltaTime;
                if (shown >= SkipDelay)
                {
                    Ui.Show(skip, true);
                    if (GameInput.Pause || GameInput.Jump || UnityEngine.InputSystem.Keyboard.current?.enterKey.wasPressedThisFrame == true) Leave();
                }
                yield return null;
            }
        }

        static string FindVideo()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Intro");
#if UNITY_WEBGL && !UNITY_EDITOR
            return dir + "/intro.mp4";   // WebGL cannot list files; name the file intro.mp4 for web builds
#else
            foreach (var n in Names) { var p = Path.Combine(dir, n); if (File.Exists(p)) return p; }
            if (Directory.Exists(dir)) foreach (var p in Directory.GetFiles(dir, "*.mp4")) return p;
            return null;
#endif
        }

        void Leave()
        {
            if (left) return;
            left = leaving = true;
            if (vp) vp.Stop();
            Game.Load(Game.MenuScene);
            enabled = false;
        }
    }
}

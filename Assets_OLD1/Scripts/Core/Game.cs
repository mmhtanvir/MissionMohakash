using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mohakash
{
    /// <summary>Scene flow, persistent settings and mission unlocks.</summary>
    public static class Game
    {
        public const string IntroScene = "Intro", MenuScene = "MainMenu", Mission1Scene = "Mission1";

        /// <summary>True once the intro has been shown in this launch, so returning to the menu never replays it.</summary>
        public static bool IntroShown;

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("mouseSens", 1f);
            set { PlayerPrefs.SetFloat("mouseSens", value); PlayerPrefs.Save(); }
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt("invertY", 0) == 1;
            set { PlayerPrefs.SetInt("invertY", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat("volume", 0.8f);
            set { PlayerPrefs.SetFloat("volume", value); AudioListener.volume = value; PlayerPrefs.Save(); }
        }

        public static bool Hints
        {
            get => PlayerPrefs.GetInt("hints", 1) == 1;
            set { PlayerPrefs.SetInt("hints", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Mission n (1-4) is unlocked when mission n-1 was passed.</summary>
        public static bool IsUnlocked(int mission) => mission <= 1 || PlayerPrefs.GetInt("passed" + (mission - 1), 0) == 1;
        public static void MarkPassed(int mission) { PlayerPrefs.SetInt("passed" + mission, 1); PlayerPrefs.Save(); }
        public static int BestScore(int mission) => PlayerPrefs.GetInt("best" + mission, 0);
        public static void RecordScore(int mission, int score)
        {
            if (score > BestScore(mission)) { PlayerPrefs.SetInt("best" + mission, score); PlayerPrefs.Save(); }
        }

        public static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public static void SetCursorFree(bool free)
        {
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmotionBank
{
    public class MainMenuManager : MonoBehaviour
    {
        public string gameSceneName = "Level"; // or whatever yours is

        public GameObject menuRoot; // <- assign your Canvas or top-level menu object
        public GameObject TitleMenu;

        public void OnStartGame()
        {
            TitleMenu.SetActive(false);

            // Hide/destroy the menu in case it's on a DontDestroyOnLoad object
            if (menuRoot != null)
                Destroy(menuRoot);

            // Load the main game scene, replacing the current one
            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        public void OnQuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
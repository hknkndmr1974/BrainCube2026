using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private const string PREFS_WORLD = "SavedWorld";
    private const string PREFS_LEVEL = "SavedLevel";

    public void PlayNewGame()
    {
        // Kaydedilen seviyeyi oku (varsayılan 1-1)
        int savedWorld = PlayerPrefs.GetInt(PREFS_WORLD, 1);
        int savedLevel = PlayerPrefs.GetInt(PREFS_LEVEL, 1);

        LevelLoader.SelectedWorld = savedWorld;
        LevelLoader.SelectedLevel = savedLevel;
        
        Debug.Log($"Starting Game from Last Played: World {savedWorld}, Level {savedLevel}");
        SceneManager.LoadScene("BrainCube");
    }

    public void ContinueGame()
    {
        // Kaydedilen seviyeyi oku (varsayılan 1-1)
        int savedWorld = PlayerPrefs.GetInt(PREFS_WORLD, 1);
        int savedLevel = PlayerPrefs.GetInt(PREFS_LEVEL, 1);

        LevelLoader.SelectedWorld = savedWorld;
        LevelLoader.SelectedLevel = savedLevel;

        Debug.Log($"Continuing Game: World {savedWorld}, Level {savedLevel}");
        SceneManager.LoadScene("BrainCube");
    }

    public void LoadSpecificLevel(int levelIndex)
    {
        LoadWorldLevel(1, levelIndex);
    }

    public void LoadWorldLevel(int worldIndex, int levelIndex)
    {
        LevelLoader.SelectedWorld = worldIndex;
        LevelLoader.SelectedLevel = levelIndex;

        Debug.Log($"Loading Selected Level: World {worldIndex}, Level {levelIndex}");
        SceneManager.LoadScene("BrainCube");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

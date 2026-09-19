using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSelectButton : MonoBehaviour
{
    [Header("Level Info")]
    public int worldIndex = 1;
    public int levelIndex = 1;

    public void OnClick()
    {
        // Seviye bilgilerini statik olarak ata
        LevelLoader.SelectedWorld = worldIndex;
        LevelLoader.SelectedLevel = levelIndex;

        Debug.Log($"[LevelSelectButton] Loading World {worldIndex}, Level {levelIndex}...");
        SceneManager.LoadScene("BrainCube");
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Call this method to restart the scene
    public void RestartScene()
    {
        Time.timeScale = 1f;

        // Get the index of the currently active scene
        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // Reload the currently active scene
        SceneManager.LoadScene(activeSceneIndex);
    }
}

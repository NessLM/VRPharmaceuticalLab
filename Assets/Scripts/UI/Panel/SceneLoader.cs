using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadVRLabSimulation()
    {
        SceneManager.LoadScene("VRLabSimulation");
    }
}
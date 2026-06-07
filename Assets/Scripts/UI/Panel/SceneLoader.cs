using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadVRLabSimulation()
    {
        SceneManager.LoadScene("VRLabSimulation");
    }

    public void LoadVRLabSimulationPadat()
    {
        SceneManager.LoadScene("VRLabSimulation_Padat");
    }
}
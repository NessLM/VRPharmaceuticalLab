using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnableButtonAfterDelay : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private float delay = 1f;

    private void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        StartCoroutine(EnableAfterDelay());
    }

    private IEnumerator EnableAfterDelay()
    {
        button.interactable = false;
        yield return new WaitForSeconds(delay);
        button.interactable = true;
    }
}
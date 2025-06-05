using UnityEngine;
using UnityEngine.UI;

public class PanelTrigger : MonoBehaviour
{

    [SerializeField] private PanelEvent panelEvent;
    [SerializeField] private int idPanel;

    void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => ShowPanel(idPanel));
    }

    public void ShowPanel(int idPanel) => panelEvent.Raise(idPanel);
}

using System.Collections.Generic;
using UnityEngine;

public class PanelManager : GameEventListener<int>
{
    [SerializeField] List<GameObject> panels = new List<GameObject>();

    public void ShowPanel(int idPanel)
    {
        for (int i = 0; i < panels.Count; i++)
        {
            if (i == idPanel)
                panels[i].SetActive(true);
            else
                panels[i].SetActive(false);
        }
    }
}

using TMPro;
using UnityEngine;

public class Step1ChecklistManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;

    public void CheckCTMWeight() => CheckLine("Anak timbangan CTM");
    public void CheckCTMPerkamen() => CheckLine("Kertas perkamen CTM");
    public void CheckCTMBottle() => CheckLine("Botol CTM");
    public void CheckCTMDone() => CheckLine("CTM selesai");

    public void CheckParaWeight() => CheckLine("Anak timbangan Paracetamol");
    public void CheckParaPerkamen() => CheckLine("Kertas perkamen Paracetamol");
    public void CheckParaBottle() => CheckLine("Botol Paracetamol");
    public void CheckParaDone() => CheckLine("Paracetamol selesai");

    private void CheckLine(string keyword)
    {
        if (checklistText == null) return;

        string[] lines = checklistText.text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(keyword))
            {
                lines[i] = "[OK] " + lines[i].Replace("☐", "").Replace("☑", "").Trim();
                checklistText.text = string.Join("\n", lines);
                Debug.Log("Checklist berubah: " + keyword);
                return;
            }
        }

        Debug.LogWarning("Checklist tidak menemukan keyword: " + keyword);
    }
}
using TMPro;
using UnityEngine;

public class Step2ChecklistManager : MonoBehaviour
{
    [SerializeField] private TMP_Text checklistText;

    public void CheckCTM() => CheckLine("Masukkan CTM");
    public void CheckParacetamol() => CheckLine("Masukkan Paracetamol");
    public void CheckGrinding() => CheckLine("Gerus bahan");
    public void CheckSpatula() => CheckLine("Kumpulkan bubuk");

    private void CheckLine(string keyword)
    {
        if (checklistText == null) return;

        string[] lines = checklistText.text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(keyword))
            {
                lines[i] = "[OK] " + lines[i]
                    .Replace("[ ]", "")
                    .Replace("[OK]", "")
                    .Trim();

                checklistText.text = string.Join("\n", lines);
                Debug.Log("Checklist Step 2 berubah: " + keyword);
                return;
            }
        }

        Debug.LogWarning("Checklist Step 2 tidak menemukan keyword: " + keyword);
    }
}
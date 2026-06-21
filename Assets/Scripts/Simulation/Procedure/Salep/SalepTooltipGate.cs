using UnityEngine;

/// <summary>
/// Menyembunyikan tooltip affordance generik (mis. "Grab", "UI Press", "Turn")
/// selama simulasi Salep berjalan, supaya tidak memenuhi layar.
///
/// Wiring (opsional): tambahkan komponen ini ke [SYS] SalepProcedureSystem,
/// isi <see cref="tooltipsToHideDuringSimulation"/> dengan object tooltip yang ingin
/// disembunyikan. Dipanggil oleh SalepIntroPanelController saat start/reset.
/// Jika list kosong, komponen ini no-op (aman).
/// </summary>
[DisallowMultipleComponent]
public sealed class SalepTooltipGate : MonoBehaviour
{
    [Tooltip("Object tooltip affordance yang disembunyikan selama simulasi Salep aktif.")]
    [SerializeField] private GameObject[] tooltipsToHideDuringSimulation;

    [Tooltip("Restore state aktif awal saat simulasi berhenti/reset.")]
    [SerializeField] private bool restoreOnDeactivate = true;

    private bool[] cachedActiveStates;
    private bool cached;

    public void SetSimulationActive(bool active)
    {
        if (tooltipsToHideDuringSimulation == null || tooltipsToHideDuringSimulation.Length == 0)
            return;

        if (active)
        {
            CacheStatesOnce();
            foreach (GameObject tooltip in tooltipsToHideDuringSimulation)
            {
                if (tooltip != null)
                    tooltip.SetActive(false);
            }
        }
        else if (restoreOnDeactivate && cached)
        {
            for (int i = 0; i < tooltipsToHideDuringSimulation.Length; i++)
            {
                GameObject tooltip = tooltipsToHideDuringSimulation[i];
                if (tooltip != null && i < cachedActiveStates.Length)
                    tooltip.SetActive(cachedActiveStates[i]);
            }
        }
    }

    private void CacheStatesOnce()
    {
        if (cached)
            return;

        cachedActiveStates = new bool[tooltipsToHideDuringSimulation.Length];
        for (int i = 0; i < tooltipsToHideDuringSimulation.Length; i++)
        {
            GameObject tooltip = tooltipsToHideDuringSimulation[i];
            cachedActiveStates[i] = tooltip != null && tooltip.activeSelf;
        }

        cached = true;
    }
}

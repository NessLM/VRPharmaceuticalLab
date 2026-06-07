using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ResepPadat1StepManager : MonoBehaviour
{
    [Header("Interactables")]
    [SerializeField] private XRGrabInteractable sendokTanduk;
    [SerializeField] private XRGrabInteractable botolCTM;
    [SerializeField] private XRGrabInteractable botolParacetamol;
    [SerializeField] private XRGrabInteractable mortar;
    [SerializeField] private XRGrabInteractable penumbuk;

    [Header("Panels")]
    [SerializeField] private GameObject panelResep;
    [SerializeField] private GameObject instruksiStep1;

    private int currentStep = 0;

    private void Start()
    {
        LockAll();
        if (instruksiStep1 != null)
            instruksiStep1.SetActive(false);
    }

    public void StartSimulation()
    {
        if (panelResep != null)
            panelResep.SetActive(false);

        SetStep(1);
    }

    public void SetStep(int step)
    {
        currentStep = step;

        LockAll();

        if (instruksiStep1 != null)
            instruksiStep1.SetActive(false);

        switch (currentStep)
        {
            case 1:
                if (sendokTanduk != null)
                    sendokTanduk.enabled = true;

                if (instruksiStep1 != null)
                    instruksiStep1.SetActive(true);

                Debug.Log("Step 1 aktif: Ambil sendok tanduk.");
                break;
        }
    }

    private void LockAll()
    {
        if (sendokTanduk != null) sendokTanduk.enabled = false;
        if (botolCTM != null) botolCTM.enabled = false;
        if (botolParacetamol != null) botolParacetamol.enabled = false;
        if (mortar != null) mortar.enabled = false;
        if (penumbuk != null) penumbuk.enabled = false;
    }
}
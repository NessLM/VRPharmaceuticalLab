using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ResepPadat1StepManager : MonoBehaviour
{
    [Header("SEMUA barang yang bisa di-grab")]
    [SerializeField] private XRGrabInteractable[] allGrabItems;

    [Header("Barang Step 1")]
    [SerializeField] private XRGrabInteractable[] step1GrabItems;

    [Header("Barang Step 2")]
    [SerializeField] private XRGrabInteractable[] step2GrabItems;

    [Header("Barang Step 3")]
    [SerializeField] private XRGrabInteractable[] step3GrabItems;

    [Header("Barang Step 4")]
[SerializeField] private XRGrabInteractable[] step4GrabItems;

    [Header("Script Khusus")]
    [SerializeField] private MonoBehaviour stackPerkamenScript;
    [SerializeField] private MonoBehaviour botolKapsulScript;

    [Header("Tutup Botol / Simple Interactable")]
    [SerializeField] private MonoBehaviour tutupCTMInteractable;
    [SerializeField] private MonoBehaviour tutupParacetamolInteractable;
    [SerializeField] private MonoBehaviour tutupBotolKapsulInteractable;
    [SerializeField] private MonoBehaviour stackGridPerkamenScript;

    [Header("Panels")]
    [SerializeField] private GameObject panelResep;
    [SerializeField] private GameObject instruksiStep1;
    [SerializeField] private GameObject instruksiStep2;
    [SerializeField] private GameObject instruksiStep3;
    [SerializeField] private GameObject instruksiStep4;

    private bool simulationStarted = false;

    private void Awake()
    {
        Debug.Log("MANAGER RESEP PADAT 1 JALAN");
        LockBeforeStart();
    }

    private void Start()
    {
        LockBeforeStart();
    }

    private void LockBeforeStart()
    {
        simulationStarted = false;

        LockItems(allGrabItems);

        SetScript(stackPerkamenScript, false);
        SetScript(botolKapsulScript, false);

        SetScript(tutupCTMInteractable, false);
        SetScript(tutupParacetamolInteractable, false);
        SetScript(tutupBotolKapsulInteractable, false);
        SetScript(stackGridPerkamenScript, false);

        SetPanel(instruksiStep1, false);
        SetPanel(instruksiStep2, false);
        SetPanel(instruksiStep3, false);
        SetPanel(instruksiStep4, false);

        Debug.Log("Sebelum Mulai: semua grab dan script khusus dimatikan.");
    }

    public void StartSimulation()
    {
        simulationStarted = true;

        if (panelResep != null)
            panelResep.SetActive(false);

        SetStep(1);
    }

    public void SetStep(int step)
    {
        if (!simulationStarted)
            return;

        LockItems(allGrabItems);

        SetScript(stackPerkamenScript, false);
        SetScript(botolKapsulScript, false);

        SetScript(tutupCTMInteractable, false);
        SetScript(tutupParacetamolInteractable, false);
        SetScript(tutupBotolKapsulInteractable, false);
        SetScript(stackGridPerkamenScript, false);

        SetPanel(instruksiStep1, false);
        SetPanel(instruksiStep2, false);
        SetPanel(instruksiStep3, false);
        SetPanel(instruksiStep4, false);

        if (step == 1)
        {
            UnlockItems(step1GrabItems);

            SetScript(stackPerkamenScript, true);

            // Step 1 mulai dari fase CTM dulu.
            // Tutup Paracetamol tetap mati sampai CTM selesai.
            SetScript(tutupCTMInteractable, true);
            SetScript(tutupParacetamolInteractable, false);

            SetScript(stackPerkamenScript, true);
SetScript(stackGridPerkamenScript, false);

            SetPanel(instruksiStep1, true);

            Debug.Log("Step 1 aktif: Fase CTM aktif, Paracetamol masih terkunci.");
        }
        else if (step == 2)
        {
            UnlockItems(step2GrabItems);

            SetPanel(instruksiStep2, true);

            Debug.Log("Step 2 aktif.");
        }
        else if (step == 3)
        {
            UnlockItems(step3GrabItems);

            SetScript(stackPerkamenScript, false);
SetScript(stackGridPerkamenScript, true);

            SetPanel(instruksiStep3, true);

            Debug.Log("Step 3 aktif.");
        }
        else if (step == 4)
{
    UnlockItems(step4GrabItems);

    SetScript(botolKapsulScript, true);
    SetScript(tutupBotolKapsulInteractable, true);

    SetPanel(instruksiStep4, true);

    Debug.Log("Step 4 aktif.");
}
    }

    private void LockItems(XRGrabInteractable[] items)
    {
        if (items == null) return;

        foreach (XRGrabInteractable item in items)
        {
            if (item == null) continue;

            item.enabled = false;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
            }
        }
    }

    private void UnlockItems(XRGrabInteractable[] items)
    {
        if (items == null) return;

        foreach (XRGrabInteractable item in items)
        {
            if (item == null) continue;

            item.enabled = true;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
            }
        }
    }

    private void SetScript(MonoBehaviour script, bool active)
    {
        if (script == null) return;
        script.enabled = active;
    }

    private void SetPanel(GameObject panel, bool active)
    {
        if (panel == null) return;
        panel.SetActive(active);
    }
}
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Step1IngredientPhaseManager : MonoBehaviour
{
    [Header("Paracetamol - Aktif setelah CTM selesai")]
    [SerializeField] private XRSimpleInteractable tutupParacetamolInteractable;
    [SerializeField] private XRGrabInteractable botolParacetamolGrab;
    [SerializeField] private GameObject paracetamolScoopTrigger;

    [Header("Trigger Timbangan Kanan")]
    [SerializeField] private GameObject ctmWeightSnapObject;
    [SerializeField] private GameObject paracetamolWeightSnapObject;

    [Header("Trigger Perkamen Kiri")]
    [SerializeField] private GameObject ctmPerkamenSnapObject;
    [SerializeField] private GameObject paracetamolPerkamenSnapObject;

    private void Start()
    {
        LockParacetamol();
    }

    public void LockParacetamol()
    {
        if (tutupParacetamolInteractable != null)
            tutupParacetamolInteractable.enabled = false;

        if (botolParacetamolGrab != null)
            botolParacetamolGrab.enabled = false;

        if (paracetamolScoopTrigger != null)
            paracetamolScoopTrigger.SetActive(false);

        if (ctmWeightSnapObject != null)
            ctmWeightSnapObject.SetActive(true);

        if (paracetamolWeightSnapObject != null)
            paracetamolWeightSnapObject.SetActive(false);

        if (ctmPerkamenSnapObject != null)
            ctmPerkamenSnapObject.SetActive(true);

        if (paracetamolPerkamenSnapObject != null)
            paracetamolPerkamenSnapObject.SetActive(false);

        Debug.Log("Paracetamol dikunci. CTM aktif.");
    }

    public void EnableParacetamolPhase()
    {
        if (tutupParacetamolInteractable != null)
            tutupParacetamolInteractable.enabled = true;

        if (botolParacetamolGrab != null)
            botolParacetamolGrab.enabled = false;

        if (paracetamolScoopTrigger != null)
            paracetamolScoopTrigger.SetActive(false);

        if (ctmWeightSnapObject != null)
            ctmWeightSnapObject.SetActive(false);

        if (paracetamolWeightSnapObject != null)
            paracetamolWeightSnapObject.SetActive(true);

        if (ctmPerkamenSnapObject != null)
            ctmPerkamenSnapObject.SetActive(false);

        if (paracetamolPerkamenSnapObject != null)
            paracetamolPerkamenSnapObject.SetActive(true);

        Debug.Log("Fase Paracetamol aktif. Trigger CTM mati, trigger Paracetamol aktif.");
    }
}
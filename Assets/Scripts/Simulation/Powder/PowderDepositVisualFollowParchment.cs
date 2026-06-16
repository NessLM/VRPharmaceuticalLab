using UnityEngine;

[DisallowMultipleComponent]
public class PowderDepositVisualFollowParchment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform plateTarget;

    [Header("Follow")]
    [SerializeField] private bool autoFindParchment = true;
    [SerializeField] private bool followRotation = true;

    [Tooltip("Naikkan kalau bubuk masih tenggelam di perkamen.")]
    [SerializeField] private float surfaceOffset = 0.006f;

    [Tooltip("Offset lokal dari tengah perkamen. Pakai ini kalau bubuk kurang center.")]
    [SerializeField] private Vector3 localPlanarOffset = Vector3.zero;

    [Tooltip("Koreksi rotasi kalau visual bubuk miring.")]
    [SerializeField] private Vector3 localEulerOffset = Vector3.zero;

    [Header("When No Parchment")]
    [SerializeField] private bool hideLevelObjectsWhenNoParchment = true;

    [Header("Debug")]
    [SerializeField] private Transform currentParchment;

    private PowderVisualLevelSwitcher levelSwitcher;

    private void Awake()
    {
        if (plateTarget == null && transform.parent != null)
            plateTarget = transform.parent;

        levelSwitcher = GetComponent<PowderVisualLevelSwitcher>();
    }

    private void LateUpdate()
    {
        Transform parchment = ResolveParchment();
        currentParchment = parchment;

        if (parchment == null)
        {
            if (hideLevelObjectsWhenNoParchment && levelSwitcher != null)
                levelSwitcher.Clear();

            return;
        }

        Vector3 targetPosition =
            parchment.position +
            parchment.up * surfaceOffset +
            parchment.TransformDirection(localPlanarOffset);

        transform.position = targetPosition;

        if (followRotation)
            transform.rotation = parchment.rotation * Quaternion.Euler(localEulerOffset);
    }

    private Transform ResolveParchment()
    {
        if (!autoFindParchment || plateTarget == null)
            return null;

        PerkamenNoGravity[] parchmentStates = plateTarget.GetComponentsInChildren<PerkamenNoGravity>(true);

        foreach (PerkamenNoGravity parchment in parchmentStates)
        {
            if (parchment != null)
                return parchment.transform;
        }

        Transform[] children = plateTarget.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == null || child == transform)
                continue;

            string lowerName = child.name.ToLowerInvariant();

            if (lowerName.Contains("perkamen") || lowerName.Contains("parchment"))
                return child;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
    }
#endif
}
using UnityEngine;
using System.Collections.Generic;

public class MenuTeleportPlayer : MonoBehaviour
{
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform targetPoint;

    private static readonly Dictionary<int, Pose> InitialPoses = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        InitialPoses.Clear();
    }

    private void Awake()
    {
        CaptureInitialPose();
    }

    public void TeleportFromMenu()
    {
        if (xrOrigin == null || targetPoint == null)
        {
            Debug.LogWarning("XR Origin atau Target Point belum diisi.");
            return;
        }

        CaptureInitialPose();
        xrOrigin.position = targetPoint.position;
        xrOrigin.rotation = targetPoint.rotation;
    }

    public void ResetPlayerToInitialPose()
    {
        if (xrOrigin == null)
            return;

        CaptureInitialPose();

        if (!InitialPoses.TryGetValue(xrOrigin.GetInstanceID(), out Pose initialPose))
            return;

        xrOrigin.SetPositionAndRotation(initialPose.position, initialPose.rotation);
    }

    private void CaptureInitialPose()
    {
        if (xrOrigin == null)
            return;

        int key = xrOrigin.GetInstanceID();
        if (!InitialPoses.ContainsKey(key))
            InitialPoses.Add(key, new Pose(xrOrigin.position, xrOrigin.rotation));
    }
}

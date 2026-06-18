using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SpoonPowderPlateTransfer : MonoBehaviour
{
    private enum TransferMode
    {
        None,
        ScoopFromPlate,
        PourToMortar
    }

    [Header("References")]
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Transform spoonTip;
    [SerializeField] private SpoonActionPrompt actionPrompt;

    [Header("Powder Source - Piring Kiri")]
    [SerializeField] private PowderDepositZone powderDepositZone;
    [SerializeField] private Transform powderSourcePoint;

    [Header("Mortar Receiver")]
    [SerializeField] private Component mortarReceiver;
    [SerializeField] private Transform mortarTargetPoint;

    [Header("Transfer Settings")]
    [SerializeField] private bool transferEnabled = false;
    [SerializeField] private float transferStepMg = 50f;
    [SerializeField] private float sourceDetectRadius = 0.22f;
    [SerializeField] private float mortarDetectRadius = 0.25f;
    [SerializeField] private bool requireHeld = true;
    [SerializeField] private bool onlyTakeWhenSpoonEmpty = true;
    [SerializeField] private float cooldownAfterTransfer = 0.25f;

    [Header("Prompt Text")]
    [SerializeField] private string scoopPromptText = "Scoop\n<size=65%>Ambil bubuk</size>";
    [SerializeField] private string pourPromptText = "Trigger\n<size=65%>Tuang ke Mortar</size>";
    [SerializeField] private string carryPromptText = "Bawa ke Mortar";

    [Header("Tilt Animation")]
    [SerializeField] private bool useTiltAnimation = true;
    [SerializeField] private Transform animatedRoot;
    [SerializeField] private Vector3 scoopTiltEuler = new Vector3(-25f, 0f, 0f);
    [SerializeField] private Vector3 pourTiltEuler = new Vector3(35f, 0f, 0f);
    [SerializeField] private float tiltInDuration = 0.12f;
    [SerializeField] private float holdTiltDuration = 0.08f;
    [SerializeField] private float tiltOutDuration = 0.16f;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem scoopFx;
    [SerializeField] private ParticleSystem pourFx;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool showDistanceLogs;

    private readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private bool isBusy;
    private float nextAllowedTransferTime;
    private Quaternion baseLocalRotation;

    private void Awake()
    {
        if (hornSpoon == null)
            hornSpoon = GetComponent<HornSpoon>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (spoonTip == null && hornSpoon != null)
            spoonTip = hornSpoon.TipTransform;

        if (actionPrompt == null)
            actionPrompt = GetComponent<SpoonActionPrompt>();

        if (animatedRoot == null)
            animatedRoot = transform;

        baseLocalRotation = animatedRoot.localRotation;
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.activated.AddListener(OnActivated);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.activated.RemoveListener(OnActivated);

        if (actionPrompt != null)
            actionPrompt.ClearExternalPrompt();
    }

    private void Update()
    {
        UpdateStep4Prompt();
    }

    public void SetTransferEnabled(bool value)
    {
        transferEnabled = value;

        if (!transferEnabled && actionPrompt != null)
            actionPrompt.ClearExternalPrompt();
    }

    private void OnActivated(ActivateEventArgs args)
    {
        if (!transferEnabled)
        {
            Log("Rejected: Step 4 transfer belum aktif.");
            return;
        }

        if (isBusy)
        {
            Log("Rejected: masih animasi transfer.");
            return;
        }

        if (Time.time < nextAllowedTransferTime)
        {
            Log("Rejected: cooldown.");
            return;
        }

        if (requireHeld && (grabInteractable == null || !grabInteractable.isSelected))
        {
            Log("Rejected: sendok belum di-grab.");
            return;
        }

        if (hornSpoon == null)
        {
            Log("Rejected: HornSpoon belum diisi.");
            return;
        }

        if (spoonTip == null)
        {
            Log("Rejected: SpoonTip belum diisi.");
            return;
        }

        TransferMode mode = GetCurrentTransferMode();

        if (mode == TransferMode.ScoopFromPlate)
        {
            StartCoroutine(TransferRoutine(mode));
            return;
        }

        if (mode == TransferMode.PourToMortar)
        {
            StartCoroutine(TransferRoutine(mode));
            return;
        }

        Log($"No valid target. Source distance = {GetDistance(powderSourcePoint):0.000}, Mortar distance = {GetDistance(mortarTargetPoint):0.000}, SpoonMg = {GetSpoonAmountMg():0.###}");
    }

    private IEnumerator TransferRoutine(TransferMode mode)
    {
        isBusy = true;

        bool success = false;

        Quaternion startRotation = animatedRoot != null ? animatedRoot.localRotation : Quaternion.identity;
        Quaternion targetRotation = startRotation;

        if (useTiltAnimation && animatedRoot != null)
        {
            Vector3 tiltEuler = mode == TransferMode.ScoopFromPlate ? scoopTiltEuler : pourTiltEuler;
            targetRotation = startRotation * Quaternion.Euler(tiltEuler);

            yield return AnimateLocalRotation(startRotation, targetRotation, tiltInDuration);
        }

        if (mode == TransferMode.ScoopFromPlate)
            success = TryTakeFromPlate();

        if (mode == TransferMode.PourToMortar)
            success = TryPourToMortar();

        if (success)
        {
            if (mode == TransferMode.ScoopFromPlate)
                PlayFxAt(scoopFx, powderSourcePoint);

            if (mode == TransferMode.PourToMortar)
                PlayFxAt(pourFx, mortarTargetPoint);
        }

        if (holdTiltDuration > 0f)
            yield return new WaitForSeconds(holdTiltDuration);

        if (useTiltAnimation && animatedRoot != null)
            yield return AnimateLocalRotation(animatedRoot.localRotation, startRotation, tiltOutDuration);

        nextAllowedTransferTime = Time.time + cooldownAfterTransfer;
        isBusy = false;
    }

    private IEnumerator AnimateLocalRotation(Quaternion from, Quaternion to, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.001f, duration));
            animatedRoot.localRotation = Quaternion.Slerp(from, to, a);
            yield return null;
        }

        animatedRoot.localRotation = to;
    }

    private TransferMode GetCurrentTransferMode()
    {
        bool nearSource = IsNear(powderSourcePoint, sourceDetectRadius);
        bool nearMortar = IsNear(mortarTargetPoint, mortarDetectRadius);
        float spoonMg = GetSpoonAmountMg();

        if (nearMortar && spoonMg > 0.1f)
            return TransferMode.PourToMortar;

        if (nearSource && spoonMg <= 0.1f && powderDepositZone != null && powderDepositZone.HasPowder)
            return TransferMode.ScoopFromPlate;

        return TransferMode.None;
    }

    private void UpdateStep4Prompt()
    {
        if (actionPrompt == null)
            return;

        if (!transferEnabled)
        {
            actionPrompt.ClearExternalPrompt();
            return;
        }

        if (grabInteractable == null || !grabInteractable.isSelected)
        {
            actionPrompt.ClearExternalPrompt();
            return;
        }

        bool nearSource = IsNear(powderSourcePoint, sourceDetectRadius);
        bool nearMortar = IsNear(mortarTargetPoint, mortarDetectRadius);
        float spoonMg = GetSpoonAmountMg();

        if (nearMortar && spoonMg > 0.1f)
        {
            actionPrompt.SetExternalPrompt(pourPromptText);
            return;
        }

        if (nearSource && spoonMg <= 0.1f && powderDepositZone != null && powderDepositZone.HasPowder)
        {
            actionPrompt.SetExternalPrompt(scoopPromptText);
            return;
        }

        if (spoonMg > 0.1f)
        {
            actionPrompt.SetExternalPrompt(carryPromptText);
            return;
        }

        actionPrompt.ClearExternalPrompt();

        if (showDistanceLogs)
            Log($"Prompt none. Source distance = {GetDistance(powderSourcePoint):0.000}, Mortar distance = {GetDistance(mortarTargetPoint):0.000}");
    }

    private bool TryTakeFromPlate()
    {
        if (powderDepositZone == null)
        {
            Log("Rejected: PowderDepositZone belum diisi.");
            return false;
        }

        if (onlyTakeWhenSpoonEmpty && !hornSpoon.IsEmpty)
        {
            Log("Rejected: sendok masih ada bubuk. Tuang dulu ke mortar.");
            return false;
        }

        float availableMg = powderDepositZone.DepositedMg;

        if (availableMg <= 0.1f)
        {
            Log("Rejected: bubuk di piring kiri sudah habis.");
            return false;
        }

        float takeRequestMg = Mathf.Min(transferStepMg, availableMg);
        float addedToSpoonMg = AddPowderToSpoon(takeRequestMg);

        if (addedToSpoonMg <= 0.1f)
        {
            Log("Rejected: gagal memasukkan bubuk ke sendok.");
            return false;
        }

        float remainingPlateMg = Mathf.Max(0f, availableMg - addedToSpoonMg);
        powderDepositZone.SetDepositMg(remainingPlateMg);

        Log($"TAKE OK: sendok ambil {addedToSpoonMg:0.###} mg dari piring kiri. Sisa piring = {remainingPlateMg:0.###} mg.");
        return true;
    }

    private bool TryPourToMortar()
    {
        Component receiver = ResolveMortarReceiver();

        if (receiver == null)
        {
            Log("Rejected: MortarReceiver belum diisi atau tidak ada MortarController.");
            return false;
        }

        float spoonMg = GetSpoonAmountMg();

        if (spoonMg <= 0.1f)
        {
            Log("Rejected: sendok kosong.");
            return false;
        }

        MortarController mortar = receiver as MortarController;

        if (mortar == null)
            mortar = receiver.GetComponent<MortarController>();

        if (mortar == null)
            mortar = receiver.GetComponentInParent<MortarController>();

        if (mortar == null)
            mortar = receiver.GetComponentInChildren<MortarController>();

        float removedFromSpoonMg = RemovePowderFromSpoon(spoonMg);

        if (removedFromSpoonMg <= 0.1f)
        {
            Log("Rejected: gagal mengurangi bubuk dari sendok.");
            return false;
        }

        float acceptedByMortarMg = 0f;

        if (mortar != null)
            mortar.SetAcceptingPowderTransfer(true);

        try
        {
            acceptedByMortarMg = AddPowderToMortar(receiver, removedFromSpoonMg);
        }
        finally
        {
            if (mortar != null)
                mortar.SetAcceptingPowderTransfer(false);
        }

        if (acceptedByMortarMg <= 0.1f)
        {
            AddPowderToSpoon(removedFromSpoonMg);
            Log("Rejected: mortar tidak menerima bubuk. Bubuk dikembalikan ke sendok.");
            return false;
        }

        float leftoverMg = removedFromSpoonMg - acceptedByMortarMg;

        if (leftoverMg > 0.1f)
            AddPowderToSpoon(leftoverMg);

        Log($"POUR OK: mortar menerima {acceptedByMortarMg:0.###} mg. Sisa balik ke sendok = {leftoverMg:0.###} mg.");
        return true;
    }

    private bool IsNear(Transform target, float radius)
    {
        if (target == null || spoonTip == null)
            return false;

        return Vector3.Distance(spoonTip.position, target.position) <= radius;
    }

    private float GetDistance(Transform target)
    {
        if (target == null || spoonTip == null)
            return -1f;

        return Vector3.Distance(spoonTip.position, target.position);
    }

    private Component ResolveMortarReceiver()
    {
        if (mortarReceiver == null)
            return null;

        if (mortarReceiver.GetType().Name == "MortarController")
            return mortarReceiver;

        MortarController direct = mortarReceiver.GetComponent<MortarController>();
        if (direct != null)
            return direct;

        MortarController parent = mortarReceiver.GetComponentInParent<MortarController>();
        if (parent != null)
            return parent;

        MortarController child = mortarReceiver.GetComponentInChildren<MortarController>();
        if (child != null)
            return child;

        return mortarReceiver;
    }

    private float GetSpoonAmountMg()
    {
        if (hornSpoon == null)
            return 0f;

        if (TryReadFloat(hornSpoon, out float value,
                "CurrentAmountMg",
                "currentAmountMg",
                "CurrentMg",
                "currentMg",
                "AmountMg",
                "amountMg"))
        {
            return Mathf.Max(0f, value);
        }

        if (hornSpoon.IsEmpty)
            return 0f;

        if (hornSpoon.IsFull)
            return transferStepMg;

        return transferStepMg;
    }

    private float AddPowderToSpoon(float amountMg)
    {
        if (hornSpoon == null)
            return 0f;

        if (TryInvokeFloatMethod(hornSpoon, amountMg, out float returnedValue,
                "AddPowder",
                "AddPowderMg",
                "SetPowder",
                "SetPowderMg"))
        {
            TryInvokeNoArgMethod(hornSpoon, "UpdateVisual", "RefreshVisual");
            return returnedValue >= 0f ? returnedValue : amountMg;
        }

        float before = GetSpoonAmountMg();
        float after = before + amountMg;

        if (TryWriteFloat(hornSpoon, after,
                "currentAmountMg",
                "CurrentAmountMg",
                "currentMg",
                "CurrentMg",
                "amountMg",
                "AmountMg"))
        {
            TryInvokeNoArgMethod(hornSpoon, "UpdateVisual", "RefreshVisual");
            return amountMg;
        }

        return 0f;
    }

    private float RemovePowderFromSpoon(float amountMg)
    {
        if (hornSpoon == null)
            return 0f;

        if (TryInvokeFloatMethod(hornSpoon, amountMg, out float returnedValue,
                "RemovePowder",
                "RemovePowderMg",
                "TakePowder",
                "TakePowderMg"))
        {
            TryInvokeNoArgMethod(hornSpoon, "UpdateVisual", "RefreshVisual");
            return returnedValue >= 0f ? returnedValue : amountMg;
        }

        float before = GetSpoonAmountMg();
        float removed = Mathf.Min(before, amountMg);
        float after = Mathf.Max(0f, before - removed);

        if (TryWriteFloat(hornSpoon, after,
                "currentAmountMg",
                "CurrentAmountMg",
                "currentMg",
                "CurrentMg",
                "amountMg",
                "AmountMg"))
        {
            TryInvokeNoArgMethod(hornSpoon, "UpdateVisual", "RefreshVisual");
            return removed;
        }

        return 0f;
    }

    private float AddPowderToMortar(Component receiver, float amountMg)
    {
        if (receiver == null)
            return 0f;

        if (TryInvokeFloatMethod(receiver, amountMg, out float returnedValue,
                "AddPowderMg",
                "AddPowder",
                "AddIngredientMg",
                "AddIngredient"))
        {
            TryInvokeNoArgMethod(receiver, "UpdateVisual", "RefreshVisual");
            return returnedValue >= 0f ? returnedValue : amountMg;
        }

        float before = 0f;

        TryReadFloat(receiver, out before,
            "CurrentAmountMg",
            "currentAmountMg",
            "CurrentMg",
            "currentMg",
            "AmountMg",
            "amountMg",
            "powderMg",
            "currentPowderMg");

        float after = before + amountMg;

        if (TryWriteFloat(receiver, after,
                "currentAmountMg",
                "CurrentAmountMg",
                "currentMg",
                "CurrentMg",
                "amountMg",
                "AmountMg",
                "powderMg",
                "currentPowderMg"))
        {
            TryInvokeNoArgMethod(receiver, "UpdateVisual", "RefreshVisual");
            return amountMg;
        }

        return 0f;
    }

    private bool TryReadFloat(object target, out float value, params string[] names)
    {
        value = 0f;

        if (target == null)
            return false;

        Type type = target.GetType();

        foreach (string name in names)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return ConvertToFloat(field.GetValue(target), out value);

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
                return ConvertToFloat(property.GetValue(target), out value);
        }

        return false;
    }

    private bool TryWriteFloat(object target, float value, params string[] names)
    {
        if (target == null)
            return false;

        Type type = target.GetType();

        foreach (string name in names)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null && field.FieldType == typeof(float))
            {
                field.SetValue(target, value);
                return true;
            }

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(float))
            {
                property.SetValue(target, value);
                return true;
            }
        }

        return false;
    }

    private bool TryInvokeFloatMethod(object target, float value, out float returnedValue, params string[] methodNames)
    {
        returnedValue = -1f;

        if (target == null)
            return false;

        Type type = target.GetType();
        MethodInfo[] methods = type.GetMethods(flags);

        foreach (string methodName in methodNames)
        {
            foreach (MethodInfo method in methods)
            {
                if (method == null)
                    continue;

                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length != 1)
                    continue;

                if (parameters[0].ParameterType != typeof(float))
                    continue;

                object result = method.Invoke(target, new object[] { value });

                if (method.ReturnType == typeof(void))
                {
                    returnedValue = -1f;
                    return true;
                }

                if (ConvertToFloat(result, out float converted))
                {
                    returnedValue = converted;
                    return true;
                }

                returnedValue = -1f;
                return true;
            }
        }

        return false;
    }

    private bool TryInvokeNoArgMethod(object target, params string[] methodNames)
    {
        if (target == null)
            return false;

        Type type = target.GetType();
        MethodInfo[] methods = type.GetMethods(flags);

        foreach (string methodName in methodNames)
        {
            foreach (MethodInfo method in methods)
            {
                if (method == null)
                    continue;

                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    continue;

                if (method.GetParameters().Length != 0)
                    continue;

                method.Invoke(target, null);
                return true;
            }
        }

        return false;
    }

    private bool ConvertToFloat(object raw, out float value)
    {
        value = 0f;

        if (raw is float f)
        {
            value = f;
            return true;
        }

        if (raw is int i)
        {
            value = i;
            return true;
        }

        if (raw is double d)
        {
            value = (float)d;
            return true;
        }

        return false;
    }

    private void PlayFxAt(ParticleSystem fx, Transform target)
    {
        if (fx == null)
            return;

        if (target != null)
        {
            fx.transform.position = target.position;
            fx.transform.rotation = target.rotation;
        }

        fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        fx.Play(true);
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[SpoonPowderPlateTransfer] {message}", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        transferStepMg = Mathf.Max(1f, transferStepMg);
        sourceDetectRadius = Mathf.Max(0.01f, sourceDetectRadius);
        mortarDetectRadius = Mathf.Max(0.01f, mortarDetectRadius);
        cooldownAfterTransfer = Mathf.Max(0f, cooldownAfterTransfer);
        tiltInDuration = Mathf.Max(0.01f, tiltInDuration);
        holdTiltDuration = Mathf.Max(0f, holdTiltDuration);
        tiltOutDuration = Mathf.Max(0.01f, tiltOutDuration);
    }
#endif
}
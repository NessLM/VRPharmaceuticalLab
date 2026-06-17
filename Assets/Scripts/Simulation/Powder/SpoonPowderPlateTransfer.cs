using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public class SpoonPowderPlateTransfer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HornSpoon hornSpoon;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private Transform spoonTip;

    [Header("Powder Source - Piring Kiri")]
    [SerializeField] private PowderDepositZone powderDepositZone;
    [SerializeField] private Transform powderSourcePoint;

    [Header("Mortar Receiver")]
    [SerializeField] private Component mortarReceiver;
    [SerializeField] private Transform mortarTargetPoint;

    [Header("Transfer Settings")]
    [SerializeField] private float transferStepMg = 50f;
    [SerializeField] private float sourceDetectRadius = 0.18f;
    [SerializeField] private float mortarDetectRadius = 0.18f;
    [SerializeField] private bool requireHeld = true;
    [SerializeField] private bool onlyTakeWhenSpoonEmpty = true;

    [Header("Optional FX")]
    [SerializeField] private ParticleSystem scoopFx;
    [SerializeField] private ParticleSystem pourFx;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private void Awake()
    {
        if (hornSpoon == null)
            hornSpoon = GetComponent<HornSpoon>();

        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (spoonTip == null && hornSpoon != null)
            spoonTip = hornSpoon.TipTransform;
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
    }

    private void OnActivated(ActivateEventArgs args)
    {
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

        bool nearMortar = IsNear(mortarTargetPoint, mortarDetectRadius);
        bool nearSource = IsNear(powderSourcePoint, sourceDetectRadius);

        float spoonMg = GetSpoonAmountMg();

        if (nearMortar && spoonMg > 0.1f)
        {
            TryPourToMortar();
            return;
        }

        if (nearSource)
        {
            TryTakeFromPlate();
            return;
        }

        Log($"No valid target. Distance source = {GetDistance(powderSourcePoint):0.000}, distance mortar = {GetDistance(mortarTargetPoint):0.000}");
    }

    private void TryTakeFromPlate()
    {
        if (powderDepositZone == null)
        {
            Log("Rejected: PowderDepositZone belum diisi.");
            return;
        }

        if (onlyTakeWhenSpoonEmpty && !hornSpoon.IsEmpty)
        {
            Log("Rejected: sendok masih ada bubuk. Tuang dulu ke mortar.");
            return;
        }

        float availableMg = powderDepositZone.DepositedMg;

        if (availableMg <= 0.1f)
        {
            Log("Rejected: bubuk di piring kiri sudah habis.");
            return;
        }

        float takeRequestMg = Mathf.Min(transferStepMg, availableMg);
        float addedToSpoonMg = AddPowderToSpoon(takeRequestMg);

        if (addedToSpoonMg <= 0.1f)
        {
            Log("Rejected: gagal memasukkan bubuk ke sendok.");
            return;
        }

        float remainingPlateMg = Mathf.Max(0f, availableMg - addedToSpoonMg);
        powderDepositZone.SetDepositMg(remainingPlateMg);

        PlayFx(scoopFx);

        Log($"TAKE OK: sendok ambil {addedToSpoonMg:0.###} mg dari piring kiri. Sisa piring = {remainingPlateMg:0.###} mg.");
    }

    private void TryPourToMortar()
    {
        Component receiver = ResolveMortarReceiver();

        if (receiver == null)
        {
            Log("Rejected: MortarReceiver belum diisi atau tidak ada MortarController.");
            return;
        }

        float spoonMg = GetSpoonAmountMg();

        if (spoonMg <= 0.1f)
        {
            Log("Rejected: sendok kosong.");
            return;
        }

        float removedFromSpoonMg = RemovePowderFromSpoon(spoonMg);

        if (removedFromSpoonMg <= 0.1f)
        {
            Log("Rejected: gagal mengurangi bubuk dari sendok.");
            return;
        }

        float acceptedByMortarMg = AddPowderToMortar(receiver, removedFromSpoonMg);

        if (acceptedByMortarMg <= 0.1f)
        {
            AddPowderToSpoon(removedFromSpoonMg);
            Log("Rejected: mortar tidak menerima bubuk. Bubuk dikembalikan ke sendok.");
            return;
        }

        float leftoverMg = removedFromSpoonMg - acceptedByMortarMg;

        if (leftoverMg > 0.1f)
            AddPowderToSpoon(leftoverMg);

        PlayFx(pourFx);

        Log($"POUR OK: mortar menerima {acceptedByMortarMg:0.###} mg. Sisa balik ke sendok = {leftoverMg:0.###} mg.");
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

    private void PlayFx(ParticleSystem fx)
    {
        if (fx == null)
            return;

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
    }
#endif
}
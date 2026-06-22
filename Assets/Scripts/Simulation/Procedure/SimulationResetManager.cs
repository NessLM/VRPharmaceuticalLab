using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SimulationResetManager : MonoBehaviour
{
    [Header("Procedure Managers")]
    [SerializeField] private SyrupProcedureManager syrupProcedureManager;
    [SerializeField] private SalepProcedureManager salepProcedureManager;
    [SerializeField] private SimulationStateResetter stateResetter;

    [Header("Player")]
    [SerializeField] private Transform xrOrigin;
    [SerializeField] private Transform resetSpawnPoint;

    [Header("Main Menu")]
    [SerializeField] private PadatMenuPanelController mainMenuController;
    [SerializeField] private GameObject panelPilihJenisSediaan;
    [SerializeField] private GameObject[] panelsToHide;
    [SerializeField] private GameObject[] procedureSystemsToDisable;

    private Pose initialPlayerPose;
    private bool playerPoseCaptured;
    private bool isResetting;

    private void Awake()
    {
        CaptureInitialPlayerPose();

        if (stateResetter == null)
            stateResetter = GetComponent<SimulationStateResetter>();
    }

    public void ResetAllAndReturnToMainMenu()
    {
        if (isResetting)
            return;

        isResetting = true;

        try
        {
            ExecuteResetPhase(
                () => syrupProcedureManager?.ResetProcedureStateFromGlobal(),
                "Syrup procedure");
            ExecuteResetPhase(
                () => salepProcedureManager?.ResetProcedureStateFromGlobal(),
                "Salep procedure");
            ExecuteResetPhase(ResetCustomSimulationState, "Custom simulation objects");
            ExecuteResetPhase(() => stateResetter?.ResetCapturedState(), "Captured transforms");
            ExecuteResetPhase(ResetBottleLidsWithoutEvents, "Bottle lids");
        }
        finally
        {
            ExecuteResetPhase(RestorePlayerPose, "Player respawn");
            ExecuteResetPhase(RestoreMainMenu, "Main menu");
            isResetting = false;
        }

        Debug.Log("[SimulationReset] Semua state simulasi direset dan player kembali ke spawn awal.", this);
    }

    public void ResetCustomSimulationState()
    {
        foreach (PerkamenSnapTarget target in
                 FindObjectsByType<PerkamenSnapTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            target.ClearSnapState(false);
        }

        foreach (StackPerkamenDispenser dispenser in
                 FindObjectsByType<StackPerkamenDispenser>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            dispenser.ResetDispenser();
        }

        foreach (LiquidSnapStation snapStation in
                 FindObjectsByType<LiquidSnapStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            snapStation.ClearSnappedContainer();
        }

        foreach (LiquidContainer liquid in
                 FindObjectsByType<LiquidContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            liquid.ResetLiquidState();
        }

        foreach (PowderPayload payload in
                 FindObjectsByType<PowderPayload>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            payload.Clear();
        }

        foreach (PowderContainer container in
                 FindObjectsByType<PowderContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            container.ResetAmount();
        }

        foreach (PowderVisualLevelSwitcher visual in
                 FindObjectsByType<PowderVisualLevelSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            visual.Clear();
        }

        foreach (HornSpoon spoon in
                 FindObjectsByType<HornSpoon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            spoon.ClearPowder();
        }

        foreach (RedPipetteController pipette in
                 FindObjectsByType<RedPipetteController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            pipette.ResetContents();
        }

        foreach (MortarController mortar in
                 FindObjectsByType<MortarController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            mortar.ResetMortar();
        }

        foreach (StamperResidueController residue in
                 FindObjectsByType<StamperResidueController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            residue.ClearResidue();
        }

        foreach (BalanceWeightResetter resetter in
                 FindObjectsByType<BalanceWeightResetter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            resetter.ResetAllWeights();
        }

        foreach (MG_BalanceController balance in
                 FindObjectsByType<MG_BalanceController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            balance.ResetVisualToBasePose();
        }

        foreach (WeightItem weight in
                 FindObjectsByType<WeightItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            weight.ResetInteractionState();
        }

        foreach (WasherWaterController washer in
                 FindObjectsByType<WasherWaterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            washer.TurnOffWater();
        }
    }

    private void ResetBottleLidsWithoutEvents()
    {
        foreach (BottleLid lid in
                 FindObjectsByType<BottleLid>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lid == null)
                continue;

            try
            {
                lid.ResetToClosed();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[SimulationReset] Tutup {lid.name} gagal direset, reset global tetap dilanjutkan. {exception.Message}",
                    lid);
            }
        }
    }

    private void RestoreMainMenu()
    {
        if (panelsToHide != null)
        {
            foreach (GameObject panel in panelsToHide)
            {
                if (panel != null)
                    panel.SetActive(false);
            }
        }

        if (procedureSystemsToDisable != null)
        {
            foreach (GameObject procedureSystem in procedureSystemsToDisable)
            {
                if (procedureSystem != null)
                    procedureSystem.SetActive(false);
            }
        }

        if (mainMenuController != null)
        {
            mainMenuController.gameObject.SetActive(true);
            mainMenuController.ShowPanelPilihJenisSediaan();
        }

        if (panelPilihJenisSediaan != null)
            panelPilihJenisSediaan.SetActive(true);
    }

    private void CaptureInitialPlayerPose()
    {
        if (playerPoseCaptured || xrOrigin == null)
            return;

        initialPlayerPose = new Pose(xrOrigin.position, xrOrigin.rotation);
        playerPoseCaptured = true;
    }

    private void RestorePlayerPose()
    {
        if (xrOrigin != null && resetSpawnPoint != null)
        {
            xrOrigin.SetPositionAndRotation(
                resetSpawnPoint.position,
                resetSpawnPoint.rotation);
            return;
        }

        CaptureInitialPlayerPose();
        if (xrOrigin != null && playerPoseCaptured)
            xrOrigin.SetPositionAndRotation(initialPlayerPose.position, initialPlayerPose.rotation);

        foreach (MenuTeleportPlayer teleporter in
                 FindObjectsByType<MenuTeleportPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            teleporter.ResetPlayerToInitialPose();
        }
    }

    private void ExecuteResetPhase(Action resetAction, string phaseName)
    {
        if (resetAction == null)
            return;

        try
        {
            resetAction.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[SimulationReset] Fase '{phaseName}' gagal, fase reset berikutnya tetap dijalankan.\n{exception}",
                this);
        }
    }
}

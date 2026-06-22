using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Materi
{
    [System.Serializable]
    public class MateriEntry
    {
        public string title = "Judul Topik";
        [TextArea(4, 12)] public string description = "Isi materi...";
    }

    [DisallowMultipleComponent]
    public class MateriPanelController : MonoBehaviour
    {
        [Header("Konten Materi (EDITABLE di Inspector)")]
        [SerializeField] private MateriEntry[] entries;

        [Header("UI References (Wajib Di-bind di Inspector)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button backButton;
        [SerializeField] private List<Button> stepButtons = new List<Button>();

        [Header("Teleport & VR Setup")]
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Transform teleportTarget;
        [SerializeField] private float autoTargetDistance = 1.6f;
        [SerializeField] private Transform boardAnchor;

        [Header("Menu & Kembali")]
        [SerializeField] private GameObject menuToToggle;
        [SerializeField] private Transform returnTarget;

        [Header("Mode Tampil")]
        [SerializeField] private bool placeInFrontOfCamera = false;
        [SerializeField] private float cameraDistance = 1.3f;
        [SerializeField] private float cameraHeightOffset = -0.1f;

        // Properties for access
        public MateriEntry[] Entries { get => entries; set => entries = value; }
        public GameObject PanelRoot { get => panelRoot; set => panelRoot = value; }
        public TMP_Text TitleText { get => titleText; set => titleText = value; }
        public TMP_Text BodyText { get => bodyText; set => bodyText = value; }
        public Button BackButton { get => backButton; set => backButton = value; }
        public List<Button> StepButtons { get => stepButtons; set => stepButtons = value; }
        public Transform XrOrigin { get => xrOrigin; set => xrOrigin = value; }
        public Transform TeleportTarget { get => teleportTarget; set => teleportTarget = value; }
        public Transform BoardAnchor { get => boardAnchor; set => boardAnchor = value; }
        public GameObject MenuToToggle { get => menuToToggle; set => menuToToggle = value; }
        public Transform ReturnTarget { get => returnTarget; set => returnTarget = value; }
        public bool PlaceInFrontOfCamera { get => placeInFrontOfCamera; set => placeInFrontOfCamera = value; }

        private bool isOpen;
        private Pose initialPose;
        private bool initialPoseCaptured;
        private int currentIndex;

        protected virtual void Awake()
        {
            ResolveReferences();
            SetupButtonListeners();
        }

        public void SetupButtonListeners()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(CloseMateri);
                backButton.onClick.AddListener(CloseMateri);
            }

            for (int i = 0; i < stepButtons.Count; i++)
            {
                if (stepButtons[i] == null) continue;
                int index = i;
                stepButtons[i].onClick.RemoveAllListeners();
                stepButtons[i].onClick.AddListener(() => ShowEntry(index));
            }
        }

        private void ResolveReferences()
        {
            if (xrOrigin == null)
            {
                GameObject candidate =
                    GameObject.Find("XR Origin (XR Rig)") ??
                    GameObject.Find("XR Origin") ??
                    GameObject.Find("XROrigin") ??
                    GameObject.Find("XR Origin (VR)");
                if (candidate != null)
                    xrOrigin = candidate.transform;
            }

            if (boardAnchor == null)
                boardAnchor = transform;
        }

        public void OpenMateri()
        {
            ResolveReferences();

            if (menuToToggle != null)
                menuToToggle.SetActive(false);

            if (placeInFrontOfCamera)
            {
                PlaceBoardInFrontOfCamera();
            }
            else
            {
                CaptureInitialPose();
                TeleportTo(GetOrCreateTeleportTarget());
            }

            if (panelRoot != null)
                panelRoot.SetActive(true);

            isOpen = true;
            ShowEntry(0);
        }

        private void PlaceBoardInFrontOfCamera()
        {
            Camera cam = Camera.main;
            if (cam == null || boardAnchor == null)
                return;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            boardAnchor.position =
                cam.transform.position + forward * cameraDistance + Vector3.up * cameraHeightOffset;
            boardAnchor.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        public void CloseMateri()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (returnTarget != null)
                TeleportTo(returnTarget);
            else
                RestoreInitialPose();

            if (menuToToggle != null)
                menuToToggle.SetActive(true);

            isOpen = false;
        }

        private Transform GetOrCreateTeleportTarget()
        {
            if (teleportTarget != null)
                return teleportTarget;

            if (boardAnchor == null)
                return null;

            GameObject target = new GameObject("MateriTeleportTarget (auto)");
            target.transform.SetParent(boardAnchor, false);
            target.transform.position =
                boardAnchor.position + boardAnchor.forward * autoTargetDistance;
            target.transform.rotation =
                Quaternion.LookRotation(-boardAnchor.forward, Vector3.up);
            teleportTarget = target.transform;
            return teleportTarget;
        }

        private void CaptureInitialPose()
        {
            if (xrOrigin == null || initialPoseCaptured)
                return;
            initialPose = new Pose(xrOrigin.position, xrOrigin.rotation);
            initialPoseCaptured = true;
        }

        private void TeleportTo(Transform target)
        {
            if (xrOrigin == null || target == null)
                return;
            xrOrigin.SetPositionAndRotation(target.position, target.rotation);
        }

        private void RestoreInitialPose()
        {
            if (xrOrigin == null || !initialPoseCaptured)
                return;
            xrOrigin.SetPositionAndRotation(initialPose.position, initialPose.rotation);
        }

        public void ShowEntry(int index)
        {
            if (entries == null || entries.Length == 0)
                return;

            currentIndex = Mathf.Clamp(index, 0, entries.Length - 1);
            if (titleText != null)
                titleText.text = entries[currentIndex].title;
            if (bodyText != null)
                bodyText.text = entries[currentIndex].description;

            // Optional: highlight selected button
            for (int i = 0; i < stepButtons.Count; i++)
            {
                if (stepButtons[i] == null) continue;
                Image img = stepButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = i == currentIndex ? new Color(0.12f, 0.38f, 0.78f, 1f) : new Color(0.20f, 0.55f, 0.95f, 1f);
                }
            }
        }
    }
}

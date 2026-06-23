using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Quiz
{
    [DisallowMultipleComponent]
    public class QuizIntroPanelController : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Text Elements")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text instructionText;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField inputNama;
        [SerializeField] private TMP_InputField inputKelas;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        public GameObject PanelRoot => panelRoot;
        public TMP_Text TitleText => titleText;
        public TMP_Text InstructionText => instructionText;
        public TMP_InputField InputNama => inputNama;
        public TMP_InputField InputKelas => inputKelas;
        public Button StartButton => startButton;
        public Button BackButton => backButton;

        private void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
        }
    }
}

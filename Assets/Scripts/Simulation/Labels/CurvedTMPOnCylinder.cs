using TMPro;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CurvedTMPOnCylinder : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private TMP_Text tmpText;

    [Header("Cylinder Curve")]
    [Tooltip("Semakin kecil radius, semakin melengkung. Sesuaikan dengan radius botol.")]
    [SerializeField] private float radius = 0.55f;

    [Tooltip("Kekuatan lengkungan. 1 = normal.")]
    [SerializeField] private float curveStrength = 1f;

    [Tooltip("Aktifkan kalau lengkungannya masuk ke arah yang salah.")]
    [SerializeField] private bool invertCurve = false;

    private void Reset()
    {
        tmpText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        tmpText.OnPreRenderText += BendText;
        tmpText.ForceMeshUpdate();
    }

    private void OnDisable()
    {
        if (tmpText != null)
            tmpText.OnPreRenderText -= BendText;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();

        if (tmpText != null)
            tmpText.ForceMeshUpdate();
    }
#endif

    private void BendText(TMP_TextInfo textInfo)
    {
        if (radius <= 0.001f)
            return;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible)
                continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            for (int j = 0; j < 4; j++)
            {
                Vector3 v = vertices[vertexIndex + j];

                float theta = (v.x / radius) * curveStrength;

                float curvedX = Mathf.Sin(theta) * radius;
                float curvedZ = Mathf.Cos(theta) * radius - radius;

                if (invertCurve)
                    curvedZ = -curvedZ;

                v.x = curvedX;
                v.z += curvedZ;

                vertices[vertexIndex + j] = v;
            }
        }
    }
}
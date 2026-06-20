using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class ScoopBottleTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PowderContainer powderContainer;
    [SerializeField] private BottleLid requiredOpenLid;
    [SerializeField] private Collider scoopAccessZone;
    [SerializeField] private Transform entryAnchor;
    [SerializeField] private Transform insideAnchor;
    [SerializeField] private Transform exitAnchor;
    [SerializeField] private Transform ghostSpoonVisual;
    [SerializeField] private ParticleSystem scoopDustFX;
    [SerializeField] private GameObject scoopPrompt;
    [SerializeField] private bool debugLogs;

    [Header("Timing")]
    [SerializeField] private float approachDuration = 0.12f;
    [SerializeField] private float dipDuration = 0.16f;
    [SerializeField] private float dwellDuration = 0.06f;
    [SerializeField] private float exitDuration = 0.16f;
    [SerializeField] private float cooldown = 0.2f;

    [Header("Amount")]
    [SerializeField] private float scoopAmountMg = 120f;

    [Header("Tip Detection Fallback")]
    [SerializeField] private float tipEligibilityRadius = 0.38f;
    [SerializeField] private float tipInsideRadius = 0.24f;
    [SerializeField] private bool autoScoopWhenTipInside = true;
    [SerializeField] private bool requireSelectedForAutoScoop;
    [SerializeField] private float autoScoopDelay = 0.06f;
    [SerializeField] private float proximityRefreshInterval = 0.05f;

    private readonly Dictionary<HornSpoon, int> eligibleSpoons = new Dictionary<HornSpoon, int>();
    private readonly HashSet<HornSpoon> proximitySpoons = new HashSet<HornSpoon>();
    private readonly Dictionary<HornSpoon, float> autoScoopTimers = new Dictionary<HornSpoon, float>();
    private readonly List<HornSpoon> removalBuffer = new List<HornSpoon>();
    private bool isScooping;
    private float nextReadyTime;
    private float nextProximityRefreshTime;
    private Material ghostRuntimeMaterial;
    private Material dustRuntimeMaterial;

    private void Awake()
    {
        ResolveReferences();
        ConfigureDust();
        EnsureGhostVisual();
        EnsurePromptVisual();
        SetPrompt(false);
        SetGhost(false);
    }

    private void Update()
    {
        RefreshNearbySpoonsByTip();
        ProcessAutoScoop();

        bool canAnySpoonScoop = false;
        foreach (HornSpoon spoon in eligibleSpoons.Keys)
        {
            if (CanScoop(spoon))
            {
                canAnySpoonScoop = true;
                break;
            }
        }

        if (!canAnySpoonScoop)
        {
            foreach (HornSpoon spoon in proximitySpoons)
            {
                if (CanScoop(spoon))
                {
                    canAnySpoonScoop = true;
                    break;
                }
            }
        }

        SetPrompt(canAnySpoonScoop);
        FacePromptToCamera();
    }

    private void OnTriggerEnter(Collider other)
    {
        HornSpoon spoon = ResolveSpoon(other);
        if (spoon == null)
            return;

        if (!eligibleSpoons.ContainsKey(spoon))
        {
            eligibleSpoons.Add(spoon, 0);
            GetActivator(spoon)?.SetCurrentTarget(this);
            Log($"Spoon entered zone: {spoon.name}");
        }

        eligibleSpoons[spoon]++;
    }

    private void OnTriggerStay(Collider other)
    {
        HornSpoon spoon = ResolveSpoon(other);
        if (spoon == null || eligibleSpoons.ContainsKey(spoon))
            return;

        eligibleSpoons.Add(spoon, 1);
        GetActivator(spoon)?.SetCurrentTarget(this);
    }

    private void OnTriggerExit(Collider other)
    {
        HornSpoon spoon = ResolveSpoon(other);
        if (spoon == null || !eligibleSpoons.ContainsKey(spoon))
            return;

        eligibleSpoons[spoon]--;
        if (eligibleSpoons[spoon] > 0)
            return;

        eligibleSpoons.Remove(spoon);
        GetActivator(spoon)?.SetCurrentTarget(null);
        Log($"Spoon exited zone: {spoon.name}");
    }

    public bool CanScoop(HornSpoon spoon)
    {
        if (isScooping || Time.time < nextReadyTime)
            return false;

        if (spoon == null || !IsEligible(spoon))
            return false;

        if (!IsLidOpenEnough())
            return false;

        if (powderContainer == null || !powderContainer.IsAccessible || powderContainer.IsEmpty)
            return false;

        return spoon.IsEmpty;
    }

    public bool TryScoop(HornSpoon spoon)
    {
        RefreshNearbySpoonsByTip(force: true);

        if (!CanScoop(spoon))
        {
            Log("Scoop rejected");
            return false;
        }

        StartCoroutine(ScoopRoutine(spoon));
        return true;
    }

    private IEnumerator ScoopRoutine(HornSpoon spoon)
    {
        isScooping = true;
        SetPrompt(false);

        // Match ghost appearance to the real spoon before showing it
        UpdateGhostToMatchSpoon(spoon);

        // Capture the real spoon's world pose at the moment scooping begins
        Vector3 spoonStartPos = spoon.transform.position;
        Quaternion spoonStartRot = spoon.transform.rotation;

        // Hide the real spoon so only the ghost is visible during animation
        Renderer[] spoonRenderers = spoon.GetComponentsInChildren<Renderer>();
        SetRenderersEnabled(spoonRenderers, false);

        // Show ghost positioned exactly where the real spoon is
        SetGhost(true);
        if (ghostSpoonVisual != null)
            ghostSpoonVisual.SetPositionAndRotation(spoonStartPos, spoonStartRot);

        // Phase 1: animate ghost from spoon's position → inside anchor (dip into bottle)
        Vector3 insidePos = insideAnchor != null ? insideAnchor.position : transform.position;
        Quaternion insideRot = insideAnchor != null ? insideAnchor.rotation : transform.rotation;
        yield return MoveGhostBetweenPositions(spoonStartPos, spoonStartRot, insidePos, insideRot, approachDuration + dipDuration);

        // Dwell, fill spoon, emit dust particle burst
        yield return new WaitForSeconds(dwellDuration);
        FillSpoon(spoon);
        EmitDust();

        // Phase 2: animate ghost from inside anchor → back to spoon's current position
        Vector3 returnPos = spoon.transform.position;
        Quaternion returnRot = spoon.transform.rotation;
        yield return MoveGhostBetweenPositions(insidePos, insideRot, returnPos, returnRot, exitDuration);

        // Finish: hide ghost, restore real spoon visuals (now showing powder)
        SetGhost(false);
        SetRenderersEnabled(spoonRenderers, true);

        isScooping = false;
        nextReadyTime = Time.time + cooldown;
    }

    /// <summary>Copies the real spoon's mesh, materials, and world scale onto the ghost visual.</summary>
    private void UpdateGhostToMatchSpoon(HornSpoon spoon)
    {
        if (ghostSpoonVisual == null || spoon == null)
            return;

        MeshFilter ghostFilter = ghostSpoonVisual.GetComponent<MeshFilter>();
        MeshRenderer ghostRenderer = ghostSpoonVisual.GetComponent<MeshRenderer>();

        MeshFilter spoonFilter = spoon.GetComponent<MeshFilter>();
        if (ghostFilter != null && spoonFilter != null && spoonFilter.sharedMesh != null)
            ghostFilter.sharedMesh = spoonFilter.sharedMesh;

        MeshRenderer spoonRenderer = spoon.GetComponent<MeshRenderer>();
        if (ghostRenderer != null && spoonRenderer != null && spoonRenderer.sharedMaterials.Length > 0)
            ghostRenderer.sharedMaterials = spoonRenderer.sharedMaterials;

        // Scale ghost in its local space to match the real spoon's world scale
        if (ghostSpoonVisual.parent != null)
        {
            Vector3 parentLossy = ghostSpoonVisual.parent.lossyScale;
            Vector3 spoonLossy = spoon.transform.lossyScale;
            ghostSpoonVisual.localScale = new Vector3(
                parentLossy.x > 0.0001f ? spoonLossy.x / parentLossy.x : 1f,
                parentLossy.y > 0.0001f ? spoonLossy.y / parentLossy.y : 1f,
                parentLossy.z > 0.0001f ? spoonLossy.z / parentLossy.z : 1f);
        }
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        foreach (Renderer r in renderers)
            if (r != null)
                r.enabled = enabled;
    }

    private IEnumerator MoveGhostBetweenPositions(Vector3 startPos, Quaternion startRot, Vector3 endPos, Quaternion endRot, float duration)
    {
        if (ghostSpoonVisual == null)
            yield break;

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ghostSpoonVisual.SetPositionAndRotation(
                Vector3.Lerp(startPos, endPos, t),
                Quaternion.Slerp(startRot, endRot, t));
            yield return null;
        }

        ghostSpoonVisual.SetPositionAndRotation(endPos, endRot);
    }

    private void FillSpoon(HornSpoon spoon)
    {
        if (spoon == null || powderContainer == null)
            return;

        float taken = powderContainer.TakePowder(scoopAmountMg);
        float accepted = spoon.AddPowder(taken);
        if (accepted < taken)
            powderContainer.AddPowder(taken - accepted);
    }

    private void RefreshNearbySpoonsByTip(bool force = false)
    {
        if (!force && Time.time < nextProximityRefreshTime)
            return;

        nextProximityRefreshTime = Time.time + proximityRefreshInterval;

        removalBuffer.Clear();
        foreach (HornSpoon spoon in proximitySpoons)
            removalBuffer.Add(spoon);

        HornSpoon[] spoons = FindObjectsByType<HornSpoon>(FindObjectsSortMode.None);
        foreach (HornSpoon spoon in spoons)
        {
            if (spoon == null || !IsSpoonTipNear(spoon, tipEligibilityRadius))
                continue;

            removalBuffer.Remove(spoon);
            if (proximitySpoons.Add(spoon))
            {
                GetActivator(spoon)?.SetCurrentTarget(this);
                Log($"Spoon tip near mouth: {spoon.name}");
            }
        }

        foreach (HornSpoon spoon in removalBuffer)
        {
            proximitySpoons.Remove(spoon);
            autoScoopTimers.Remove(spoon);
            if (spoon != null && !eligibleSpoons.ContainsKey(spoon))
                GetActivator(spoon)?.SetCurrentTarget(null);
        }
    }

    private void ProcessAutoScoop()
    {
        if (!autoScoopWhenTipInside || isScooping)
            return;

        HornSpoon spoonToScoop = null;
        foreach (HornSpoon spoon in proximitySpoons)
        {
            if (spoon == null)
                continue;

            if (!CanScoop(spoon) || !IsSpoonTipNear(spoon, tipInsideRadius) || !PassesAutoSelectRequirement(spoon))
            {
                autoScoopTimers[spoon] = 0f;
                continue;
            }

            autoScoopTimers.TryGetValue(spoon, out float timer);
            timer += Time.deltaTime;
            autoScoopTimers[spoon] = timer;

            if (timer >= autoScoopDelay)
            {
                autoScoopTimers[spoon] = 0f;
                spoonToScoop = spoon;
                break;
            }
        }

        if (spoonToScoop != null)
            TryScoop(spoonToScoop);
    }

    private bool IsEligible(HornSpoon spoon)
    {
        return eligibleSpoons.ContainsKey(spoon) || proximitySpoons.Contains(spoon);
    }

    private bool IsSpoonTipNear(HornSpoon spoon, float radius)
    {
        if (spoon == null)
            return false;

        Transform target = insideAnchor != null ? insideAnchor : transform;
        Transform tip = spoon.TipTransform;
        if (target == null || tip == null)
            return false;

        float sqrRadius = radius * radius;
        if ((tip.position - target.position).sqrMagnitude <= sqrRadius)
            return true;

        Collider[] colliders = spoon.GetComponentsInChildren<Collider>();
        foreach (Collider spoonCollider in colliders)
        {
            if (spoonCollider == null || !spoonCollider.enabled)
                continue;

            Vector3 closestPoint = spoonCollider.ClosestPoint(target.position);
            if ((closestPoint - target.position).sqrMagnitude <= sqrRadius)
                return true;
        }

        return false;
    }

    private bool PassesAutoSelectRequirement(HornSpoon spoon)
    {
        if (!requireSelectedForAutoScoop)
            return true;

        XRGrabInteractable grab = spoon != null ? spoon.GetComponent<XRGrabInteractable>() : null;
        return grab != null && grab.isSelected;
    }

    private bool IsLidOpenEnough()
    {
        return requiredOpenLid == null || requiredOpenLid.IsOpen || requiredOpenLid.transform.parent == null;
    }

    private HornSpoon ResolveSpoon(Collider other)
    {
        if (other == null)
            return null;

        HornSpoon spoon = other.GetComponentInParent<HornSpoon>();
        if (spoon != null)
            return spoon;

        Transform parent = other.transform.parent;
        while (parent != null)
        {
            spoon = parent.GetComponent<HornSpoon>();
            if (spoon != null)
                return spoon;
            parent = parent.parent;
        }

        return null;
    }

    private SpoonScoopActivator GetActivator(HornSpoon spoon)
    {
        return spoon != null ? spoon.GetComponent<SpoonScoopActivator>() : null;
    }

    private void ResolveReferences()
    {
        if (scoopAccessZone == null)
            scoopAccessZone = GetComponent<Collider>();

        if (scoopAccessZone != null)
            scoopAccessZone.isTrigger = true;

        if (powderContainer == null)
            powderContainer = GetComponentInParent<PowderContainer>();

        if (requiredOpenLid == null)
            requiredOpenLid = GetComponentInParent<BottleLid>();

        if (scoopDustFX == null)
        {
            Transform dustParent = insideAnchor != null ? insideAnchor : transform;
            GameObject dustObject = new GameObject("Runtime_ScoopDustFX");
            dustObject.transform.SetParent(dustParent, false);
            dustObject.transform.localPosition = Vector3.zero;
            dustObject.transform.localRotation = Quaternion.identity;
            dustObject.transform.localScale = Vector3.one;
            scoopDustFX = dustObject.AddComponent<ParticleSystem>();
        }
    }

    private void ConfigureDust()
    {
        if (scoopDustFX == null)
            return;

        ParticleSystem.MainModule main = scoopDustFX.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.006f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.94f, 0.92f, 0.82f, 0.42f));
        main.maxParticles = 36;
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = scoopDustFX.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = scoopDustFX.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.018f;

        ParticleSystemRenderer renderer = scoopDustFX.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            renderer = scoopDustFX.gameObject.AddComponent<ParticleSystemRenderer>();

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0.0005f;
        renderer.maxParticleSize = 0.014f;
        renderer.sharedMaterial = GetRuntimeMaterial("Runtime_Difenhidramin_ScoopDustFX", 0.42f);
    }

    private void EmitDust()
    {
        if (scoopDustFX == null)
            return;

        if (insideAnchor != null)
            scoopDustFX.transform.position = insideAnchor.position;

        scoopDustFX.Emit(7);
    }

    private void EnsureGhostVisual()
    {
        if (ghostSpoonVisual == null)
            return;

        MeshFilter meshFilter = ghostSpoonVisual.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = ghostSpoonVisual.gameObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = ghostSpoonVisual.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = ghostSpoonVisual.gameObject.AddComponent<MeshRenderer>();

        // Fallback mesh — will be replaced by the actual spoon mesh in UpdateGhostToMatchSpoon
        meshFilter.sharedMesh = BuildGhostSpoonMesh();
        meshRenderer.sharedMaterial = CreateGhostMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Creates a solid opaque horn-colored material for the ghost spoon.</summary>
    private Material CreateGhostMaterial()
    {
        if (ghostRuntimeMaterial != null)
            return ghostRuntimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        ghostRuntimeMaterial = new Material(shader) { name = "Runtime_Ghost_Spoon_Material" };
        Color hornColor = new Color(0.60f, 0.40f, 0.18f, 1f);

        if (ghostRuntimeMaterial.HasProperty("_BaseColor"))
            ghostRuntimeMaterial.SetColor("_BaseColor", hornColor);
        if (ghostRuntimeMaterial.HasProperty("_Color"))
            ghostRuntimeMaterial.SetColor("_Color", hornColor);
        if (ghostRuntimeMaterial.HasProperty("_Smoothness"))
            ghostRuntimeMaterial.SetFloat("_Smoothness", 0.3f);

        return ghostRuntimeMaterial;
    }

    private Mesh BuildGhostSpoonMesh()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        AddBox(vertices, normals, uvs, triangles, new Vector3(-0.08f, 0f, 0f), new Vector3(0.12f, 0.008f, 0.012f));
        AddOvalDish(vertices, normals, uvs, triangles, new Vector3(0.055f, 0f, 0f), 0.04f, 0.025f, 0.008f, 24);

        Mesh mesh = new Mesh
        {
            name = "Generated_GhostSpoonVisual"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static void AddBox(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 size)
    {
        Vector3 half = size * 0.5f;
        int start = vertices.Count;
        vertices.Add(center + new Vector3(-half.x, -half.y, -half.z));
        vertices.Add(center + new Vector3(-half.x, half.y, -half.z));
        vertices.Add(center + new Vector3(half.x, half.y, -half.z));
        vertices.Add(center + new Vector3(half.x, -half.y, -half.z));
        vertices.Add(center + new Vector3(-half.x, -half.y, half.z));
        vertices.Add(center + new Vector3(-half.x, half.y, half.z));
        vertices.Add(center + new Vector3(half.x, half.y, half.z));
        vertices.Add(center + new Vector3(half.x, -half.y, half.z));

        for (int i = 0; i < 8; i++)
        {
            normals.Add(Vector3.up);
            uvs.Add(Vector2.zero);
        }

        int[] indices =
        {
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            3, 2, 6, 3, 6, 7,
            1, 5, 6, 1, 6, 2,
            0, 3, 7, 0, 7, 4
        };

        for (int i = 0; i < indices.Length; i++)
            triangles.Add(start + indices[i]);
    }

    private static void AddOvalDish(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 center, float radiusX, float radiusZ, float height, int segments)
    {
        int capCenter = vertices.Count;
        vertices.Add(center + Vector3.up * height);
        normals.Add(Vector3.up);
        uvs.Add(new Vector2(0.5f, 0.5f));

        int ringStart = vertices.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertices.Add(center + new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles.Add(capCenter);
            triangles.Add(ringStart + (i + 1) % segments);
            triangles.Add(ringStart + i);
        }
    }

    private void EnsurePromptVisual()
    {
        if (scoopPrompt == null)
            return;

        TextMesh textMesh = scoopPrompt.GetComponent<TextMesh>();
        if (textMesh == null)
            textMesh = scoopPrompt.AddComponent<TextMesh>();

        textMesh.text = "Ambil bubuk";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.03f;
        textMesh.fontSize = 34;
        textMesh.color = Color.white;

        MeshRenderer renderer = scoopPrompt.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = scoopPrompt.AddComponent<MeshRenderer>();

        if (textMesh.font != null)
            renderer.sharedMaterial = textMesh.font.material;

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void SetPrompt(bool active)
    {
        if (scoopPrompt != null && scoopPrompt.activeSelf != active)
            scoopPrompt.SetActive(active);
    }

    private void FacePromptToCamera()
    {
        if (scoopPrompt == null || !scoopPrompt.activeSelf || Camera.main == null)
            return;

        Vector3 direction = scoopPrompt.transform.position - Camera.main.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            scoopPrompt.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void SetGhost(bool active)
    {
        if (ghostSpoonVisual != null && ghostSpoonVisual.gameObject.activeSelf != active)
            ghostSpoonVisual.gameObject.SetActive(active);
    }

    private Material GetRuntimeMaterial(string materialName, float alpha)
    {
        if (dustRuntimeMaterial != null)
            return dustRuntimeMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        dustRuntimeMaterial = new Material(shader)
        {
            name = materialName
        };

        Color color = new Color(0.94f, 0.92f, 0.82f, alpha);

        if (dustRuntimeMaterial.HasProperty("_BaseColor"))
            dustRuntimeMaterial.SetColor("_BaseColor", color);
        if (dustRuntimeMaterial.HasProperty("_Color"))
            dustRuntimeMaterial.SetColor("_Color", color);

        return dustRuntimeMaterial;
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[ScoopBottleTarget] {message}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (scoopAccessZone != null)
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(scoopAccessZone.bounds.center, scoopAccessZone.bounds.extents.magnitude);
        }

        DrawAnchorGizmo(entryAnchor, Color.yellow);
        DrawAnchorGizmo(insideAnchor, Color.green);
        DrawAnchorGizmo(exitAnchor, Color.cyan);
    }

    private static void DrawAnchorGizmo(Transform anchor, Color color)
    {
        if (anchor == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawSphere(anchor.position, 0.01f);
        Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward * 0.05f);
    }
#endif
}

using UnityEngine;
using System.Collections.Generic;

public class BoardNode : MonoBehaviour
{
    public string nodeName;
    public List<BoardNode> neighbors;
    private Renderer rend;

    public enum NodeType { Media, Business, Sport, IT, Art, Science, Volounteer, Tourism, Money, Travel, Project, Grant, None }

    [Header("Reward Settings")]
    public NodeType nodeStat = NodeType.None;

    [Header("Highlight Visual")]
    public bool useInnerFill = false;
    public bool autoCreateHighlightSphere = true;
    public Transform highlightVisual;
    public Material highlightMaterial;
    public Color highlightColor = new Color(0.3f, 1f, 0.45f, 0.15f);
    public float highlightEmission = 1.5f;
    public float highlightScaleMultiplier = 0.55f;
    public float minHighlightScale = 0.2f;
    public float maxHighlightScale = 1.6f;
    public float highlightYOffset = 0.7f;
    public float highlightThickness = 0.03f;

    [Header("Outline Visual")]
    public bool useOutline = true;
    public bool autoCreateOutlineSphere = true;
    public Transform outlineVisual;
    public Material outlineMaterial;
    public Color outlineColor = new Color(0.45f, 1f, 0.55f, 0.08f);
    public float outlineEmission = 2.5f;
    public float outlineScaleMultiplier = 1.2f;
    public float outlineYOffset = 0.005f;
    [Range(16, 128)] public int outlineRingSegments = 48;
    public float outlineRingWidth = 0.08f;
    public float outlineHoverEmissionMultiplier = 1.7f;

    [Header("Hover highlight")]
    public float hoverEmissionMultiplier = 1.8f;
    public float hoverScaleMultiplier = 1.12f;

    private Renderer highlightRenderer;
    private Material runtimeHighlightMaterial;
    private Renderer outlineRenderer;
    private Material runtimeOutlineMaterial;
    private LineRenderer outlineLineRenderer;
    private Vector3 baseHighlightScale = Vector3.one;
    private Vector3 baseOutlineScale = Vector3.one;
    private bool highlightActive;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        EnsureHighlightVisual();
        SetHighlight(false);
    }

    public void SetHighlight(bool side)
    {
        highlightActive = side;
        if (highlightVisual != null)
            highlightVisual.gameObject.SetActive(side);
        if (highlightRenderer != null)
            highlightRenderer.enabled = side && useInnerFill;
        if (outlineVisual != null)
            outlineVisual.gameObject.SetActive(side && useOutline);
        ApplyHighlightHoverState(false);
    }

    private void OnMouseEnter()
    {
        if (!highlightActive) return;
        ApplyHighlightHoverState(true);
    }

    private void OnMouseExit()
    {
        if (!highlightActive) return;
        ApplyHighlightHoverState(false);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (neighbors == null) return;
        foreach (var n in neighbors)
        {
            if (n != null) Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }

    private void EnsureHighlightVisual()
    {
        if (highlightVisual == null && autoCreateHighlightSphere)
        {
            GameObject circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circle.name = "MoveHighlightCircle";
            circle.transform.SetParent(transform, false);
            circle.transform.localPosition = Vector3.up * highlightYOffset;

            Collider circleCollider = circle.GetComponent<Collider>();
            if (circleCollider != null)
                Destroy(circleCollider);

            float baseScale = 1f;
            if (rend != null)
            {
                Vector3 size = rend.bounds.size;
                baseScale = Mathf.Max(size.x, size.z);
            }
            float clampedScale = Mathf.Clamp(baseScale * highlightScaleMultiplier, minHighlightScale, maxHighlightScale);
            float thickness = Mathf.Max(0.001f, highlightThickness);
            circle.transform.localScale = new Vector3(clampedScale, thickness, clampedScale);
            highlightVisual = circle.transform;
        }

        if (highlightVisual == null)
            return;

        highlightRenderer = highlightVisual.GetComponent<Renderer>();
        if (highlightRenderer == null)
            return;

        Material mat = highlightMaterial != null
            ? new Material(highlightMaterial)
            : CreateRuntimeTransparentMaterial();

        ConfigureTransparentGlowMaterial(mat, highlightColor, highlightEmission);
        runtimeHighlightMaterial = mat;
        highlightRenderer.material = mat;
        highlightRenderer.enabled = useInnerFill;
        baseHighlightScale = highlightVisual.localScale;

        EnsureOutlineVisual();
    }

    private void EnsureOutlineVisual()
    {
        if (!useOutline)
            return;

        if (outlineVisual == null && autoCreateOutlineSphere && highlightVisual != null)
        {
            GameObject ringObject = new GameObject("MoveHighlightOutlineRing");
            ringObject.transform.SetParent(highlightVisual.parent, false);
            ringObject.transform.localPosition = highlightVisual.localPosition + Vector3.up * outlineYOffset;
            // Лежит в плоскости клетки (XZ): LineRenderer с TransformZ рисует круг в локальной XY,
            // поворачиваем на 90° вокруг X -> кольцо горизонтально, без billboard к камере (View давал искажение).
            ringObject.transform.localRotation = highlightVisual.localRotation * Quaternion.Euler(90f, 0f, 0f);

            outlineLineRenderer = ringObject.AddComponent<LineRenderer>();
            outlineLineRenderer.useWorldSpace = false;
            outlineLineRenderer.loop = true;
            outlineLineRenderer.alignment = LineAlignment.TransformZ;
            outlineLineRenderer.textureMode = LineTextureMode.Stretch;
            outlineLineRenderer.numCornerVertices = 8;
            outlineLineRenderer.numCapVertices = 8;
            outlineLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineLineRenderer.receiveShadows = false;
            outlineLineRenderer.widthMultiplier = Mathf.Max(0.001f, outlineRingWidth);

            int segments = Mathf.Max(16, outlineRingSegments);
            outlineLineRenderer.positionCount = segments;
            float radius = 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                // Круг в локальной XY (см. TransformZ + поворот выше).
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                outlineLineRenderer.SetPosition(i, new Vector3(x, y, 0f));
            }

            float outlineScale = Mathf.Max(1f, outlineScaleMultiplier);
            float uniform = Mathf.Max(highlightVisual.localScale.x, highlightVisual.localScale.z) * outlineScale;
            ringObject.transform.localScale = new Vector3(uniform, uniform, uniform);

            outlineVisual = ringObject.transform;
        }

        if (outlineVisual == null)
            return;

        if (outlineLineRenderer == null)
            outlineLineRenderer = outlineVisual.GetComponent<LineRenderer>();

        outlineRenderer = outlineVisual.GetComponent<Renderer>();
        if (outlineRenderer == null)
            return;

        Material mat = outlineMaterial != null
            ? new Material(outlineMaterial)
            : CreateRuntimeTransparentMaterial();

        ConfigureTransparentGlowMaterial(mat, outlineColor, outlineEmission);
        runtimeOutlineMaterial = mat;
        if (outlineLineRenderer != null)
            outlineLineRenderer.material = mat;
        else
            outlineRenderer.material = mat;
        baseOutlineScale = outlineVisual.localScale;
    }

    private void ConfigureTransparentGlowMaterial(Material mat, Color color, float emission)
    {
        if (mat == null) return;

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (mat.HasProperty("_EmissionColor"))
        {
            Color emissionColor = new Color(color.r, color.g, color.b) * emission;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
        }

        // URP/HDRP style transparent setup (e.g. Lit shader).
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f); // Alpha blend
            if (mat.HasProperty("_AlphaClip"))
                mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_AlphaToMask"))
                mat.SetFloat("_AlphaToMask", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        if (mat.HasProperty("_Mode"))
        {
            // Standard shader transparent setup.
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private Material CreateRuntimeTransparentMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        if (shader == null)
            return new Material(Shader.Find("Standard"));

        return new Material(shader);
    }

    private void ApplyHighlightHoverState(bool hovered)
    {
        if (highlightVisual == null || highlightRenderer == null) return;
        if (!highlightVisual.gameObject.activeSelf) return;

        float emissionMul = hovered ? Mathf.Max(1f, hoverEmissionMultiplier) : 1f;
        if (runtimeHighlightMaterial != null && runtimeHighlightMaterial.HasProperty("_EmissionColor"))
        {
            Color emissionColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b) * (highlightEmission * emissionMul);
            runtimeHighlightMaterial.SetColor("_EmissionColor", emissionColor);
        }

        float scaleMul = hovered ? Mathf.Max(1f, hoverScaleMultiplier) : 1f;
        highlightVisual.localScale = baseHighlightScale * scaleMul;

        if (useOutline && outlineVisual != null)
        {
            outlineVisual.localScale = baseOutlineScale * scaleMul;
            if (runtimeOutlineMaterial != null && runtimeOutlineMaterial.HasProperty("_EmissionColor"))
            {
                float outlineEmissionMul = hovered ? Mathf.Max(1f, outlineHoverEmissionMultiplier) : 1f;
                Color emissionColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b) * (outlineEmission * outlineEmissionMul);
                runtimeOutlineMaterial.SetColor("_EmissionColor", emissionColor);
            }
        }
    }
}
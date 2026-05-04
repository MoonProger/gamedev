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
    public bool autoCreateHighlightSphere = true;
    public Transform highlightVisual;
    public Material highlightMaterial;
    public Color highlightColor = new Color(0.3f, 1f, 0.45f, 0.15f);
    public float highlightEmission = 1.5f;
    public float highlightScaleMultiplier = 0.55f;
    public float minHighlightScale = 0.2f;
    public float maxHighlightScale = 1.6f;
    public float highlightYOffset = 0.7f;
    [Header("Hover highlight")]
    public float hoverEmissionMultiplier = 1.8f;
    public float hoverScaleMultiplier = 1.12f;

    private Renderer highlightRenderer;
    private Material runtimeHighlightMaterial;
    private Vector3 baseHighlightScale = Vector3.one;
    private bool highlightActive;

    void Awake()
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

    void OnDrawGizmos() {
        Gizmos.color = Color.green;
        if (neighbors == null) return;
        foreach (var n in neighbors) {
            if (n != null) Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }

    private void EnsureHighlightVisual()
    {
        if (highlightVisual == null && autoCreateHighlightSphere)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "MoveHighlightSphere";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.up * highlightYOffset;

            Collider sphereCollider = sphere.GetComponent<Collider>();
            if (sphereCollider != null)
                Destroy(sphereCollider);

            float baseScale = 1f;
            if (rend != null)
            {
                Vector3 size = rend.bounds.size;
                baseScale = Mathf.Max(size.x, size.z);
            }
            float clampedScale = Mathf.Clamp(baseScale * highlightScaleMultiplier, minHighlightScale, maxHighlightScale);
            sphere.transform.localScale = Vector3.one * clampedScale;
            highlightVisual = sphere.transform;
        }

        if (highlightVisual == null)
            return;

        highlightRenderer = highlightVisual.GetComponent<Renderer>();
        if (highlightRenderer == null)
            return;

        Material mat = highlightMaterial != null
            ? new Material(highlightMaterial)
            : new Material(Shader.Find("Standard"));

        ConfigureTransparentGlowMaterial(mat);
        runtimeHighlightMaterial = mat;
        highlightRenderer.material = mat;
        baseHighlightScale = highlightVisual.localScale;
    }

    private void ConfigureTransparentGlowMaterial(Material mat)
    {
        if (mat == null) return;

        mat.color = highlightColor;
        if (mat.HasProperty("_EmissionColor"))
        {
            Color emissionColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b) * highlightEmission;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor);
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
    }
}
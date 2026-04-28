using UnityEngine;

public class CharacterPickCard : MonoBehaviour
{
    [Header("Визуал карты (как у обычных карт)")]
    public Renderer cardRenderer;
    [Header("Hover анимация")]
    public Camera hoverCamera;
    public bool hoverToCameraCenter = true;
    public float hoverCenterDistanceFromCamera = 7f;
    [Range(0f, 1f)] public float hoverCenterPull = 1f;
    public float hoverLocalZOffset = 1.2f;
    public float hoverScaleMultiplier = 1.18f;
    public float hoverLerpSpeed = 10f;
    public float clickDragThresholdPixels = 12f;

    private CharacterData characterData;
    private System.Action<CharacterData> onSelected;
    private bool isHovered;
    private bool pointerDown;
    private Vector3 pointerDownScreenPos;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private Vector3 baseWorldPosition;

    private void Awake()
    {
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    public void Bind(CharacterData character, System.Action<CharacterData> onClick)
    {
        characterData = character;
        onSelected = onClick;
        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        baseWorldPosition = transform.position;
        isHovered = false;
        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;

        if (cardRenderer != null)
            cardRenderer.material.mainTexture = character != null && character.portrait != null
                ? character.portrait.texture
                : null;
    }

    public void SetBaseTransformFromLayout(Vector3 localPosition, Vector3 localScale)
    {
        baseLocalPosition = localPosition;
        baseLocalScale = localScale;
        baseWorldPosition = transform.position;

        if (!isHovered)
        {
            transform.localPosition = baseLocalPosition;
            transform.localScale = baseLocalScale;
        }
    }

    private void Update()
    {
        Vector3 targetPosition;
        bool useWorldTarget = false;
        if (isHovered && hoverToCameraCenter)
        {
            Camera cam = GetActiveHoverCamera();
            if (cam != null)
            {
                float depth = hoverCenterDistanceFromCamera > 0f
                    ? hoverCenterDistanceFromCamera
                    : Vector3.Distance(cam.transform.position, baseWorldPosition);
                Vector3 centerWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
                targetPosition = Vector3.Lerp(baseWorldPosition, centerWorld, Mathf.Clamp01(hoverCenterPull));
                useWorldTarget = true;
            }
            else
            {
                targetPosition = baseLocalPosition + new Vector3(0f, 0f, hoverLocalZOffset);
            }
        }
        else
        {
            targetPosition = isHovered
                ? baseLocalPosition + new Vector3(0f, 0f, hoverLocalZOffset)
                : baseLocalPosition;
        }

        Vector3 targetScale = isHovered
            ? baseLocalScale * Mathf.Max(0.01f, hoverScaleMultiplier)
            : baseLocalScale;

        float t = Mathf.Clamp01(Time.deltaTime * Mathf.Max(0.01f, hoverLerpSpeed));
        if (useWorldTarget)
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        else
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, t);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
    }

    private void OnMouseEnter() => isHovered = true;
    private void OnMouseExit() => isHovered = false;

    private void OnMouseDown()
    {
        pointerDown = true;
        pointerDownScreenPos = Input.mousePosition;
    }

    private void OnMouseUp()
    {
        if (!pointerDown || characterData == null) return;
        pointerDown = false;
        float dragDistance = Vector3.Distance(pointerDownScreenPos, Input.mousePosition);
        if (dragDistance <= Mathf.Max(0f, clickDragThresholdPixels))
            onSelected?.Invoke(characterData);
    }

    private void OnDisable()
    {
        isHovered = false;
        pointerDown = false;
        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;
    }

    private Camera GetActiveHoverCamera()
    {
        if (hoverCamera != null && hoverCamera.enabled)
            return hoverCamera;
        if (Camera.main != null && Camera.main.enabled)
            return Camera.main;
        return null;
    }

}

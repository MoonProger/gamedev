using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance; 
    public GameObject tooltipObject;
    public TextMeshProUGUI tooltipText;
    [SerializeField] private float cursorGap = 20f;
    [SerializeField] private float screenPadding = 8f;

    private RectTransform tooltipRect;
    private RectTransform parentRect;
    private Canvas rootCanvas;

    void Awake()
    {
        Instance = this;
        tooltipRect = tooltipObject != null ? tooltipObject.GetComponent<RectTransform>() : null;
        parentRect = tooltipRect != null ? tooltipRect.parent as RectTransform : null;
        rootCanvas = tooltipObject != null ? tooltipObject.GetComponentInParent<Canvas>() : null;
    }

    void Update()
    {
        if (tooltipObject != null && tooltipObject.activeSelf)
        {
            PositionTooltip(Input.mousePosition);
        }
    }

    public void Show(string content)
    {
        tooltipObject.SetActive(true);
        tooltipText.text = content;
        Canvas.ForceUpdateCanvases();
        PositionTooltip(Input.mousePosition);
    }

    public void Hide()
    {
        tooltipObject.SetActive(false);
    }

    private void PositionTooltip(Vector2 mouseScreenPos)
    {
        if (tooltipRect == null || parentRect == null)
            return;

        float tooltipWidth = tooltipRect.rect.width;
        float tooltipHeight = tooltipRect.rect.height;

        bool canPlaceRight = mouseScreenPos.x + cursorGap + tooltipWidth <= Screen.width - screenPadding;
        bool canPlaceLeft = mouseScreenPos.x - cursorGap - tooltipWidth >= screenPadding;

        bool placeRight = canPlaceRight || !canPlaceLeft;
        tooltipRect.pivot = placeRight ? new Vector2(0f, 1f) : new Vector2(1f, 1f);

        Vector2 targetScreenPos = mouseScreenPos + new Vector2(placeRight ? cursorGap : -cursorGap, -cursorGap);
        Camera uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, targetScreenPos, uiCamera, out Vector2 localPoint))
            return;

        float minX = parentRect.rect.xMin + tooltipWidth * tooltipRect.pivot.x + screenPadding;
        float maxX = parentRect.rect.xMax - tooltipWidth * (1f - tooltipRect.pivot.x) - screenPadding;
        float minY = parentRect.rect.yMin + tooltipHeight * tooltipRect.pivot.y + screenPadding;
        float maxY = parentRect.rect.yMax - tooltipHeight * (1f - tooltipRect.pivot.y) - screenPadding;

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        tooltipRect.anchoredPosition = localPoint;
    }
}
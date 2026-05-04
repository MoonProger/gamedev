using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Корневая панель выбора")]
    public GameObject panel;
    public TextMeshProUGUI titleText;

    [Header("Карточки персонажей")]
    public Transform cardContainer;
    public GameObject cardPrefab;

    [Header("Режим раскладки")]
    public bool useCarouselLayout = true;
    public bool use3DLayout = true;

    [Header("3D карусель")]
    public float carouselArcDegrees = 120f;
    public float carouselRadius = 5f;
    public float carouselVerticalOffset = 0f;
    public float carouselDepthOffset = 0.5f;
    public float carouselDragSensitivity = 0.2f;
    public bool carouselScaleByDepth = true;
    public float carouselCenterScaleBoost = 0.2f;

    [Header("Сетка (fallback)")]
    public int cardsPerRow = 4;
    public float spacingX = 2.4f;
    public float spacingY = 3.2f;
    public Vector3 cardsLocalOffset = Vector3.zero;
    public bool overrideCardScale = false;
    public Vector3 cardLocalScale = Vector3.one;
    public Vector3 cardLocalEulerOffset = new Vector3(90f, 0f, 0f);
    public bool autoAlignToSelectionCamera = true;
    public float cardsDistanceFromCamera = 8f;
    public Vector2 viewportCenter = new Vector2(0.5f, 0.5f);
    public Vector3 containerWorldOffset = Vector3.zero;

    [Header("Камеры (опционально)")]
    public Camera selectionCamera;
    public Camera gameplayCamera;
    public bool switchToSelectionCamera = true;

    [Header("Временно скрыть на время выбора")]
    public List<GameObject> hideObjectsDuringSelection = new List<GameObject>();

    private readonly Dictionary<GameObject, bool> cachedActiveStates = new Dictionary<GameObject, bool>();
    private readonly List<Transform> spawnedCardTransforms = new List<Transform>();
    private readonly List<Vector3> spawnedCardBaseScales = new List<Vector3>();
    private float carouselAngleOffset;
    private bool isSelectionActive;
    private Vector3 lastMousePosition;

    public IEnumerator ShowAndPickForPlayers(
        List<PlayerController> activePlayers,
        IReadOnlyList<CharacterData> availableCharacters,
        System.Action<PlayerController, CharacterData> onPicked)
    {
        if (activePlayers == null || activePlayers.Count == 0 || availableCharacters == null || availableCharacters.Count == 0)
            yield break;

        if (panel == null || cardContainer == null || cardPrefab == null)
        {
            Debug.LogWarning("CharacterSelectionUI is not configured. Fallback to first character.");
            CharacterData fallback = availableCharacters[0];
            foreach (var player in activePlayers)
                onPicked?.Invoke(player, fallback);
            yield break;
        }

        if (switchToSelectionCamera)
            SetSelectionCameraState(true);

        if (autoAlignToSelectionCamera)
            AlignCardContainerToCamera();

        SetHiddenObjectsState(true);
        panel.SetActive(true);
        isSelectionActive = true;
        try
        {
            for (int playerIdx = 0; playerIdx < activePlayers.Count; playerIdx++)
            {
                PlayerController player = activePlayers[playerIdx];
                CharacterData picked = null;
                BuildCharacterCards(availableCharacters, selected => picked = selected);

                if (titleText != null)
                    titleText.text = $"Игрок \"{GetPlayerColorNameByIndex(playerIdx)}\", выберите персонажа";

                yield return new WaitUntil(() => picked != null);
                onPicked?.Invoke(player, picked);
            }
        }
        finally
        {
            isSelectionActive = false;
            ClearCards();
            panel.SetActive(false);
            SetHiddenObjectsState(false);
            if (switchToSelectionCamera)
                SetSelectionCameraState(false);
        }
    }

    private void BuildCharacterCards(IReadOnlyList<CharacterData> characters, System.Action<CharacterData> onSelected)
    {
        ClearCards();
        int index = 0;
        foreach (CharacterData character in characters)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            spawnedCardTransforms.Add(cardObj.transform);
            spawnedCardBaseScales.Add(cardObj.transform.localScale);
            if (!useCarouselLayout && use3DLayout)
                Apply3DCardLayout(cardObj.transform, index, characters.Count);

            CharacterPickCard pickCard = cardObj.GetComponent<CharacterPickCard>();
            if (pickCard != null)
            {
                pickCard.Bind(character, onSelected);
                index++;
                continue;
            }

            CharacterCardButtonView view = cardObj.GetComponent<CharacterCardButtonView>();
            if (view != null)
            {
                view.Bind(character, onSelected);
                index++;
                continue;
            }

            Button button = cardObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(character));
            }

            TextMeshProUGUI text = cardObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = character.displayName;

            index++;
        }
        if (useCarouselLayout)
            ApplyCarouselLayout(characters.Count);
    }

    private void Update()
    {
        if (!isSelectionActive || !useCarouselLayout || cardContainer == null || spawnedCardTransforms.Count == 0)
            return;

        if (Input.GetMouseButtonDown(0))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(0))
        {
            Vector3 current = Input.mousePosition;
            float deltaX = current.x - lastMousePosition.x;
            lastMousePosition = current;

            if (Mathf.Abs(deltaX) > 0.001f)
            {
                carouselAngleOffset += deltaX * carouselDragSensitivity;
                ApplyCarouselLayout(spawnedCardTransforms.Count);
            }
        }
    }

    private void ApplyCarouselLayout(int totalCount)
    {
        if (totalCount <= 0) return;

        float arc = Mathf.Max(5f, carouselArcDegrees);
        bool fullCircle = arc >= 359.9f;
        float step = fullCircle
            ? (arc / totalCount)
            : (totalCount > 1 ? arc / (totalCount - 1) : 0f);
        float start = -arc * 0.5f + carouselAngleOffset;
        float safeRadius = Mathf.Max(0.5f, carouselRadius);
        float safeDepthOffset = carouselDepthOffset;

        for (int i = 0; i < spawnedCardTransforms.Count; i++)
        {
            Transform card = spawnedCardTransforms[i];
            if (card == null) continue;

            float aDeg = start + step * i;
            float aRad = aDeg * Mathf.Deg2Rad;
            float x = Mathf.Sin(aRad) * safeRadius;
            float z = Mathf.Cos(aRad) * safeRadius - safeRadius + safeDepthOffset;

            card.localPosition = cardsLocalOffset + new Vector3(x, carouselVerticalOffset, z);
            card.localRotation = Quaternion.Euler(cardLocalEulerOffset);

            Vector3 baseScale = i < spawnedCardBaseScales.Count ? spawnedCardBaseScales[i] : card.localScale;
            Vector3 targetScale = baseScale;
            if (overrideCardScale)
            {
                targetScale = cardLocalScale;
            }
            else if (carouselScaleByDepth)
            {
                float depth01 = Mathf.InverseLerp(-safeRadius + safeDepthOffset, safeDepthOffset, z);
                float boost = 1f + carouselCenterScaleBoost * Mathf.Clamp01(depth01);
                targetScale = baseScale * boost;
            }
            card.localScale = targetScale;

            CharacterPickCard pickCard = card.GetComponent<CharacterPickCard>();
            if (pickCard != null)
                pickCard.SetBaseTransformFromLayout(card.localPosition, card.localScale);
        }
    }

    private string GetPlayerColorNameByIndex(int playerIndex)
    {
        return playerIndex switch
        {
            0 => "Красный",
            1 => "Синий",
            2 => "Зелёный",
            3 => "Жёлтый",
            _ => "Игрок"
        };
    }

    private void Apply3DCardLayout(Transform cardTransform, int index, int totalCount)
    {
        int safeCardsPerRow = Mathf.Max(1, cardsPerRow);
        int rowCount = Mathf.CeilToInt(totalCount / (float)safeCardsPerRow);
        int row = index / safeCardsPerRow;
        int col = index % safeCardsPerRow;

        int cardsInRow = Mathf.Min(safeCardsPerRow, totalCount - row * safeCardsPerRow);
        float rowWidth = (cardsInRow - 1) * spacingX;
        float x = (-rowWidth * 0.5f) + col * spacingX;

        float totalHeight = (rowCount - 1) * spacingY;
        float y = (totalHeight * 0.5f) - row * spacingY;

        cardTransform.localPosition = cardsLocalOffset + new Vector3(x, y, 0f);
        cardTransform.localRotation = Quaternion.Euler(cardLocalEulerOffset);
        Vector3 targetScale = cardTransform.localScale;
        if (overrideCardScale)
            targetScale = cardLocalScale;
        cardTransform.localScale = targetScale;

        CharacterPickCard pickCard = cardTransform.GetComponent<CharacterPickCard>();
        if (pickCard != null)
            pickCard.SetBaseTransformFromLayout(cardTransform.localPosition, cardTransform.localScale);
    }

    private void ClearCards()
    {
        spawnedCardTransforms.Clear();
        spawnedCardBaseScales.Clear();
        for (int i = cardContainer.childCount - 1; i >= 0; i--)
            Destroy(cardContainer.GetChild(i).gameObject);
    }

    private void AlignCardContainerToCamera()
    {
        Camera cam = selectionCamera != null ? selectionCamera : Camera.main;
        if (cam == null || cardContainer == null) return;

        float distance = Mathf.Max(0.5f, cardsDistanceFromCamera);
        Vector3 viewportPoint = new Vector3(
            Mathf.Clamp01(viewportCenter.x),
            Mathf.Clamp01(viewportCenter.y),
            distance);

        Vector3 worldPos = cam.ViewportToWorldPoint(viewportPoint) + containerWorldOffset;
        cardContainer.position = worldPos;
        cardContainer.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
    }

    private void SetSelectionCameraState(bool selectionActive)
    {
        if (selectionCamera != null)
            selectionCamera.enabled = selectionActive;
        if (gameplayCamera != null)
            gameplayCamera.enabled = !selectionActive;
    }

    private void SetHiddenObjectsState(bool hide)
    {
        if (hide)
        {
            cachedActiveStates.Clear();
            foreach (GameObject obj in hideObjectsDuringSelection)
            {
                if (obj == null || obj == panel) continue;
                cachedActiveStates[obj] = obj.activeSelf;
                obj.SetActive(false);
            }
            return;
        }

        foreach (var kv in cachedActiveStates)
        {
            if (kv.Key == null) continue;
            kv.Key.SetActive(kv.Value);
        }
        cachedActiveStates.Clear();
    }
}

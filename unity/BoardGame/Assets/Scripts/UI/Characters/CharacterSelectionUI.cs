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

    [Header("3D раскладка карточек")]
    public bool use3DLayout = true;
    public int cardsPerRow = 4;
    public float spacingX = 2.4f;
    public float spacingY = 3.2f;
    public Vector3 cardsLocalOffset = Vector3.zero;
    public Vector3 cardLocalScale = Vector3.one;
    public bool autoAlignToSelectionCamera = true;
    public float cardsDistanceFromCamera = 8f;
    public Vector2 viewportCenter = new Vector2(0.5f, 0.5f);
    public Vector3 containerWorldOffset = Vector3.zero;

    [Header("Камеры (опционально)")]
    public Camera selectionCamera;
    public Camera gameplayCamera;
    public bool switchToSelectionCamera = true;

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

        panel.SetActive(true);
        try
        {
            foreach (PlayerController player in activePlayers)
            {
                CharacterData picked = null;
                BuildCharacterCards(availableCharacters, selected => picked = selected);

                if (titleText != null)
                    titleText.text = $"Выбор персонажа: {player.playerName}";

                yield return new WaitUntil(() => picked != null);
                onPicked?.Invoke(player, picked);
            }
        }
        finally
        {
            ClearCards();
            panel.SetActive(false);
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
            if (use3DLayout)
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
        cardTransform.localRotation = Quaternion.identity;
        cardTransform.localScale = cardLocalScale;
    }

    private void ClearCards()
    {
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
        cardContainer.forward = cam.transform.forward;
    }

    private void SetSelectionCameraState(bool selectionActive)
    {
        if (selectionCamera != null)
            selectionCamera.enabled = selectionActive;
        if (gameplayCamera != null)
            gameplayCamera.enabled = !selectionActive;
    }
}

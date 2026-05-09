using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Card Visual")]
    public CardVisual cardVisual;

    [Header("Общие данные")]
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI expText;
    public TextMeshProUGUI successText;
    public TextMeshProUGUI grantsText;

    [Header("8 Сфер")]
    public TextMeshProUGUI volunteerText;
    public TextMeshProUGUI scienceText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI mediaText;
    public TextMeshProUGUI businessText;
    public TextMeshProUGUI sportText;
    public TextMeshProUGUI tourismText;
    public TextMeshProUGUI itText;

    [Header("Небольшой статус хода")]
    public TextMeshProUGUI currentTurnStatusText;

    [Header("Gameplay HUD")]
    /// <summary>Если задан — показывает/скрывает весь игровой интерфейс одним объектом (рекомендуется).</summary>
    public GameObject gameplayHudRoot;

    [Header("Общий лог")]
    public GameObject logPanel;
    public GameObject logTextRoot;
    public TextMeshProUGUI logText;
    public ScrollRect logScrollRect;
    public bool autoScrollToLatest = true;
    public bool autoScrollOnlyWhenNearBottom = true;
    [Range(0f, 1f)] public float autoScrollBottomThreshold = 0.08f;
    public int maxLogEntries = 250;

    [Header("Экран победы")]
    public CanvasGroup victoryOverlay;
    public TextMeshProUGUI victoryText;
    public float victoryFadeDuration = 0.45f;

    private readonly List<string> logEntries = new List<string>();
    private readonly StringBuilder logBuilder = new StringBuilder(8192);
    private int currentLogTurn = 1;
    private Coroutine victoryFadeRoutine;
    private bool isInitialized;

    private void Awake()
    {
        InitializeUiState();
    }

    private void Start()
    {
        InitializeUiState();
        RefreshLogView();
    }

    private void InitializeUiState()
    {
        if (isInitialized)
            return;
        isInitialized = true;

        // Панель можно оставить активной (например, чтобы кнопка всегда была видна),
        // скрываем только область текста лога.
        SetLogTextVisible(false);
        if (logText != null)
        {
            logText.alignment = TextAlignmentOptions.BottomLeft;
            // Иначе колесо мыши часто "съедается" самим текстом, а не ScrollRect.
            logText.raycastTarget = false;
        }
        if (currentTurnStatusText != null)
            currentTurnStatusText.gameObject.SetActive(false);
        if (victoryOverlay != null)
        {
            victoryOverlay.alpha = 0f;
            victoryOverlay.gameObject.SetActive(false);
            victoryOverlay.blocksRaycasts = false;
            victoryOverlay.interactable = false;
        }
    }

    public void UpdateAllStats(PlayerController player)
    {
        if (player == null) return;

        moneyText.text = player.money.ToString();
        expText.text = player.experience.ToString();
        successText.text = player.success.ToString();
        if (grantsText != null)
            grantsText.text = (player.earnedGrants != null ? player.earnedGrants.Count : 0).ToString();

        volunteerText.text = player.volounteer.ToString();
        scienceText.text = player.science.ToString();
        artText.text = player.art.ToString();
        mediaText.text = player.media.ToString();
        businessText.text = player.business.ToString();
        sportText.text = player.sport.ToString();
        tourismText.text = player.tourism.ToString();
        itText.text = player.IT.ToString();
    }

    // Совместимость со старым API: теперь пишет только в общий лог.
    public void ShowNotification(string message, float? duration = null)
    {
        AddLog(message);
    }

    public void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        int safeTurn = Mathf.Max(1, currentLogTurn);
        logEntries.Add($"[Ход {safeTurn}] {message}");

        if (logEntries.Count > maxLogEntries)
            logEntries.RemoveRange(0, logEntries.Count - maxLogEntries);

        RefreshLogView();
    }

    public void SetCurrentTurnNumber(int turnNumber)
    {
        currentLogTurn = Mathf.Max(1, turnNumber);
    }

    /// <summary>Скрывает/показывает игровой HUD (до выбора персонажа всё можно отключить; выбор персонажа — отдельный объект).</summary>
    public void SetGameplayHudVisible(bool visible)
    {
        if (gameplayHudRoot != null)
        {
            gameplayHudRoot.SetActive(visible);
            return;
        }

        void ToggleStatLabel(TextMeshProUGUI tmp)
        {
            if (tmp != null)
                tmp.gameObject.SetActive(visible);
        }

        ToggleStatLabel(moneyText);
        ToggleStatLabel(expText);
        ToggleStatLabel(successText);
        ToggleStatLabel(grantsText);
        ToggleStatLabel(volunteerText);
        ToggleStatLabel(scienceText);
        ToggleStatLabel(artText);
        ToggleStatLabel(mediaText);
        ToggleStatLabel(businessText);
        ToggleStatLabel(sportText);
        ToggleStatLabel(tourismText);
        ToggleStatLabel(itText);

        if (logPanel != null)
            logPanel.SetActive(visible);
        if (cardVisual != null)
            cardVisual.gameObject.SetActive(visible);

        var logRoot = GetLogTextRoot();
        if (!visible && logRoot != null)
            logRoot.SetActive(false);
    }

    public void SetCurrentTurnStatus(string status)
    {
        if (currentTurnStatusText == null) return;
        currentTurnStatusText.text = status;
        currentTurnStatusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(status));
    }

    public void ToggleLogPanel()
    {
        bool makeVisible = !(GetLogTextRoot()?.activeSelf ?? false);
        SetLogTextVisible(makeVisible);
        if (makeVisible)
            RefreshLogView();
    }

    public void OpenLogPanel()
    {
        SetLogTextVisible(true);
        RefreshLogView();
    }

    public void CloseLogPanel()
    {
        SetLogTextVisible(false);
    }

    public void ClearLog()
    {
        logEntries.Clear();
        RefreshLogView();
    }

    public void ShowVictoryScreen(string winnerName, Color winnerColor)
    {
        if (victoryOverlay == null)
            return;

        if (victoryText != null)
        {
            string safeName = string.IsNullOrWhiteSpace(winnerName) ? "Игрок" : winnerName;
            string colorHex = ColorUtility.ToHtmlStringRGB(winnerColor);
            victoryText.text = $"<b>Победил <color=#{colorHex}>{safeName}</color></b>";
        }

        if (victoryFadeRoutine != null)
            StopCoroutine(victoryFadeRoutine);
        victoryFadeRoutine = StartCoroutine(FadeInVictoryOverlay());
    }

    public void ShowPauseScreen(string reason)
    {
        if (victoryOverlay == null)
            return;

        if (victoryText != null)
        {
            string suffix = string.IsNullOrWhiteSpace(reason) ? "" : $"\n<size=65%>{reason}</size>";
            victoryText.text = $"<b>Игра на паузе</b>{suffix}";
        }

        if (victoryFadeRoutine != null)
            StopCoroutine(victoryFadeRoutine);
        victoryFadeRoutine = StartCoroutine(FadeInVictoryOverlay());
    }

    public void HideOverlayScreen()
    {
        if (victoryFadeRoutine != null)
        {
            StopCoroutine(victoryFadeRoutine);
            victoryFadeRoutine = null;
        }
        if (victoryOverlay == null)
            return;
        victoryOverlay.alpha = 0f;
        victoryOverlay.gameObject.SetActive(false);
        victoryOverlay.blocksRaycasts = false;
        victoryOverlay.interactable = false;
    }

    private void RefreshLogView()
    {
        if (logText == null) return;
        bool shouldScrollToBottom = ShouldAutoScrollToBottom();

        if (logEntries.Count == 0)
        {
            logText.text = "Лог пуст.";
            if (shouldScrollToBottom && logScrollRect != null)
                logScrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        logBuilder.Clear();
        logBuilder.EnsureCapacity(Mathf.Max(logBuilder.Capacity, logEntries.Count * 48));
        for (int i = 0; i < logEntries.Count; i++)
        {
            logBuilder.Append(logEntries[i]);
            if (i < logEntries.Count - 1) logBuilder.Append('\n');
        }
        logText.text = logBuilder.ToString();

        if (shouldScrollToBottom && logScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            // Для вертикального ScrollRect: 0 = низ (последние записи), 1 = верх.
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private GameObject GetLogTextRoot()
    {
        if (logTextRoot != null) return logTextRoot;
        return logText != null ? logText.gameObject : null;
    }

    private void SetLogTextVisible(bool visible)
    {
        GameObject root = GetLogTextRoot();
        if (root != null)
            root.SetActive(visible);
    }

    private bool ShouldAutoScrollToBottom()
    {
        if (!autoScrollToLatest || logScrollRect == null)
            return false;
        if (!autoScrollOnlyWhenNearBottom)
            return true;
        return logScrollRect.verticalNormalizedPosition <= Mathf.Clamp01(autoScrollBottomThreshold);
    }

    private IEnumerator FadeInVictoryOverlay()
    {
        victoryOverlay.gameObject.SetActive(true);
        victoryOverlay.blocksRaycasts = true;
        victoryOverlay.interactable = true;

        float duration = Mathf.Max(0.01f, victoryFadeDuration);
        float startAlpha = victoryOverlay.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            victoryOverlay.alpha = Mathf.Lerp(startAlpha, 1f, t);
            yield return null;
        }

        victoryOverlay.alpha = 1f;
        victoryFadeRoutine = null;
    }
}
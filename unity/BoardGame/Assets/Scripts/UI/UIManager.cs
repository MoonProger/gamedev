using UnityEngine;
using TMPro;
using System.Collections.Generic;
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

    [Header("Общий лог")]
    public GameObject logPanel;
    public GameObject logTextRoot;
    public TextMeshProUGUI logText;
    public ScrollRect logScrollRect;
    public bool autoScrollToLatest = true;
    public int maxLogEntries = 250;

    private readonly List<string> logEntries = new List<string>();

    private void Start()
    {
        // Панель можно оставить активной (например, чтобы кнопка всегда была видна),
        // скрываем только область текста лога.
        SetLogTextVisible(false);
        RefreshLogView();
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

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        logEntries.Add($"[{timestamp}] {message}");

        if (logEntries.Count > maxLogEntries)
            logEntries.RemoveRange(0, logEntries.Count - maxLogEntries);

        RefreshLogView();
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

    private void RefreshLogView()
    {
        if (logText == null) return;

        if (logEntries.Count == 0)
        {
            logText.text = "Лог пуст.";
            return;
        }

        StringBuilder sb = new StringBuilder(logEntries.Count * 48);
        for (int i = 0; i < logEntries.Count; i++)
        {
            sb.Append(logEntries[i]);
            if (i < logEntries.Count - 1) sb.Append('\n');
        }
        logText.text = sb.ToString();

        if (autoScrollToLatest && logScrollRect != null)
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
}
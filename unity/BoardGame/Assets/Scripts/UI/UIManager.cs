using UnityEngine;
using TMPro; 
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    private struct NotificationEntry
    {
        public string message;
        public float duration;
    }

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

    [Header("Уведомления")]
    public GameObject notificationRoot;
    public TextMeshProUGUI notificationText;
    public float notificationDuration = 2f;

    private Coroutine notificationRoutine;
    private readonly Queue<NotificationEntry> notificationQueue = new Queue<NotificationEntry>();

    private void Start()
    {
        if (notificationRoot != null)
            notificationRoot.SetActive(false);
        else if (notificationText != null)
            notificationText.gameObject.SetActive(false);
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

    public void ShowNotification(string message, float? duration = null)
    {
        if (notificationText == null)
        {
            Debug.LogWarning("UIManager: notificationText is not assigned.");
            return;
        }

        notificationQueue.Enqueue(new NotificationEntry
        {
            message = message,
            duration = duration ?? notificationDuration
        });

        if (notificationRoutine == null)
            notificationRoutine = StartCoroutine(ProcessNotificationQueue());
    }

    private IEnumerator ProcessNotificationQueue()
    {
        while (notificationQueue.Count > 0)
        {
            NotificationEntry entry = notificationQueue.Dequeue();
            notificationText.text = entry.message;
            if (notificationRoot != null)
                notificationRoot.SetActive(true);
            else
                notificationText.gameObject.SetActive(true);

            yield return new WaitForSeconds(entry.duration);

            if (notificationRoot != null)
                notificationRoot.SetActive(false);
            else
                notificationText.gameObject.SetActive(false);
        }

        notificationRoutine = null;
    }
}
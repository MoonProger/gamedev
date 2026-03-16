using UnityEngine;
using TMPro; 
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Общие данные")]
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI expText;
    public TextMeshProUGUI successText;

    [Header("8 Сфер")]
    public TextMeshProUGUI volunteerText;
    public TextMeshProUGUI scienceText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI mediaText;
    public TextMeshProUGUI businessText;
    public TextMeshProUGUI sportText;
    public TextMeshProUGUI tourismText;
    public TextMeshProUGUI itText;

    [Header("Card UI")]
    public GameObject cardPanel;      // Само окно карточки
    public TMPro.TextMeshProUGUI cardTitle;
    public TMPro.TextMeshProUGUI cardDesc;

public void ShowCard(CardResult result)
{
    cardPanel.SetActive(true);
    cardTitle.text = result.title;
    cardDesc.text = result.description;
}

public void HideCard()
{
    cardPanel.SetActive(false);
}

    public void UpdateAllStats(PlayerController player)
    {
        if (player == null) return;

        moneyText.text = player.money.ToString();
        expText.text = player.experience.ToString();
        successText.text = player.success.ToString();

        volunteerText.text = player.volounteer.ToString();
        scienceText.text = player.science.ToString();
        artText.text = player.art.ToString();
        mediaText.text = player.media.ToString();
        businessText.text = player.business.ToString();
        sportText.text = player.sport.ToString();
        tourismText.text = player.tourism.ToString();
        itText.text = player.IT.ToString();
    }
}
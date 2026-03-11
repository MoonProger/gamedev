using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GreenCardUI : MonoBehaviour
{
    [Header("Панель зелёной карточки")]
    public GameObject panel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI mainStatText;
    public Transform partnerButtonContainer;
    public GameObject partnerButtonPrefab;

    private System.Action<PlayerController> onPartnerChosen;

    public IEnumerator ShowAndWait(string mainStat, List<PlayerController> candidates, System.Action<PlayerController> callback)
    {
        onPartnerChosen = callback;

        titleText.text = "🟢 Team Project";
        mainStatText.text = $"Sphere: {mainStat.ToUpper()}\nChoose Partner:";

        foreach (Transform child in partnerButtonContainer)
            Destroy(child.gameObject);

        foreach (PlayerController candidate in candidates)
        {
            PlayerController captured = candidate;
            GameObject btn = Instantiate(partnerButtonPrefab, partnerButtonContainer);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = captured.playerName;
            btn.GetComponent<Button>().onClick.AddListener(() => ChoosePartner(captured));
        }

        panel.SetActive(true);
        yield return new WaitUntil(() => !panel.activeSelf);
    }

    private void ChoosePartner(PlayerController partner)
    {
        panel.SetActive(false);
        onPartnerChosen?.Invoke(partner);
    }
}
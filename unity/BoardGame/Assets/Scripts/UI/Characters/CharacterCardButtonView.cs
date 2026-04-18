using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCardButtonView : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI statsText;
    public Image portraitImage;

    public void Bind(CharacterData character, System.Action<CharacterData> onSelected)
    {
        if (character == null) return;
        if (button == null) button = GetComponent<Button>();

        if (nameText != null)
            nameText.text = string.IsNullOrWhiteSpace(character.displayName) ? "Без имени" : character.displayName;

        if (statsText != null)
        {
            statsText.text =
                $"Деньги: {character.money} | Опыт: {character.experience} | Успех: {character.success}\n" +
                $"Вол: {character.volounteer} Нау: {character.science} Иск: {character.art} Мед: {character.media}\n" +
                $"Биз: {character.business} Спорт: {character.sport} Тур: {character.tourism} IT: {character.it}";
        }

        if (portraitImage != null)
            portraitImage.sprite = character.portrait;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelected?.Invoke(character));
        }
    }
}

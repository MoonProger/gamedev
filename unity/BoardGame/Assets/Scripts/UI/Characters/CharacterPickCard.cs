using UnityEngine;

public class CharacterPickCard : MonoBehaviour
{
    [Header("Визуал карты (как у обычных карт)")]
    public Renderer cardRenderer;

    private CharacterData characterData;
    private System.Action<CharacterData> onSelected;

    private void Awake()
    {
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    public void Bind(CharacterData character, System.Action<CharacterData> onClick)
    {
        characterData = character;
        onSelected = onClick;

        if (cardRenderer != null)
            cardRenderer.material.mainTexture = character != null && character.portrait != null
                ? character.portrait.texture
                : null;
    }

    private void OnMouseDown()
    {
        if (characterData == null) return;
        onSelected?.Invoke(characterData);
    }
}

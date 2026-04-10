using UnityEngine;
using TMPro;
using System.Collections;

public class CardVisual : MonoBehaviour
{
    [Header("Поля карточки")]
    public Renderer cardImageRenderer;


    [Header("Позиции")]
    public Transform hiddenPosition;   // куда карточка уходит (за стол)
    public Transform shownPosition;    // перед камерой
    public float animDuration = 0.4f;
    private bool isLocked = false;

    private static readonly Color ColorYellow   = new Color(1f,    0.85f, 0.1f);
    private static readonly Color ColorBlue     = new Color(0.29f, 0.56f, 0.85f);
    private static readonly Color ColorRed      = new Color(0.9f,  0.2f,  0.2f);
    private static readonly Color ColorGreen    = new Color(0.18f, 0.8f,  0.44f);
    private static readonly Color ColorSurprise = new Color(0.95f, 0.61f, 0.07f);
    private static readonly Color ColorTravel   = new Color(0.20f, 0.75f, 0.95f);
    private static readonly Color ColorGrant    = new Color(0.65f, 0.45f, 0.95f);

    private bool isShown = false;
    private Coroutine currentAnim;

    // Коллайдер для клика
    private void Awake()
    {
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();
    }

    private void OnMouseDown()
    {
         if (isShown && !isLocked) Hide();
    }

public void Show(CardData card, string sphere, string extraDesc = "")
{
    if (card == null) return;
    ApplyVisuals(card.cardType, sphere, card.image);
    AnimateTo(shownPosition);
    isShown = true;
}

public void ShowRaw(string title, string description, CardType type, string sphere)
{
    ApplyVisuals(type, sphere, null);
    AnimateTo(shownPosition);
    isShown = true;
}

    public void Hide()
    {
        AnimateTo(hiddenPosition);
        isShown = false;
    }

    private void ApplyVisuals(CardType type, string sphere, Sprite image)
    {
        Color cardColor = type switch
        {
            CardType.Yellow   => ColorYellow,
            CardType.Blue     => ColorBlue,
            CardType.Red      => ColorRed,
            CardType.Green    => ColorGreen,
            CardType.Surprise => ColorSurprise,
            CardType.Travel   => ColorTravel,
            CardType.Grant    => ColorGrant,
            _ => Color.white
        };

        if (cardImageRenderer != null)
            cardImageRenderer.material.mainTexture = image != null ? image.texture : null;
    }

    private void AnimateTo(Transform target)
{
    if (currentAnim != null) StopCoroutine(currentAnim);
    currentAnim = StartCoroutine(MoveAndRotate(target));
}

private IEnumerator MoveAndRotate(Transform target)
{
    Vector3 startPos = transform.position;
    Quaternion startRot = transform.rotation;
    float elapsed = 0f;

    while (elapsed < animDuration)
    {
        elapsed += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
        transform.position = Vector3.Lerp(startPos, target.position, t);
        transform.rotation = Quaternion.Lerp(startRot, target.rotation, t);
        yield return null;
    }

    transform.position = target.position;
    transform.rotation = target.rotation;
}
public void SetLocked(bool locked) => isLocked = locked;
}
using UnityEngine;
using TMPro;
using System.Collections;

public class CardVisual : MonoBehaviour
{
    [Header("Поля карточки")]
    public Renderer sphereFieldRenderer;
    public TextMeshPro sphereText;
    public Renderer logoFieldRenderer;
    public TextMeshPro titleText;
    public TextMeshPro descriptionText;

    [Header("Позиции")]
    public Transform hiddenPosition;   // куда карточка уходит (за стол)
    public Transform shownPosition;    // перед камерой

    [Header("Анимация")]
    public float animDuration = 0.4f;

    private static readonly Color ColorYellow   = new Color(1f,    0.85f, 0.1f);
    private static readonly Color ColorBlue     = new Color(0.29f, 0.56f, 0.85f);
    private static readonly Color ColorRed      = new Color(0.9f,  0.2f,  0.2f);
    private static readonly Color ColorGreen    = new Color(0.18f, 0.8f,  0.44f);
    private static readonly Color ColorSurprise = new Color(0.95f, 0.61f, 0.07f);

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
        if (isShown) Hide();
    }

 public void Show(CardData card, string sphere)
{
    ApplyVisuals(card.cardType, sphere, card.title, card.description);
    AnimateTo(shownPosition);  // ← добавить эту строку
    isShown = true;
}

public void ShowRaw(string title, string description, CardType type, string sphere)
{
    ApplyVisuals(type, sphere, title, description);
    AnimateTo(shownPosition);  // ← добавить эту строку
    isShown = true;
}

    public void Hide()
    {
        AnimateTo(hiddenPosition);
        isShown = false;
    }

    private void ApplyVisuals(CardType type, string sphere, string title, string description)
    {
        Color cardColor = type switch
        {
            CardType.Yellow   => ColorYellow,
            CardType.Blue     => ColorBlue,
            CardType.Red      => ColorRed,
            CardType.Green    => ColorGreen,
            CardType.Surprise => ColorSurprise,
            _ => Color.white
        };

        if (sphereFieldRenderer != null)
            sphereFieldRenderer.material.color = cardColor;
        if (logoFieldRenderer != null)
            logoFieldRenderer.material.color = cardColor;
        if (sphereText != null)
            sphereText.text = sphere.ToUpper();
        if (titleText != null)
            titleText.text = title;
        if (descriptionText != null)
            descriptionText.text = description;
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
}
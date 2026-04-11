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

    private bool isShown = false;
    private bool isAnimating = false;
    private Coroutine currentAnim;
    public bool IsShown => isShown;
    public bool IsAnimating => isAnimating;

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
    isAnimating = true;
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
    isAnimating = false;
    currentAnim = null;
}
public void SetLocked(bool locked) => isLocked = locked;
}
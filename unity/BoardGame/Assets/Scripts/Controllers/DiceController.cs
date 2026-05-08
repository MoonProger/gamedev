using UnityEngine;
using System;
using System.Collections;

public class DiceController : MonoBehaviour
{
    private static readonly Vector3[] FaceVectors =
    {
        Vector3.up,      // 1
        Vector3.left,    // 2
        Vector3.back,    // 3
        Vector3.forward, // 4
        Vector3.right,   // 5
        Vector3.down     // 6
    };

    public AudioClip rollSound;
    private AudioSource audioSource;    

    public event Action<int> OnDiceRolled;

    private bool isRolling = false;
    public bool IsRolling => isRolling;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }


    public void RollDice()
    {
        if (isRolling) return;
        
        int result = UnityEngine.Random.Range(1, 7);
        StartCoroutine(RollRoutine(result, true));
    }

    public void RollDiceToValue(int result)
    {
        if (isRolling) return;
        int clamped = Mathf.Clamp(result, 1, 6);
        StartCoroutine(RollRoutine(clamped, false));
    }

private IEnumerator RollRoutine(int result, bool emitEvent)
{
    if (rollSound != null && audioSource != null) audioSource.PlayOneShot(rollSound);
    isRolling = true;

    Vector3 directionToCamera = (Camera.main.transform.position - transform.position).normalized;
    Vector3 faceToLook = FaceVectors[result - 1];

    Quaternion finalRotation = Quaternion.FromToRotation(faceToLook, directionToCamera);
    finalRotation *= Quaternion.AngleAxis(UnityEngine.Random.Range(0, 4) * 90f, faceToLook);

    int spinsX = UnityEngine.Random.Range(2, 4);
    int spinsY = UnityEngine.Random.Range(2, 4);
    Vector3 startEuler = transform.eulerAngles;
    Vector3 targetEuler = finalRotation.eulerAngles + new Vector3(360f * spinsX, 360f * spinsY, 0);

    float duration = 1.5f;
    float t = 0f;
    while (t < duration)
    {
        float progress = t / duration;
        float smooth = Mathf.SmoothStep(0, 1, progress);
        transform.rotation = Quaternion.Euler(Vector3.Lerp(startEuler, targetEuler, smooth));
        t += Time.deltaTime;
        yield return null;
    }

    transform.rotation = finalRotation;

    if (emitEvent)
        OnDiceRolled?.Invoke(result);
    isRolling = false;
}

}
using UnityEngine;
using System;
using System.Collections;

public class DiceController : MonoBehaviour
{
    public AudioClip rollSound;
    private AudioSource audioSource;    

    public event Action<int> OnDiceRolled;

    private bool isRolling = false;

    private Quaternion[] diceRotations;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        diceRotations = new Quaternion[]
        {
            Quaternion.Euler(-90, 0, 0),      // 1
            Quaternion.Euler(0, -90, 0),      // 2
            Quaternion.Euler(0, 0, 0),       // 3
            Quaternion.Euler(0, 180, 0),     // 4 
            Quaternion.Euler(0, 90, 0),     // 5 
            Quaternion.Euler(90, 0, 0)      // 6 
        };
    }


    public void RollDice()
    {
        if (isRolling) return;

        StartCoroutine(RollRoutine());
    }

private IEnumerator RollRoutine()
{
    audioSource.PlayOneShot(rollSound);

    isRolling = true;

    int result = UnityEngine.Random.Range(1, 7);

    Quaternion finalRotation = diceRotations[result - 1];

    int spinsX = UnityEngine.Random.Range(2,5);
    int spinsY = UnityEngine.Random.Range(2, 5);

    Vector3 startEuler = transform.eulerAngles;

    Vector3 finalEuler = finalRotation.eulerAngles;

    Vector3 targetEuler = finalEuler + new Vector3(
        360f * spinsX,
        360f * spinsY,
        0
    );

    float duration = 1.5f;
    float t = 0f;

    while (t < duration)
    {
        float progress = t / duration;

        float smooth = Mathf.SmoothStep(0, 1, progress);

        Vector3 currentEuler = Vector3.Lerp(startEuler, targetEuler, smooth);

        transform.rotation = Quaternion.Euler(currentEuler);

        t += Time.deltaTime;
        yield return null;
    }

    transform.rotation = finalRotation;

    Debug.Log("🎲 Выпало: " + result);

    OnDiceRolled?.Invoke(result);

    isRolling = false;
}

}
using UnityEngine;

public class DiceClick : MonoBehaviour
{
    private DiceController dice;

    void Start()
    {
        dice = GetComponent<DiceController>();
    }

    void OnMouseDown()
    {
        dice.RollDice();
    }
}
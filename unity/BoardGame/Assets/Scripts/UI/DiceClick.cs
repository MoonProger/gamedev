using UnityEngine;

public class DiceClick : MonoBehaviour
{
    private GameManager gameManager;

    void Start()
    {
        // Ищем GameManager на сцене
        gameManager = Object.FindFirstObjectByType<GameManager>();
    }

    void OnMouseDown()
    {
        if (gameManager != null)
        {
            // Вызываем проверку в менеджере, а не бросок напрямую
            gameManager.TryRollDice();
        }
    }
}
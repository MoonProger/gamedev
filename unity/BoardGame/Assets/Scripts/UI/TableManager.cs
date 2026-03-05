using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    [Header("Table Setup")]
    public GameObject tablePanel;
    public Transform verticalGroup; 
    public GameObject tokenPrefab;

    private GameObject[,] playerMarkers;
    private bool isInitialized = false;

    public void InitializeTable()
    {
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        
        // Проверка: если игроки еще не пришли, не инициализируем
        if (gm.expectedPlayerCount <= 0) return;

        playerMarkers = new GameObject[gm.expectedPlayerCount, 8];
        Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };

        for (int p = 0; p < gm.expectedPlayerCount; p++)
        {
            for (int s = 0; s < 8; s++)
            {
                GameObject marker = Instantiate(tokenPrefab, tablePanel.transform);
                marker.GetComponent<Image>().color = colors[p];
                playerMarkers[p, s] = marker;
                marker.SetActive(false); // Скрываем до открытия таблицы
            }
        }
        isInitialized = true;
    }

    public void UpdateTablePositions()
    {
        if (!isInitialized) return;
        GameManager gm = Object.FindFirstObjectByType<GameManager>();

        for (int p = 0; p < gm.expectedPlayerCount; p++)
        {
            for (int s = 0; s < 8; s++)
            {
                string statName = GetStatNameByIndex(s);
                int level = gm.players[p].GetStatValue(statName);

                Transform row = verticalGroup.GetChild(level);
                
                // 2. Находим ячейку (Cell) внутри этой строки по индексу сферы
                // +1 если у тебя в строке первая ячейка — это заголовок с цифрой
                Transform cell = row.GetChild(s+1); 

                // 3. Берем мировую позицию центра ячейки
                Vector3 targetPos = cell.position;

                // Смещение, чтобы игроки не сливались
                targetPos += new Vector3(-55f+ p*35f, 0, 0);

                playerMarkers[p, s].transform.position = targetPos;
            }
        }
    }

    private string GetStatNameByIndex(int index)
    {
        string[] stats = { "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };
        return stats[index];
    }

public void ToggleTable()
    {
        // Если вдруг нажали кнопку раньше, чем пришли данные
        if (!isInitialized) InitializeTable();

        if (tablePanel != null)
        {
            bool isActive = !tablePanel.activeSelf;
            tablePanel.SetActive(isActive);
            
            // Показываем/скрываем маркеры
            if (isInitialized)
            {
                for (int p = 0; p < playerMarkers.GetLength(0); p++)
                    for (int s = 0; s < 8; s++)
                        playerMarkers[p, s].SetActive(isActive);
            }

            if (isActive) UpdateTablePositions();
        }
    }
}
using UnityEngine;

public enum TableStatKey
{
    Volounteer = 0,
    Science = 1,
    Art = 2,
    Media = 3,
    Business = 4,
    Sport = 5,
    Tourism = 6,
    IT = 7
}

public class TableCellAnchor : MonoBehaviour
{
    [Tooltip("Сфера, к которой относится ячейка.")]
    public TableStatKey statKey = TableStatKey.Volounteer;

    [Range(0, 10)]
    [Tooltip("Уровень в сфере (0..10).")]
    public int level = 0;

    public string GetStatKeyString()
    {
        return statKey switch
        {
            TableStatKey.Volounteer => "volounteer",
            TableStatKey.Science => "science",
            TableStatKey.Art => "art",
            TableStatKey.Media => "media",
            TableStatKey.Business => "business",
            TableStatKey.Sport => "sport",
            TableStatKey.Tourism => "tourism",
            TableStatKey.IT => "it",
            _ => "volounteer"
        };
    }
}

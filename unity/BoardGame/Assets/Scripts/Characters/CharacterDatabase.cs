using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterData
{
    public string id;
    public string displayName;
    public Sprite portrait;

    [Header("Экономика и прогресс")]
    public int money;
    public int experience;
    public int success;

    [Header("8 сфер жизни (0-10)")]
    public int volounteer = 5;
    public int science = 5;
    public int art = 5;
    public int media = 5;
    public int business = 5;
    public int sport = 5;
    public int tourism = 5;
    public int it = 5;
}

public class CharacterDatabase : MonoBehaviour
{
    public static CharacterDatabase Instance { get; private set; }

    [Header("Набор стартовых персонажей")]
    public List<CharacterData> characters = new List<CharacterData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate CharacterDatabase found. Destroying extra instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public IReadOnlyList<CharacterData> GetAllCharacters()
    {
        return characters;
    }
}

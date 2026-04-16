using UnityEngine;
using System.Collections.Generic;

public enum CardEffect
{
    None,
    SkipNextTurn,
    GainStat,
    LoseStat,
    DrawNextCardFromSameSphere
}

[System.Serializable]
public enum CardEffectCondition
{
    Always,
    OnSuccess,
    OnFailure
}

[System.Serializable]
public enum CardStat
{
    CurrentSphere = 0,
    Money = 1,
    Experience = 2,
    Success = 3,
    Volounteer = 4,
    Science = 5,
    Art = 6,
    Media = 7,
    Business = 8,
    Sport = 9,
    Tourism = 10,
    IT = 11,
    LastSphere = 12
}

[System.Serializable]
public class CardEffectData
{
    public CardEffect effect;
    public CardEffectCondition condition = CardEffectCondition.Always;
    public CardStat statName = CardStat.CurrentSphere;
    public int amount;
}

[System.Serializable]
public class CardData
{
    public CardType cardType;
    public Sprite image;
    public List<CardEffectData> effects = new List<CardEffectData>();
    public List<CardEffectData> soloEffects = new List<CardEffectData>();
    public List<CardEffectData> coopEffects = new List<CardEffectData>();
}

[System.Serializable]
public class CardDeckEntry
{
    public string key;
    public List<CardData> cards = new List<CardData>();
}

public class CardDatabase : MonoBehaviour
{
    public static CardDatabase Instance { get; private set; }

    [Header("Decks by key: science, art, business, sport, media, volounteer, tourism, it, travel, grant_success")]
    public List<CardDeckEntry> decks = new List<CardDeckEntry>();

    private readonly Dictionary<string, List<CardData>> decksByKey = new Dictionary<string, List<CardData>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate CardDatabase found. Destroying extra instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildLookup();
    }

    private void BuildLookup()
    {
        decksByKey.Clear();
        foreach (var deck in decks)
        {
            if (deck == null || string.IsNullOrWhiteSpace(deck.key)) continue;
            string normalized = deck.key.Trim().ToLower();
            if (!decksByKey.ContainsKey(normalized))
                decksByKey.Add(normalized, deck.cards ?? new List<CardData>());
        }
    }

    private void OnValidate()
    {
        BuildLookup();
    }

    public static CardData GetRandomBySphere(string sphere)
    {
        if (Instance == null)
        {
            Debug.LogWarning("CardDatabase is not present in scene.");
            return CreateFallbackCard();
        }

        if (string.IsNullOrWhiteSpace(sphere))
            return CreateFallbackCard();

        string key = sphere.ToLower();
        if (!Instance.decksByKey.TryGetValue(key, out var deck) || deck == null || deck.Count == 0)
        {
            Debug.LogWarning($"CardDatabase: deck '{sphere}' is empty or missing.");
            return CreateFallbackCard();
        }

        return deck[Random.Range(0, deck.Count)];
    }

    private static CardData CreateFallbackCard()
    {
        return new CardData
        {
            cardType = CardType.Surprise,
            image = null,
            effects = new List<CardEffectData>()
        };
    }
}
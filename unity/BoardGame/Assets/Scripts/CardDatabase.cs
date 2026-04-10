using UnityEngine;
using System.Collections.Generic;

public enum CardEffect
{
    None,
    SkipNextTurn,
    GainStat,
    LoseStat,
    GainMoney,
    LoseMoney,
    GainSuccess,
}

[System.Serializable]
public class CardEffectData
{
    public CardEffect effect;
    public string statName;
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

    public static CardData GetByType(string sphere, CardType type)
    {
        if (Instance == null) return null;

        if (string.IsNullOrWhiteSpace(sphere))
            return null;

        string key = sphere.ToLower();
        if (!Instance.decksByKey.TryGetValue(key, out var deck) || deck == null || deck.Count == 0)
            return null;

        var filtered = deck.FindAll(c => c != null && c.cardType == type);
        if (filtered.Count == 0) return null;
        return filtered[Random.Range(0, filtered.Count)];
    }

    public static List<CardEffectData> GetEffects(CardData card, bool playWithPartner)
    {
        if (card == null) return new List<CardEffectData>();
        if (card.cardType != CardType.Green) return card.effects;
        return playWithPartner ? card.coopEffects : card.soloEffects;
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
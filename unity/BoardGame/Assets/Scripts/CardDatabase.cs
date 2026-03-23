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
    public string statName;  // пусто = основная сфера клетки
    public int amount;
}

[System.Serializable]
public class CardData
{
    public string title;
    public string description;
    public CardType cardType;
    public List<CardEffectData> effects = new List<CardEffectData>();
}

public static class CardDatabase
{
    private static readonly Dictionary<string, List<CardData>> BySphere = new Dictionary<string, List<CardData>>
    {
        { "science", new List<CardData> {
            new CardData {
                title = "SCIENCE FAIR",
                description = "You presented your research.\n+2 Science",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "LAB ACCIDENT",
                description = "Experiment went wrong.\n-1 Science, +1 Experience",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 },
                    new CardEffectData { effect = CardEffect.GainStat, statName = "experience", amount = 1 }
                }
            },
            new CardData {
                title = "CONFERENCE TALK",
                description = "Roll vs exp — win: +2 Science, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "PUBLISHED PAPER",
                description = "If Science >= 5: +3 Science, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "JOINT RESEARCH",
                description = "Collaborate on a study.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },       // лидер: +3 основная
                    new CardEffectData { effect = CardEffect.GainStat, statName = "experience", amount = 2 } // партнёр: +2 experience
                }
            },
            new CardData {
                title = "LUCKY GRANT",
                description = "Unexpected funding arrived!\n+3 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 3 }
                }
            },
        }},

        { "art", new List<CardData> {
            new CardData {
                title = "GALLERY SHOW",
                description = "Your work was displayed publicly.\n+2 Art",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "CREATIVE BLOCK",
                description = "Inspiration ran dry.\n-1 Art",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 }
                }
            },
            new CardData {
                title = "ART COMPETITION",
                description = "Roll vs exp — win: +2 Art, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "VIRAL ARTWORK",
                description = "If Art >= 5: +3 Art, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "COLLAB EXHIBITION",
                description = "Create together with another artist.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },    // лидер: +3 Art
                    new CardEffectData { effect = CardEffect.GainStat, statName = "media", amount = 2 } // партнёр: +2 Media
                }
            },
            new CardData {
                title = "SOLD A PIECE",
                description = "Someone bought your work!\n+4 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 4 }
                }
            },
        }},

        { "business", new List<CardData> {
            new CardData {
                title = "SUCCESSFUL DEAL",
                description = "Closed a profitable contract.\n+2 Business, +2 Money",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 },
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 2 }
                }
            },
            new CardData {
                title = "FAILED PITCH",
                description = "Investors said no.\n-1 Business",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 }
                }
            },
            new CardData {
                title = "PITCH CONTEST",
                description = "Roll vs exp — win: +2 Business, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "STARTUP LAUNCH",
                description = "If Business >= 5: +3 Business, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "BUSINESS PARTNERSHIP",
                description = "Form a strategic alliance.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },   // лидер: +3 Business
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 3 }                  // партнёр: +3 Money
                }
            },
            new CardData {
                title = "TAX RETURN",
                description = "Unexpected refund!\n+5 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 5 }
                }
            },
        }},

        { "sport", new List<CardData> {
            new CardData {
                title = "TRAINING CAMP",
                description = "Intensive week of practice.\n+2 Sport",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "INJURY",
                description = "You got hurt during training.\n-1 Sport, skip next turn",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 },
                    new CardEffectData { effect = CardEffect.SkipNextTurn, amount = 1 }
                }
            },
            new CardData {
                title = "LOCAL TOURNAMENT",
                description = "Roll vs exp — win: +2 Sport, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "CHAMPIONSHIP",
                description = "If Sport >= 5: +3 Sport, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "TEAM MATCH",
                description = "Play together to win.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },      // лидер: +3 Sport
                    new CardEffectData { effect = CardEffect.GainStat, statName = "volounteer", amount = 2 } // партнёр: +2 Volunteer
                }
            },
            new CardData {
                title = "SPONSORSHIP",
                description = "A brand noticed you!\n+4 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 4 }
                }
            },
        }},

        { "media", new List<CardData> {
            new CardData {
                title = "VIRAL POST",
                description = "Your content blew up.\n+2 Media",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "BAD REVIEW",
                description = "Public backlash.\n-1 Media",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 }
                }
            },
            new CardData {
                title = "INTERVIEW",
                description = "Roll vs exp — win: +2 Media, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "TV APPEARANCE",
                description = "If Media >= 5: +3 Media, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "MEDIA COLLAB",
                description = "Co-create content with another player.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },  // лидер: +3 Media
                    new CardEffectData { effect = CardEffect.GainStat, statName = "art", amount = 2 } // партнёр: +2 Art
                }
            },
            new CardData {
                title = "AD REVENUE",
                description = "Your channel earned money!\n+3 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 3 }
                }
            },
        }},

        { "volounteer", new List<CardData> {
            new CardData {
                title = "COMMUNITY EVENT",
                description = "You organized a local event.\n+2 Volunteer",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "LOW TURNOUT",
                description = "Nobody showed up.\n-1 Volunteer",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 }
                }
            },
            new CardData {
                title = "CHARITY RUN",
                description = "Roll vs exp — win: +2 Volunteer, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "AWARD FOR SERVICE",
                description = "If Volunteer >= 5: +3 Volunteer, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "VOLUNTEER SQUAD",
                description = "Lead a team of volunteers.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },      // лидер: +3 Volunteer
                    new CardEffectData { effect = CardEffect.GainStat, statName = "tourism", amount = 2 } // партнёр: +2 Tourism
                }
            },
            new CardData {
                title = "DONATION RECEIVED",
                description = "Someone supported your cause!\n+2 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 2 }
                }
            },
        }},

        { "tourism", new List<CardData> {
            new CardData {
                title = "GUIDED TOUR",
                description = "You led an amazing tour.\n+2 Tourism",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "TRIP CANCELLED",
                description = "Bad weather ruined the trip.\n-1 Tourism",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 }
                }
            },
            new CardData {
                title = "TRAVEL BLOG",
                description = "Roll vs exp — win: +2 Tourism, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "TRAVEL AWARD",
                description = "If Tourism >= 5: +3 Tourism, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "TRAVEL AGENCY",
                description = "Launch a travel project together.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },      // лидер: +3 Tourism
                    new CardEffectData { effect = CardEffect.GainStat, statName = "business", amount = 2 } // партнёр: +2 Business
                }
            },
            new CardData {
                title = "FOUND WALLET",
                description = "Someone left money behind!\n+3 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 3 }
                }
            },
        }},

        { "it", new List<CardData> {
            new CardData {
                title = "APP RELEASED",
                description = "You shipped a feature.\n+2 IT",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 2 }
                }
            },
            new CardData {
                title = "BUG IN PROD",
                description = "Critical bug hit production.\n-1 IT",
                cardType = CardType.Yellow,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.LoseStat, statName = "", amount = 1 }
                }
            },
            new CardData {
                title = "HACKATHON",
                description = "Roll vs exp — win: +2 IT, lose: +1 Exp",
                cardType = CardType.Blue,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "PRODUCT LAUNCH",
                description = "If IT >= 5: +3 IT, +1 Success\nElse: +1 Experience",
                cardType = CardType.Red,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.None }
                }
            },
            new CardData {
                title = "OPEN SOURCE PROJECT",
                description = "Build something together.",
                cardType = CardType.Green,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainStat, statName = "", amount = 3 },       // лидер: +3 IT
                    new CardEffectData { effect = CardEffect.GainStat, statName = "science", amount = 2 }  // партнёр: +2 Science
                }
            },
            new CardData {
                title = "FREELANCE PAYMENT",
                description = "Client paid on time!\n+4 Money",
                cardType = CardType.Surprise,
                effects = new List<CardEffectData> {
                    new CardEffectData { effect = CardEffect.GainMoney, amount = 4 }
                }
            },
        }},
    };

    // Получить случайную карточку по сфере
    public static CardData GetRandomBySphere(string sphere)
    {
        string key = sphere.ToLower();
        if (!BySphere.ContainsKey(key))
        {
            Debug.LogWarning($"CardDatabase: сфера '{sphere}' не найдена!");
            return new CardData { title = "UNKNOWN", description = "No card found.", cardType = CardType.Surprise, effects = new List<CardEffectData>() };
        }
        var deck = BySphere[key];
        return deck[Random.Range(0, deck.Count)];
    }

    // Получить карточку по сфере и конкретному типу
    public static CardData GetByType(string sphere, CardType type)
    {
        string key = sphere.ToLower();
        if (!BySphere.ContainsKey(key)) return null;
        var filtered = BySphere[key].FindAll(c => c.cardType == type);
        if (filtered.Count == 0) return null;
        return filtered[Random.Range(0, filtered.Count)];
    }
}
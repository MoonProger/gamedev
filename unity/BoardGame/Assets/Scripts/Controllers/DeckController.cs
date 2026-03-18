using UnityEngine;

public class DeckManager : MonoBehaviour
{
    [Header("Sphere Decks")]
    public CardVisual[] sphereDecks = new CardVisual[8];  // science, art, business, sport, media, volunteer, tourism, it

    [Header("Special Decks")]
    public CardVisual travelDeck;
    public CardVisual grantDeck;

    public CardVisual GetCardForNode(BoardNode.NodeType nodeType, string sphere = "")
    {
        return nodeType switch
        {
            BoardNode.NodeType.Science    => sphereDecks[0],
            BoardNode.NodeType.Art        => sphereDecks[1],
            BoardNode.NodeType.Business   => sphereDecks[2],
            BoardNode.NodeType.Sport      => sphereDecks[3],
            BoardNode.NodeType.Media      => sphereDecks[4],
            BoardNode.NodeType.Volounteer => sphereDecks[5],
            BoardNode.NodeType.Tourism    => sphereDecks[6],
            BoardNode.NodeType.IT         => sphereDecks[7],
            BoardNode.NodeType.Travel     => travelDeck,
            BoardNode.NodeType.Grant      => grantDeck,
            _ => null
        };
    }
}
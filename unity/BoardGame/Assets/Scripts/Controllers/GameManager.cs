using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CardResult
{
    public string title;
    public string description;
}

public enum CardType { Surprise, Yellow, Blue, Red, Green }

public class GameManager : MonoBehaviour
{
    [Header("Setup")]
    public List<PlayerController> players;
    public BoardNode startNode;
    public BoardNode moneyNode;
    public DiceController dice;
    public UIManager uiManager;

    [Header("Settings")]
    public float jumpHeight = 5;
    public float jumpDuration = 0.7f;
    public float stepDelay = 0.25f;

    private List<BoardNode> clickableNodes = new List<BoardNode>();
    private int currentPlayerIndex = 0;
    private int lastRoll;
    private bool isMoving = false;

    public int expectedPlayerCount = 0;
    private List<string> playerNames = new List<string>();
    private List<string> playerIds = new List<string>();

    [Header("Turn State")]
    private bool hasRolledThisTurn = false;

    private void Awake()
    {
        dice.OnDiceRolled += RegisterRoll;
    }

    private void Start()
    {
        if (expectedPlayerCount == 0)
        {
            Debug.Log("⚠️ Test mode: simulating React data");
            SetPlayerCount(4);
            SetPlayerName("Player 1");
            SetPlayerName("Player 2");
            SetPlayerName("Player 3");
            SetPlayerName("Player 4");
        }
    }

    private void OnDestroy()
    {
        dice.OnDiceRolled -= RegisterRoll;
    }

    private void Update()
    {
        HandleSelectionInput();
    }

    public void SetPlayerCount(int count)
    {
        expectedPlayerCount = count;
        Debug.Log($"🎮 React: expecting players: {count}");
    }

    public void SetPlayerName(string name)
    {
        playerNames.Add(name);
        Debug.Log($"👤 React: received player: {name}");

        if (playerNames.Count == expectedPlayerCount)
        {
            InitializeGameFromReact();
        }
    }

    public void SetPlayerId(string id)
    {
        playerIds.Add(id);
        Debug.Log($"🆔 React: received ID: {id}");
    }

    private void InitializeGameFromReact()
    {
        Debug.Log($"🎲 Initializing game with {expectedPlayerCount} players");

        for (int i = 0; i < players.Count; i++)
        {
            if (i < expectedPlayerCount)
            {
                players[i].gameObject.SetActive(true);
                Debug.Log($"✅ Activated player {i + 1}: {playerNames[i]}");
            }
            else
            {
                players[i].gameObject.SetActive(false);
                Debug.Log($"⭕ Disabled extra token {i + 1}");
            }
        }

        TableManager tm = Object.FindFirstObjectByType<TableManager>();
        if (tm != null)
        {
            tm.InitializeTable();
        }

        InitializePlayers();
    }

    private void InitializePlayers()
    {
        for (int i = 0; i < expectedPlayerCount; i++)
        {
            int previousIndex = currentPlayerIndex;
            currentPlayerIndex = i;

            players[i].RandomizeStats();

            Vector3 spawnPos = GetOffsetPosition(startNode);
            players[i].TeleportToNode(startNode);
            players[i].transform.position = spawnPos;

            currentPlayerIndex = previousIndex;
        }

        UpdatePlayersVisuals();
        Debug.Log("🎯 Game is ready to start");
    }

    public void TryRollDice()
    {
        if (isMoving)
        {
            Debug.Log("🚫 Cannot roll dice while a token is moving");
            return;
        }

        if (hasRolledThisTurn)
        {
            Debug.Log("🚫 Dice already rolled this turn. Choose a destination.");
            return;
        }

        dice.RollDice();
    }

    private void RegisterRoll(int result)
    {
        lastRoll = result;
        hasRolledThisTurn = true;
        Debug.Log($"🎲 Dice result: {lastRoll}. Rolling locked until turn ends.");
        ShowPossibleMoves(lastRoll);
    }

    public void ShowPossibleMoves(int rollResult)
    {
        if (isMoving) return;

        clickableNodes = GetPossibleDestinations(players[currentPlayerIndex].currentNode, rollResult);

        foreach (var node in clickableNodes)
            node.SetHighlight(true);
    }

    private void HandleSelectionInput()
    {
        if (Input.GetMouseButtonDown(0) && clickableNodes.Count > 0 && !isMoving)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                BoardNode target = hit.collider.GetComponent<BoardNode>();
                if (target != null && clickableNodes.Contains(target))
                {
                    StartCoroutine(MoveSequence(target, lastRoll));
                }
            }
        }
    }

    private IEnumerator MoveSequence(BoardNode target, int totalRoll)
    {
        isMoving = true;

        List<BoardNode> path = GetPathToTarget(players[currentPlayerIndex].currentNode, target, totalRoll);

        foreach (var node in clickableNodes) node.SetHighlight(false);
        clickableNodes.Clear();

        PlayerController currentPlayer = players[currentPlayerIndex];

        if (path != null)
        {
            foreach (BoardNode stepNode in path)
            {
                Vector3 targetPos = (stepNode == target) ? GetOffsetPosition(target) : stepNode.transform.position;

                yield return StartCoroutine(JumpToNode(currentPlayer, targetPos));

                currentPlayer.currentNode = stepNode;
                yield return new WaitForSeconds(stepDelay);
            }
        }

        if (currentPlayer.currentNode == moneyNode)
        {
            currentPlayer.ChangeStat("money", 1);
            Debug.Log($"💰 {currentPlayer.playerName} gained +1 money for stopping on the money tile");
        }

        TableManager table = FindObjectOfType<TableManager>();

        yield return new WaitForSeconds(0.5f);

        PullCard(currentPlayer);

        table.UpdateTablePositions();

        isMoving = false;
        hasRolledThisTurn = false;
        currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;

        UpdatePlayersVisuals();
    }

    private void PullCard(PlayerController player)
    {
        BoardNode currentNode = player.currentNode;

        if (currentNode.nodeStat == BoardNode.NodeType.None) return;

        CardType cardType = CardType.Blue;

        CardResult result = new CardResult();

        string statName = currentNode.nodeStat.ToString().ToLower();
        int currentStatLevel = player.GetStatValue(statName);
        int expLevel = player.GetStatValue("experience");

        switch (cardType)
        {
            case CardType.Surprise:
                result.title = "SURPRISE";
                result.description = "Something unusual happened...";
                break;

            case CardType.Yellow:
                player.ChangeStat(statName, 1);
                result.title = "YELLOW CARD";
                result.description = $"Regular training! {currentNode.nodeStat} +1";
                break;

            case CardType.Blue:
                int diceSum = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);

                if (expLevel > diceSum)
                {
                    player.ChangeStat(statName, 1);
                    result.title = "BLUE CARD (SUCCESS)";
                    result.description = $"Roll: {diceSum} > Your level: {expLevel}\n{currentNode.nodeStat} +1";
                }
                else
                {
                    result.title = "BLUE CARD (EXPERIENCE)";
                    result.description = $"Roll: {diceSum} <= Your level: {currentStatLevel}\nYou gain experience!";
                    player.ChangeStat("experience", 1);
                }
                break;

            case CardType.Red:
                result.title = "RED CARD";
                result.description = "Coming soon...";
                break;

            case CardType.Green:
                result.title = "GREEN CARD";
                result.description = "Coming soon...";
                break;
        }

        if (uiManager != null) uiManager.ShowCard(result);
    }

    private IEnumerator JumpToNode(PlayerController p, Vector3 targetPos)
    {
        float elapsed = 0;
        Vector3 startPos = p.transform.position;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / jumpDuration;

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, percent);

            float heightOffset = jumpHeight * 4f * percent * (1f - percent);

            p.transform.position = new Vector3(currentPos.x, currentPos.y + heightOffset, currentPos.z);

            yield return null;
        }

        p.PlayJumpSound();
        p.transform.position = targetPos;
    }

    private List<BoardNode> GetPossibleDestinations(BoardNode start, int moves)
    {
        List<BoardNode> result = new List<BoardNode>();
        List<BoardNode> visitedNodes = new List<BoardNode> { start };

        if (start == moneyNode)
        {
            result.Add(start);
        }

        FindPathsRecursive(start, moves, new List<string>(), visitedNodes, result);

        return result;
    }

    private void FindPathsRecursive(BoardNode current, int movesLeft, List<string> visitedEdges, List<BoardNode> visitedNodes, List<BoardNode> result)
    {
        if (movesLeft == 0)
        {
            if (!result.Contains(current)) result.Add(current);
            return;
        }

        foreach (var next in current.neighbors)
        {
            string edgeId = GetEdgeId(current, next);

            if (!visitedEdges.Contains(edgeId) && !visitedNodes.Contains(next))
            {
                List<string> nextVisitedEdges = new List<string>(visitedEdges) { edgeId };
                List<BoardNode> nextVisitedNodes = new List<BoardNode>(visitedNodes) { next };

                FindPathsRecursive(next, movesLeft - 1, nextVisitedEdges, nextVisitedNodes, result);
            }
        }
    }

    private List<BoardNode> GetPathToTarget(BoardNode start, BoardNode target, int maxSteps)
    {
        if (start == target && start == moneyNode) return new List<BoardNode>();

        var queue = new Queue<(List<BoardNode> nodes, List<string> edges)>();
        queue.Enqueue((new List<BoardNode> { start }, new List<string>()));

        while (queue.Count > 0)
        {
            var (path, visitedEdges) = queue.Dequeue();
            BoardNode lastNode = path[path.Count - 1];

            if (lastNode == target && path.Count - 1 == maxSteps)
            {
                path.RemoveAt(0);
                return path;
            }

            if (path.Count - 1 < maxSteps)
            {
                foreach (BoardNode neighbor in lastNode.neighbors)
                {
                    string edgeId = GetEdgeId(lastNode, neighbor);

                    if (!visitedEdges.Contains(edgeId) && !path.Contains(neighbor))
                    {
                        var newPath = new List<BoardNode>(path) { neighbor };
                        var newVisitedEdges = new List<string>(visitedEdges) { edgeId };

                        queue.Enqueue((newPath, newVisitedEdges));
                    }
                }
            }
        }

        return null;
    }

    private string GetEdgeId(BoardNode a, BoardNode b)
    {
        return string.Compare(a.name, b.name) < 0 ? a.name + b.name : b.name + a.name;
    }

    private void UpdatePlayersVisuals()
    {
        for (int i = 0; i < expectedPlayerCount; i++)
        {
            players[i].SetTransparency(i != currentPlayerIndex);
        }

        if (uiManager != null)
        {
            uiManager.UpdateAllStats(players[currentPlayerIndex]);
        }
    }

    private Vector3 GetOffsetPosition(BoardNode node)
    {
        int playersOnNode = 0;

        for (int i = 0; i < expectedPlayerCount; i++)
        {
            if (players[i].currentNode == node && i != currentPlayerIndex)
                playersOnNode++;
        }

        if (playersOnNode == 0) return node.transform.position;

        float angle = playersOnNode * 90f * Mathf.Deg2Rad;
        float radius = 5f;

        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

        return node.transform.position + offset;
    }
}
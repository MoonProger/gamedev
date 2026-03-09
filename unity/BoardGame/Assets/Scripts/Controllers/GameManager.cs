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

        CardType cardType = CardType.Green;

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
    
    Debug.Log($"{player.playerName} draws RED CARD on {currentNode.nodeStat}. Current level: {currentStatLevel}");
    
    if (currentStatLevel >= 5)
    {
        int bonusPoints = UnityEngine.Random.Range(1, 4); // Points on card (1-3)
        player.ChangeStat(statName, bonusPoints);
        player.ChangeStat("success", 1);
        
        result.description = $"Your {currentNode.nodeStat} level: {currentStatLevel} ≥ 5\n" +
                           $"Gain {bonusPoints} {currentNode.nodeStat} points +1 Success!";
        
        Debug.Log($"{player.playerName} SUCCESS: +{bonusPoints} {statName}, +1 success. Total success: {player.GetStatValue("success")}");
    }
    else
    {
        player.ChangeStat("experience", 1);
        result.description = $"Your {currentNode.nodeStat} level: {currentStatLevel} < 5\n" +
                           $"Gain +1 Experience (need to level up past 5)";
        
        Debug.Log($"{player.playerName} NEEDS LEVEL: +1 experience. Total exp: {player.GetStatValue("experience")}");
    }
    break;

           case CardType.Green:
    result.title = "GREEN CARD - JOINT PROJECT";
    
    string mainStatName = currentNode.nodeStat.ToString().ToLower(); // Сфера, на которой стоим
    string partnerStatName = GetRandomOtherStat(mainStatName); // Случайная другая сфера
    PlayerController mainPlayer = player;
    
    Debug.Log($"{mainPlayer.playerName} draws GREEN CARD: {mainStatName} + {partnerStatName}");
    
    // Считаем уровни всех игроков в mainStatName
    int mainPlayerLevel = mainPlayer.GetStatValue(mainStatName);
    List<(PlayerController p, int level)> otherPlayersLevels = new List<(PlayerController, int)>();
    
    for (int i = 0; i < expectedPlayerCount; i++)
    {
        if (players[i] != mainPlayer)
        {
            int otherLevel = players[i].GetStatValue(mainStatName);
            otherPlayersLevels.Add((players[i], otherLevel));
            Debug.Log($"  {players[i].playerName} {mainStatName}: {otherLevel}");
        }
    }
    
    // Сортируем по убыванию уровня
    otherPlayersLevels.Sort((a, b) => b.level.CompareTo(a.level));
    
    // Находим максимальный уровень среди других
    int maxOtherLevel = otherPlayersLevels.Count > 0 ? otherPlayersLevels[0].level : 0;
    
    if (mainPlayerLevel > maxOtherLevel)
    {
        // 3) Делаем сами, забираем ВСЕ бонусы
        int mainBonus = UnityEngine.Random.Range(2, 5); // Больше бонусов за лидерство
        int partnerBonus = UnityEngine.Random.Range(1, 3);
        
        mainPlayer.ChangeStat(mainStatName, mainBonus);
        mainPlayer.ChangeStat(partnerStatName, partnerBonus);
        
        result.description = $"YOUR {mainStatName.ToUpper()} level {mainPlayerLevel} > others!\n" +
                           $"Solo project: +{mainBonus} {mainStatName}, +{partnerBonus} {partnerStatName}";
        
        Debug.Log($"{mainPlayer.playerName} SOLO WIN: +{mainBonus} {mainStatName}, +{partnerBonus} {partnerStatName}");
    }
    else if (mainPlayerLevel == maxOtherLevel && otherPlayersLevels.Count > 0)
    {
        // 4) Равенство с лидерами - выбор (пока автоматизируем: выбираем самого сильного)
        var leader = otherPlayersLevels[0].p;
        int leaderBonus = UnityEngine.Random.Range(1, 3);
        int partnerBonus = UnityEngine.Random.Range(1, 3);
        
        // Автоматический выбор: делаем с лидером (можно потом UI)
        mainPlayer.ChangeStat(partnerStatName, partnerBonus);
        leader.ChangeStat(mainStatName, leaderBonus);
        
        result.description = $"TIE with {leader.playerName}! Joint project.\n" +
                           $"You get +{partnerBonus} {partnerStatName}\n" +
                           $"{leader.playerName} gets +{leaderBonus} {mainStatName}";
        
        Debug.Log($"{mainPlayer.playerName} & {leader.playerName} TIE PROJECT: You +{partnerBonus} {partnerStatName}, They +{leaderBonus} {mainStatName}");
    }
    else
    {
        // 5) Наш уровень меньше - ищем лидера и делимся
        if (otherPlayersLevels.Count > 0)
        {
            var leader = otherPlayersLevels[0].p; // Самый сильный
            int leaderBonus = UnityEngine.Random.Range(2, 4);
            int partnerBonus = UnityEngine.Random.Range(1, 2);
            
            mainPlayer.ChangeStat(partnerStatName, partnerBonus);
            leader.ChangeStat(mainStatName, leaderBonus);
            
            result.description = $"Need partner! With {leader.playerName}.\n" +
                               $"You get +{partnerBonus} {partnerStatName}\n" +
                               $"{leader.playerName} gets +{leaderBonus} {mainStatName}";
            
            Debug.Log($"{mainPlayer.playerName} NEEDS HELP from {leader.playerName}: You +{partnerBonus} {partnerStatName}, They +{leaderBonus} {mainStatName}");
        }
        else
        {
            // Никого нет - просто бонус партнёра
            int partnerBonus = UnityEngine.Random.Range(1, 3);
            mainPlayer.ChangeStat(partnerStatName, partnerBonus);
            result.description = $"Solo +{partnerBonus} {partnerStatName} (no competition)";
        }
    }
    break;
        }

        if (uiManager != null) uiManager.ShowCard(result);
    }


    private string GetRandomOtherStat(string excludeStat)
{
    string[] allStats = { "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };
    List<string> available = new List<string>();
    
    foreach (string stat in allStats)
    {
        if (stat != excludeStat) available.Add(stat);
    }
    
    return available[UnityEngine.Random.Range(0, available.Count)];
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
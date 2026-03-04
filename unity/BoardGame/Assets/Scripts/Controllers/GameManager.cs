using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CardResult
{
    public string title;
    public string description;
    public bool isSuccess;
}

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

    // 👇 НОВОЕ: для получения данных из React
    private int expectedPlayerCount = 0;
    private List<string> playerNames = new List<string>();
    private List<string> playerIds = new List<string>();

    [Header("Turn State")]
    private bool hasRolledThisTurn = false; // Флаг: бросал ли текущий игрок кубик в этом ходу

    #region LifeCycle

    private void Awake()
    {
        dice.OnDiceRolled += RegisterRoll;
    }

    private void Start()
    {
        // Убираем InitializePlayers() отсюда - теперь будем ждать данные из React
        // InitializePlayers();
        if (expectedPlayerCount == 0) 
    {
        Debug.Log("⚠️ Тестовый запуск: имитируем данные из React");
        SetPlayerCount(3); // Ставим 4 игрока для теста
        SetPlayerName("Игрок 1");
        SetPlayerName("Игрок 2");
        SetPlayerName("Игрок 3");
        SetPlayerName("Игрок 4");
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

    #endregion

    #region React Communication Methods 👈 НОВЫЕ МЕТОДЫ

    // Эти методы будут вызываться из React
    public void SetPlayerCount(int count)
    {
        expectedPlayerCount = count;
        Debug.Log($"🎮 React: ожидаем игроков: {count}");
    }

    public void SetPlayerName(string name)
    {
        playerNames.Add(name);
        Debug.Log($"👤 React: получен игрок: {name}");
        
        // Когда все игроки получены - инициализируем игру
        if (playerNames.Count == expectedPlayerCount)
        {
            InitializeGameFromReact();
        }
    }

    public void SetPlayerId(string id)
    {
        playerIds.Add(id);
        Debug.Log($"🆔 React: получен ID: {id}");
    }

    private void InitializeGameFromReact()
    {
        Debug.Log($"🎲 Инициализация игры с {expectedPlayerCount} игроками");
        
        // Активируем только нужное количество игроков
        for (int i = 0; i < players.Count; i++)
        {
            if (i < expectedPlayerCount)
            {
                // Включаем фишку и даём ей имя
                players[i].gameObject.SetActive(true);
                
                // Если у PlayerController есть поле для имени, можно его заполнить
                // players[i].playerName = playerNames[i];
                
                Debug.Log($"✅ Активирован игрок {i+1}: {playerNames[i]}");
            }
            else
            {
                // Отключаем лишние фишки
                players[i].gameObject.SetActive(false);
                Debug.Log($"⭕ Отключена лишняя фишка {i+1}");
            }
        }
        
        // Теперь инициализируем активных игроков
        InitializePlayers();
    }

    #endregion

    #region Initialization

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
        
        Debug.Log("🎯 Игра готова к началу!");
    }

    #endregion

    #region Turn Logic

// Метод, который нужно вызывать при нажатии на кубик/кнопку
public void TryRollDice()
{
    // 1. Если фишка уже движется — игнорируем
    if (isMoving) 
    {
        Debug.Log("🚫 Нельзя бросать кубик во время движения!");
        return;
    }

    // 2. Если в этом ходу кубик уже был брошен — игнорируем
    if (hasRolledThisTurn)
    {
        Debug.Log("🚫 Вы уже бросили кубик в этом ходу! Выберите клетку для хода.");
        return;
    }

    // 3. Если всё ок — запускаем физический бросок в DiceController
    dice.RollDice();
}

private void RegisterRoll(int result)
{
    lastRoll = result;
    hasRolledThisTurn = true; // Блокируем повторный бросок
    Debug.Log($"🎲 Dice Result: {lastRoll}. Бросок заблокирован до конца хода.");
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

    #endregion

    #region Movement

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
        Debug.Log($"💰 {currentPlayer.playerName} получил +1 за остановку на поле денег");
    }

      yield return new WaitForSeconds(0.5f); // Небольшая пауза перед карточкой
    PullCard(currentPlayer);

    isMoving = false;
    hasRolledThisTurn = false;
    currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
    UpdatePlayersVisuals();
}

private void PullCard(PlayerController player)
{
    BoardNode currentNode = player.currentNode;
    
    // Если поле "пустое" (None) и это не поле с деньгами, ничего не делаем
    if (currentNode.nodeStat == BoardNode.NodeType.None) return;

    int randomChance = UnityEngine.Random.Range(0, 4); 
    CardResult result = new CardResult();

    if (randomChance < 3) // 75% шанс успеха
    {
        // Получаем название стата из настроек поля
        string statName = currentNode.nodeStat.ToString().ToLower();
        
        // Применяем бонус
        player.ChangeStat(statName, 1);
        
        result.title = "train";
        result.description = $"lucky! {currentNode.nodeStat} +1";
        result.isSuccess = true;
    }
    else
    {
        result.title = "unluck";
        result.description = "meh.";
        result.isSuccess = false;
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

    #endregion

    #region Pathfinding

    private List<BoardNode> GetPossibleDestinations(BoardNode start, int moves)
{
    List<BoardNode> result = new List<BoardNode>();
    // Передаем стартовый узел в список посещенных, чтобы нельзя было на него вернуться
    List<BoardNode> visitedNodes = new List<BoardNode> { start };
    
    // ИСКЛЮЧЕНИЕ: Если мы стоим на поле с деньгами, мы можем никуда не идти (пропустить ход)
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
        
        // Условие: линия еще не пройдена И узел еще не посещен
        // (Либо этот узел - поле с деньгами, тогда правила мягче, но по твоей логике 
        // запрет обычно касается всех узлов, чтобы не было "петель")
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
    // Если игрок решил остаться на поле с деньгами (0 шагов)
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
                // Проверяем, что не идем по той же линии и не заходим в уже посещенный узел
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
    // Генерирует уникальную строку для пары узлов, чтобы путь A->B и B->A считался одной и той же линией
    return string.Compare(a.name, b.name) < 0 ? a.name + b.name : b.name + a.name;
}

    #endregion

    #region Visuals & Offsets

    private void UpdatePlayersVisuals()
    {
        for (int i = 0; i < expectedPlayerCount; i++) // Используем expectedPlayerCount
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
        for (int i = 0; i < expectedPlayerCount; i++) // Используем expectedPlayerCount
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

    #endregion
}
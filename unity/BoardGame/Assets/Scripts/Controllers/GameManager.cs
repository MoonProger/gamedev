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

    [Header("Card Visual")]
public CardVisual cardVisual;

    [Header("Audio")]
    public AudioClip victorySound;
    private AudioSource audioSource;

    [Header("UI")]
    public GreenCardUI greenCardUI;

    [Header("Setup")]
    public List<PlayerController> players;
    public BoardNode startNode;
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
    private bool hasRolledThisTurn = false;

    public int expectedPlayerCount = 0;
    private List<string> playerNames = new List<string>();
    private List<string> playerIds = new List<string>();

    private static readonly string[] allStats = { "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };

    private void Awake()
{
    dice.OnDiceRolled += RegisterRoll;
    audioSource = GetComponent<AudioSource>();
    if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
}
    private void OnDestroy() => dice.OnDiceRolled -= RegisterRoll;
    private void Update() => HandleSelectionInput();

    private void Start()
    {
        if (expectedPlayerCount == 0)
        {
            Debug.Log("Test mode: simulating React data");
            SetPlayerCount(4);
            SetPlayerName("Player 1");
            SetPlayerName("Player 2");
            SetPlayerName("Player 3");
            SetPlayerName("Player 4");
        }
    }

    public void SetPlayerCount(int count)
    {
        expectedPlayerCount = count;
        Debug.Log($"Expecting {count} players");
    }

    public void SetPlayerName(string name)
    {
        playerNames.Add(name);
        Debug.Log($"Received player: {name}");
        if (playerNames.Count == expectedPlayerCount)
            InitializeGameFromReact();
    }

    public void SetPlayerId(string id)
    {
        playerIds.Add(id);
        Debug.Log($"Received player ID: {id}");
    }

    private void InitializeGameFromReact()
    {
        Debug.Log($"Initializing game with {expectedPlayerCount} players");

        for (int i = 0; i < players.Count; i++)
        {
            players[i].gameObject.SetActive(i < expectedPlayerCount);
            Debug.Log(i < expectedPlayerCount
                ? $"Activated player {i + 1}: {playerNames[i]}"
                : $"Disabled extra token {i + 1}");
        }

        TableManager tm = Object.FindFirstObjectByType<TableManager>();
        tm?.InitializeTable();

        InitializePlayers();
    }

    private void InitializePlayers()
    {
        for (int i = 0; i < expectedPlayerCount; i++)
        {
            int prev = currentPlayerIndex;
            currentPlayerIndex = i;
            players[i].RandomizeStats();
            players[i].TeleportToNode(startNode);
            players[i].transform.position = GetOffsetPosition(startNode);
            currentPlayerIndex = prev;
        }

        UpdatePlayersVisuals();
        Debug.Log("Game is ready to start");
    }

    public void TryRollDice()
    {
        if (isMoving) { Debug.Log("Cannot roll while token is moving"); return; }
        if (hasRolledThisTurn) { Debug.Log("Already rolled this turn. Choose a destination."); return; }
         // Проверка пропуска хода
    if (players[currentPlayerIndex].skipTurns > 0)
    {
        players[currentPlayerIndex].skipTurns--;
        Debug.Log($"{players[currentPlayerIndex].playerName} skips turn ({players[currentPlayerIndex].skipTurns} left)");
        cardVisual?.ShowRaw(
    "TURN SKIPPED",
    "You must skip this turn.",
    CardType.Surprise,
    "none"
);
        hasRolledThisTurn = true; // чтобы конец хода отработал
        StartCoroutine(EndTurnAfterDelay());
        return;
    }
        dice.RollDice();
    }
private IEnumerator EndTurnAfterDelay()
{
    yield return new WaitForSeconds(2f);
    hasRolledThisTurn = false;
    currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
    UpdatePlayersVisuals();
}
    private void RegisterRoll(int result)
    {
        lastRoll = result;
        hasRolledThisTurn = true;
        Debug.Log($"Dice result: {lastRoll}");
        ShowPossibleMoves(lastRoll);
    }

    public void ShowPossibleMoves(int rollResult)
    {
        if (isMoving) return;
        clickableNodes = GetPossibleDestinations(players[currentPlayerIndex].currentNode, rollResult);
        foreach (var node in clickableNodes) node.SetHighlight(true);
    }

    private void HandleSelectionInput()
    {
        if (!Input.GetMouseButtonDown(0) || clickableNodes.Count == 0 || isMoving) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            BoardNode target = hit.collider.GetComponent<BoardNode>();
            if (target != null && clickableNodes.Contains(target))
                StartCoroutine(MoveSequence(target, lastRoll));
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
                Vector3 targetPos = stepNode == target ? GetOffsetPosition(target) : stepNode.transform.position;
                yield return StartCoroutine(JumpToNode(currentPlayer, targetPos));
                currentPlayer.currentNode = stepNode;
                yield return new WaitForSeconds(stepDelay);
            }
        }

        TableManager table = FindObjectOfType<TableManager>();
        yield return new WaitForSeconds(0.5f);

        switch (currentPlayer.currentNode.nodeStat)
        {
            case BoardNode.NodeType.Money:
                currentPlayer.ChangeStat("money", 1);
                Debug.Log($"{currentPlayer.playerName} gained +1 money");
                break;
            case BoardNode.NodeType.Travel:
    if (currentPlayer.GetStatValue("money") > 0)
    {
        currentPlayer.ChangeStat("money", -1);
        Debug.Log($"{currentPlayer.playerName} paid 1 money for travel");
        yield return StartCoroutine(ChooseTravelDestination(currentPlayer));
    }
    else
    {
        Debug.Log($"{currentPlayer.playerName} has no money, staying on travel node");
        cardVisual?.ShowRaw(
    "TRAVEL — NO FUNDS",
    "Not enough money to travel. Stay here.",
    CardType.Surprise,
    "travel"
);
    }
    break;
            case BoardNode.NodeType.Grant:
    // Находим сферы где у игрока 10, и он ещё не подавал
    List<string> availableGrants = new List<string>();
    foreach (string stat in allStats)
        if (currentPlayer.GetStatValue(stat) >= 10 && !currentPlayer.appliedGrants.Contains(stat))
            availableGrants.Add(stat);

    if (availableGrants.Count == 0)
    {
       cardVisual?.ShowRaw(
    "GRANT — NOT AVAILABLE",
    "You need level 10 in at least one sphere\nto apply for a grant.",
    CardType.Surprise,
    "grant"
);
        break;
    }

    yield return StartCoroutine(TryApplyGrant(currentPlayer, availableGrants));
    break;
            case BoardNode.NodeType.Project:
            yield return StartCoroutine(TryDoProject(currentPlayer));
            break;
            case BoardNode.NodeType.None:
                break;
            default:
                yield return StartCoroutine(PullCardCoroutine(currentPlayer));
                break;
        }

        CheckVictory(currentPlayer);
        if (isMoving && hasRolledThisTurn && currentPlayer.success >= 12)
{
    table.UpdateTablePositions();
    isMoving = false;
    yield break;
}
        table.UpdateTablePositions();
        isMoving = false;
        hasRolledThisTurn = false;
        currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
        UpdatePlayersVisuals();
    }

private IEnumerator PullCardCoroutine(PlayerController player)
{
    BoardNode node = player.currentNode;
    if (node.nodeStat == BoardNode.NodeType.None) yield break;

    string statName = node.nodeStat.ToString().ToLower();
    int statLevel = player.GetStatValue(statName);
    int expLevel = player.GetStatValue("experience");

    // Тянем карточку по сфере
    CardData card = CardDatabase.GetRandomBySphere(statName);
    
string colorPrefix = card.cardType switch
{
    CardType.Yellow   => "[YELLOW] ",
    CardType.Blue     => "[BLUE] ",
    CardType.Red      => "[RED] ",
    CardType.Green    => "[GREEN] ",
    CardType.Surprise => "[SURPRISE] ",
    _ => ""
};

CardResult result = new CardResult
{
    title = colorPrefix + card.title,
    description = card.description
};

    switch (card.cardType)
    {
        case CardType.Surprise:
            foreach (var eff in card.effects)
            {
                if (eff.effect == CardEffect.GainMoney)   player.ChangeStat("money", eff.amount);
                if (eff.effect == CardEffect.LoseMoney)   player.ChangeStat("money", -eff.amount);
                if (eff.effect == CardEffect.SkipNextTurn) player.skipTurns += eff.amount;
                if (eff.effect == CardEffect.GainSuccess) player.ChangeStat("success", eff.amount);
            }
            break;

        case CardType.Yellow:
            foreach (var eff in card.effects)
            {
                string t = string.IsNullOrEmpty(eff.statName) ? statName : eff.statName;
                if (eff.effect == CardEffect.GainStat)     player.ChangeStat(t, eff.amount);
                if (eff.effect == CardEffect.LoseStat)     player.ChangeStat(t, -eff.amount);
                if (eff.effect == CardEffect.SkipNextTurn) player.skipTurns += eff.amount;
            }
            break;

        case CardType.Blue:
            int diceSum = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
            if (expLevel > diceSum)
            {
                player.ChangeStat(statName, 1);
                result.description += $"\nRoll: {diceSum} < Exp: {expLevel} — {statName} +1";
            }
            else
            {
                player.ChangeStat("experience", 1);
                result.description += $"\nRoll: {diceSum} >= Exp: {expLevel} — +1 Experience";
            }
            break;

        case CardType.Red:
            if (statLevel >= 5)
            {
                int bonus = UnityEngine.Random.Range(1, 4);
                player.ChangeStat(statName, bonus);
                player.ChangeStat("success", 1);
                result.description += $"\n{statName} lvl {statLevel} >= 5 — +{bonus} {statName}, +1 Success";
            }
            else
            {
                player.ChangeStat("experience", 1);
                result.description += $"\n{statName} lvl {statLevel} < 5 — +1 Experience";
            }
            break;

        case CardType.Green:
            int myLevel = player.GetStatValue(statName);
            var others = new List<(PlayerController p, int level)>();
            for (int i = 0; i < expectedPlayerCount; i++)
                if (players[i] != player)
                    others.Add((players[i], players[i].GetStatValue(statName)));
            others.Sort((a, b) => b.level.CompareTo(a.level));
            int maxLevel = others.Count > 0 ? others[0].level : 0;

            // Бонусы берём из базы карточек
            CardEffectData leaderEff  = card.effects.Count > 0 ? card.effects[0] : null;
            CardEffectData partnerEff = card.effects.Count > 1 ? card.effects[1] : null;

            int leaderBonus  = leaderEff  != null ? leaderEff.amount  : Random.Range(2, 5);
            int partnerBonus = partnerEff != null ? partnerEff.amount : Random.Range(1, 3);
            string partnerStatName = partnerEff != null && !string.IsNullOrEmpty(partnerEff.statName)
                ? partnerEff.statName
                : GetRandomOtherStat(statName);

            if (myLevel > maxLevel)
            {
                player.ChangeStat(statName, leaderBonus);
                player.ChangeStat(partnerStatName, partnerBonus);
                result.description += $"\nSolo! +{leaderBonus} {statName}, +{partnerBonus} {partnerStatName}";
            }
            else
            {
                var candidates = others.FindAll(o => o.level >= myLevel).ConvertAll(o => o.p);
                PlayerController chosenPartner = null;
                if (candidates.Count == 1)
                    chosenPartner = candidates[0];
                else
                    yield return greenCardUI.ShowAndWait(statName, candidates, p => chosenPartner = p);

                player.ChangeStat(partnerStatName, partnerBonus);
                chosenPartner.ChangeStat(statName, leaderBonus);
                result.description += $"\nPartner: {chosenPartner.playerName} — You +{partnerBonus} {partnerStatName}, {chosenPartner.playerName} +{leaderBonus} {statName}";
            }
            break;
    }
        // Показываем физическую карточку
cardVisual?.Show(card, statName);
    }

    private string GetRandomOtherStat(string exclude)
    {
        var available = new List<string>();
        foreach (string s in allStats)
            if (s != exclude) available.Add(s);
        return available[UnityEngine.Random.Range(0, available.Count)];
    }

    private IEnumerator JumpToNode(PlayerController p, Vector3 targetPos)
    {
        float elapsed = 0;
        Vector3 startPos = p.transform.position;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpDuration;
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            p.transform.position = new Vector3(pos.x, pos.y + jumpHeight * 4f * t * (1f - t), pos.z);
            yield return null;
        }

        p.PlayJumpSound();
        p.transform.position = targetPos;
    }

    private List<BoardNode> GetPossibleDestinations(BoardNode start, int moves)
    {
        var result = new List<BoardNode>();
        var visited = new List<BoardNode> { start };
        if (start.nodeStat == BoardNode.NodeType.Money) result.Add(start);
        FindPathsRecursive(start, moves, new List<string>(), visited, result);
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
                FindPathsRecursive(next, movesLeft - 1,
                    new List<string>(visitedEdges) { edgeId },
                    new List<BoardNode>(visitedNodes) { next },
                    result);
        }
    }

    private List<BoardNode> GetPathToTarget(BoardNode start, BoardNode target, int maxSteps)
    {
        if (start == target && start.nodeStat == BoardNode.NodeType.Money) return new List<BoardNode>();

        var queue = new Queue<(List<BoardNode> nodes, List<string> edges)>();
        queue.Enqueue((new List<BoardNode> { start }, new List<string>()));

        while (queue.Count > 0)
        {
            var (path, visitedEdges) = queue.Dequeue();
            BoardNode last = path[path.Count - 1];

            if (last == target && path.Count - 1 == maxSteps)
            {
                path.RemoveAt(0);
                return path;
            }

            if (path.Count - 1 >= maxSteps) continue;

            foreach (BoardNode neighbor in last.neighbors)
            {
                string edgeId = GetEdgeId(last, neighbor);
                if (!visitedEdges.Contains(edgeId) && !path.Contains(neighbor))
                    queue.Enqueue((new List<BoardNode>(path) { neighbor }, new List<string>(visitedEdges) { edgeId }));
            }
        }

        return null;
    }

    private string GetEdgeId(BoardNode a, BoardNode b) =>
        string.Compare(a.name, b.name) < 0 ? a.name + b.name : b.name + a.name;

    private void UpdatePlayersVisuals()
    {
        for (int i = 0; i < expectedPlayerCount; i++)
            players[i].SetTransparency(i != currentPlayerIndex);
        uiManager?.UpdateAllStats(players[currentPlayerIndex]);
    }

    private Vector3 GetOffsetPosition(BoardNode node)
    {
        int count = 0;
        for (int i = 0; i < expectedPlayerCount; i++)
            if (players[i].currentNode == node && i != currentPlayerIndex) count++;

        if (count == 0) return node.transform.position;

        float angle = count * 90f * Mathf.Deg2Rad;
        return node.transform.position + new Vector3(Mathf.Cos(angle) * 5f, 0, Mathf.Sin(angle) * 5f);
    }
    private IEnumerator ChooseTravelDestination(PlayerController player)
{
    Debug.Log("Choose any node to travel to...");

    // Подсвечиваем все ноды кроме текущей
    List<BoardNode> allNodes = new List<BoardNode>(FindObjectsOfType<BoardNode>());
    allNodes.Remove(player.currentNode);
    foreach (var node in allNodes) node.SetHighlight(true);

    // Ждём клика игрока
    BoardNode chosen = null;
    while (chosen == null)
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                BoardNode target = hit.collider.GetComponent<BoardNode>();
                if (target != null && allNodes.Contains(target))
                    chosen = target;
            }
        }
        yield return null;
    }

    foreach (var node in allNodes) node.SetHighlight(false);

    // Прыгаем напрямую
    yield return StartCoroutine(JumpToNode(player, GetOffsetPosition(chosen)));
    player.currentNode = chosen;
    Debug.Log($"{player.playerName} travelled to {chosen.nodeName}");

    PullTravelCard(player);
}

private void PullTravelCard(PlayerController player)
{
    string[] possibleStats = { "money", "experience", "success", "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };

    // Перемешиваем и берём 2-3 случайных стата
    int bonusCount = UnityEngine.Random.Range(2, 4);
    List<string> pool = new List<string>(possibleStats);
    List<string> chosen = new List<string>();

    while (chosen.Count < bonusCount)
    {
        int idx = UnityEngine.Random.Range(0, pool.Count);
        chosen.Add(pool[idx]);
        pool.RemoveAt(idx);
    }

    System.Text.StringBuilder desc = new System.Text.StringBuilder();
    foreach (string stat in chosen)
    {
        int amount = UnityEngine.Random.Range(1, 3);
        player.ChangeStat(stat, amount);
        desc.AppendLine($"+{amount} {stat}");
    }

    Debug.Log($"{player.playerName} travel bonuses: {desc}");

    cardVisual?.ShowRaw(
        "✈️ TRAVEL CARD",
        desc.ToString().Trim(),
        CardType.Surprise,
        "travel"
    );
}
private IEnumerator TryApplyGrant(PlayerController player, List<string> availableStats)
{
    string chosenStat = availableStats[0];

    player.appliedGrants.Add(chosenStat);

    int roll = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
    int expLevel = player.GetStatValue("experience");

    Debug.Log($"{player.playerName} applies for grant in {chosenStat}. Roll: {roll}, Exp: {expLevel}");

    yield return new WaitForSeconds(0.3f);

    if (expLevel > roll)
    {
        player.earnedGrants.Add(chosenStat);
        player.ChangeStat("success", 1);

        cardVisual?.ShowRaw(
    "GRANT APPROVED! 🎉",
    $"Sphere: {chosenStat}\n" +
    $"Roll: {roll} < Your exp: {expLevel}\n" +
    $"Grant added to your profile! +1 Success",
    CardType.Red,
    chosenStat
);

        Debug.Log($"{player.playerName} earned grant in {chosenStat}. Total grants: {player.earnedGrants.Count}");
    }
    else
    {
        cardVisual?.ShowRaw(
    "GRANT REJECTED! 🎉",
    $"Sphere: {chosenStat}\n" +
    $"Roll: {roll} < Your exp: {expLevel}\n" +
    $"Grant added to your profile! +1 Success",
    CardType.Red,
    chosenStat
);

        Debug.Log($"{player.playerName} grant rejected. Roll {roll} >= exp {expLevel}");
    }
}
private IEnumerator TryDoProject(PlayerController player)
{
    // Собираем сферы где уровень 10 и проект ещё не сделан
    List<string> availableSpheres = new List<string>();
    foreach (string stat in allStats)
        if (player.GetStatValue(stat) >= 10 && !player.completedProjects.Contains(stat))
            availableSpheres.Add(stat);

    // Условие 1: нет прокачанных сфер
    if (availableSpheres.Count == 0)
    {
        cardVisual?.ShowRaw(
    "PROJECT — NOT AVAILABLE",
    "You need level 10 in at least one sphere\nto start a project.",
    CardType.Surprise,
    "project"
);
        yield break;
    }

    // Условие 2: нет гранта и нет 5 монет
    bool hasGrant = player.earnedGrants.Count > 0;
    bool hasMoney = player.GetStatValue("money") >= 5;

    if (!hasGrant && !hasMoney)
    {
        cardVisual?.ShowRaw(
    "PROJECT — NO FUNDS",
    "You need a grant or 5 coins\nto start a project.",
    CardType.Surprise,
    "project"
);
        yield break;
    }

    // Выбираем сферу — если одна, берём автоматически, иначе UI
    string chosenSphere = null;

    if (availableSpheres.Count == 1)
    {
        chosenSphere = availableSpheres[0];
    }
    else
    {
        // TODO: покажи UI выбора сферы (аналогично GreenCardUI)
        chosenSphere = availableSpheres[0]; // временный fallback
    }

    // Списываем ресурс — грант приоритетнее
    string paymentMethod;
    if (hasGrant)
    {
        // Берём первый грант который совпадает со сферой, иначе любой
        string grantToUse = player.earnedGrants.Contains(chosenSphere)
            ? chosenSphere
            : player.earnedGrants[0];
        player.earnedGrants.Remove(grantToUse);
        paymentMethod = $"grant ({grantToUse})";
    }
    else
    {
        player.ChangeStat("money", -5);
        paymentMethod = "5 coins";
    }

    player.completedProjects.Add(chosenSphere);
    player.ChangeStat("success", 5);

    Debug.Log($"{player.playerName} completed project in {chosenSphere} using {paymentMethod}. +5 Success");

    cardVisual?.ShowRaw(
    "PROJECT COMPLETE! 🏆",
    $"Sphere: {chosenSphere.ToUpper()}\n" +
    $"Paid: {paymentMethod}\n" +
    $"+5 Success",
    CardType.Green,
    chosenSphere
);
}

private void CheckVictory(PlayerController player)
{
    if (player.success < 12) return;

    Debug.Log($"{player.playerName} WON with {player.success} success points!");

    if (victorySound != null)
        audioSource.PlayOneShot(victorySound);

    cardVisual?.ShowRaw(
    "🏆 VICTORY!",
    $"{player.playerName} wins!\nSuccess: {player.success}",
    CardType.Red,
    "success"
);

    // Останавливаем игру
    isMoving = true;
    hasRolledThisTurn = true;
}

}
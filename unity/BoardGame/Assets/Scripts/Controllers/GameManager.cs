using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum CardType { Surprise, Yellow, Blue, Red, Green, Travel, Grant }

public class GameManager : MonoBehaviour
{   


    [Header("Card Visual")]
    public CardVisual utilityCard;
    public DeckManager deckManager;

    [Header("Audio")]
    private AudioSource audioSource;
    private AudioSource bgMusicSource;
    public AudioClip victorySound;
    public AudioClip drawCardSound;  
    public AudioClip travelSound;  
    public AudioClip sadSound;  
    public AudioClip moneySound;
    public AudioClip backgroundMusic;

    public AudioClip soundIT;
    public AudioClip soundScience;
    public AudioClip soundArt;
    public AudioClip soundMedia;
    public AudioClip soundBusiness;
    public AudioClip soundSport;
    public AudioClip soundTourism;
    public AudioClip soundVolounteer;

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
    if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    if (bgMusicSource == null) bgMusicSource = gameObject.AddComponent<AudioSource>();

     if (backgroundMusic != null)
    {
        bgMusicSource.clip = backgroundMusic;
        bgMusicSource.loop = true;
        bgMusicSource.volume = 0.1f;
        bgMusicSource.Play();
    }
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

    if (players[currentPlayerIndex].skipTurns > 0)
    {
        players[currentPlayerIndex].skipTurns--;
        Debug.Log($"{players[currentPlayerIndex].playerName} skips turn ({players[currentPlayerIndex].skipTurns} left)");
        ShowCard(
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

        PlayNodeSound(currentPlayer.currentNode.nodeStat);
        TableManager table = FindObjectOfType<TableManager>();
        yield return new WaitForSeconds(0.5f);

        switch (currentPlayer.currentNode.nodeStat)
        {
            case BoardNode.NodeType.Money:
                currentPlayer.ChangeStat("money", 1);
                if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(moneySound);
                Debug.Log($"{currentPlayer.playerName} gained +1 money");
                break;
            case BoardNode.NodeType.Travel:
                if (currentPlayer.GetStatValue("money") > 0)
                {
                    currentPlayer.ChangeStat("money", -1);
                    Debug.Log($"{currentPlayer.playerName} paid 1 money for travel");
                    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(travelSound);
                    yield return StartCoroutine(ChooseTravelDestination(currentPlayer));
                }
                else
                {
                    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
                    Debug.Log($"{currentPlayer.playerName} has no money, staying on travel node");
                    ShowCard(
                    "TRAVEL — NO FUNDS",
                    "Not enough money to travel. Stay here.",
                    CardType.Surprise,
                    "travel"
                    );
    }
    break;
            case BoardNode.NodeType.Grant:
                List<string> availableGrants = new List<string>();
                foreach (string stat in allStats)
                    if (currentPlayer.GetStatValue(stat) >= 10 && !currentPlayer.appliedGrants.Contains(stat))
                        availableGrants.Add(stat);

                if (availableGrants.Count == 0)
                {
                if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
                ShowCard(
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
    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(drawCardSound);
    CardData card = CardDatabase.GetRandomBySphere(statName);

    CardVisual deck = deckManager.GetCardForNode(node.nodeStat);
    switch (card.cardType)
{
    case CardType.Surprise:
        HandleSurpriseCard(player, card);
        break;

    case CardType.Yellow:
        HandleYellowCard(player, card, statName);
        break;

    case CardType.Blue:
        HandleBlueCard(player, card, statName, expLevel);
        break;

    case CardType.Red:
        HandleRedCard(player, card, statName, statLevel);
        break;

    case CardType.Green:
        yield return HandleGreenCard(player, card, statName, deck);
        break;
}

    if (card.cardType!=CardType.Green){
        deck?.Show(card, statName);
    }
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
    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(drawCardSound);
    CardData travelCard = CardDatabase.GetRandomBySphere("travel");
    ApplyGenericEffects(player, travelCard.effects);

    Debug.Log($"{player.playerName} drew travel card.");
    CardVisual travelDeck = deckManager.GetCardForNode(BoardNode.NodeType.Travel);
    travelDeck?.Show(travelCard, "travel");
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
        CardData grantCard = CardDatabase.GetRandomBySphere("grant_success");

        CardVisual grantDeck = deckManager.GetCardForNode(BoardNode.NodeType.Grant);
        grantDeck?.Show(grantCard, chosenStat);

        Debug.Log($"{player.playerName} earned grant in {chosenStat}. Total grants: {player.earnedGrants.Count}");
    }
    else
    {
        if (sadSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
        ShowCard(
    "GRANT REJECTED! 🎉",
    $"Sphere: {chosenStat}\n" +
    $"Roll: {roll} >= Your exp: {expLevel}\n" +
    $"Grant added to your profile!",
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
        if (sadSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
        ShowCard(
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
        if (sadSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
        ShowCard(
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

    ShowCard(
    "PROJECT COMPLETE! 🏆",
    $"Sphere: {chosenSphere.ToUpper()}\nPaid: {paymentMethod}\n+5 Success",
    CardType.Green,
    chosenSphere
    );
}
private string ApplyGenericEffects(
    PlayerController player,
    List<CardEffectData> effects,
    string defaultStat = "",
    CardEffectCondition triggerCondition = CardEffectCondition.Always)
{
    if (player == null || effects == null || effects.Count == 0)
        return "";

    System.Text.StringBuilder result = new System.Text.StringBuilder();

    foreach (var eff in effects)
    {
        if (eff == null || eff.effect == CardEffect.None) continue;
        if (eff.condition != CardEffectCondition.Always && eff.condition != triggerCondition) continue;

        switch (eff.effect)
        {
            case CardEffect.GainMoney:
                player.ChangeStat("money", eff.amount);
                result.AppendLine($"+{eff.amount} money");
                break;
            case CardEffect.LoseMoney:
                player.ChangeStat("money", -eff.amount);
                result.AppendLine($"-{eff.amount} money");
                break;
            case CardEffect.GainSuccess:
                player.ChangeStat("success", eff.amount);
                result.AppendLine($"+{eff.amount} success");
                break;
            case CardEffect.SkipNextTurn:
                player.skipTurns += eff.amount;
                result.AppendLine($"skip {eff.amount} turn(s)");
                break;
            case CardEffect.GainStat:
                {
                string statToGain = ResolveEffectStatName(eff.statName, defaultStat);
                if (!string.IsNullOrEmpty(statToGain))
                {
                    player.ChangeStat(statToGain, eff.amount);
                    result.AppendLine($"+{eff.amount} {statToGain}");
                }
                break;
                }
            case CardEffect.LoseStat:
                {
                string statToLose = ResolveEffectStatName(eff.statName, defaultStat);
                if (!string.IsNullOrEmpty(statToLose))
                {
                    player.ChangeStat(statToLose, -eff.amount);
                    result.AppendLine($"-{eff.amount} {statToLose}");
                }
                break;
                }
        }
    }

    return result.ToString().Trim();
}
private string ResolveEffectStatName(CardStat stat, string fallback)
{
    return stat switch
    {
        CardStat.CurrentSphere => fallback,
        CardStat.Money => "money",
        CardStat.Experience => "experience",
        CardStat.Success => "success",
        CardStat.Volounteer => "volounteer",
        CardStat.Science => "science",
        CardStat.Art => "art",
        CardStat.Media => "media",
        CardStat.Business => "business",
        CardStat.Sport => "sport",
        CardStat.Tourism => "tourism",
        CardStat.IT => "it",
        _ => fallback
    };
}

private void CheckVictory(PlayerController player)
{
    if (player.success < 12) return;

    Debug.Log($"{player.playerName} WON with {player.success} success points!");

    if (victorySound != null)
        audioSource.PlayOneShot(victorySound);

    ShowCard(
    "🏆 VICTORY!",
    $"{player.playerName} wins!\nSuccess: {player.success}",
    CardType.Red,
    "success"
    );

    isMoving = true;
    hasRolledThisTurn = true;
}

//хендлеры карточек
private void HandleSurpriseCard(PlayerController player, CardData card)
{
    ApplyGenericEffects(player, card.effects);
}

// YELLOW
private void HandleYellowCard(PlayerController player, CardData card, string statName)
{
    ApplyGenericEffects(player, card.effects, statName);
}

// BLUE
private void HandleBlueCard(PlayerController player, CardData card, string statName, int expLevel)
{
    int diceSum = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
    bool success = expLevel > diceSum;

    ApplyGenericEffects(
        player,
        card.effects,
        statName,
        success ? CardEffectCondition.OnSuccess : CardEffectCondition.OnFailure
    );

    if (expLevel > diceSum)
        Debug.Log($"BLUE success: roll {diceSum} < exp {expLevel}");
    else
        Debug.Log($"BLUE fail: roll {diceSum} >= exp {expLevel}");
}

// RED
private void HandleRedCard(PlayerController player, CardData card, string statName, int statLevel)
{
    bool success = statLevel >= 5;

    ApplyGenericEffects(
        player,
        card.effects,
        statName,
        success ? CardEffectCondition.OnSuccess : CardEffectCondition.OnFailure
    );

    if (success)
        Debug.Log($"RED success: {statName} level {statLevel} >= 5");
    else
        Debug.Log($"RED fail: {statName} level {statLevel} < 5");
}


private IEnumerator HandleGreenCard(PlayerController player, CardData card, string statName, CardVisual deck)
{
    var soloEffects = card.soloEffects ?? new List<CardEffectData>();
    var coopEffects = card.coopEffects ?? new List<CardEffectData>();

    CardEffectData soloLeaderEff = soloEffects.Count > 0 ? soloEffects[0] : null;
    CardEffectData soloPartnerEff = soloEffects.Count > 1 ? soloEffects[1] : null;
    CardEffectData coopLeaderEff = coopEffects.Count > 0 ? coopEffects[0] : null;
    CardEffectData coopPartnerEff = coopEffects.Count > 1 ? coopEffects[1] : null;

    string soloLeaderStat = soloLeaderEff != null
        ? ResolveEffectStatName(soloLeaderEff.statName, statName)
        : statName;
    string soloPartnerStat = soloPartnerEff != null
        ? ResolveEffectStatName(soloPartnerEff.statName, statName)
        : statName;
    int soloLeaderBonus = soloLeaderEff != null ? soloLeaderEff.amount : 0;
    int soloPartnerBonus = soloPartnerEff != null ? soloPartnerEff.amount : 0;

    string coopLeaderStat = coopLeaderEff != null
        ? ResolveEffectStatName(coopLeaderEff.statName, statName)
        : statName;
    string coopPartnerStat = coopPartnerEff != null
        ? ResolveEffectStatName(coopPartnerEff.statName, statName)
        : statName;
    int coopLeaderBonus = coopLeaderEff != null ? coopLeaderEff.amount : 0;
    int coopPartnerBonus = coopPartnerEff != null ? coopPartnerEff.amount : 0;

    // Важно: уровень сравниваем по партнерской сфере, а не по основной.
    int myPartnerLevel = player.GetStatValue(coopPartnerStat);
    var candidates = new List<PlayerController> { player }; // всегда можно выбрать себя (соло)
    for (int i = 0; i < expectedPlayerCount; i++)
    {
        var candidate = players[i];
        if (candidate == player) continue;
        if (candidate.GetStatValue(coopPartnerStat) >= myPartnerLevel)
            candidates.Add(candidate);
    }

    deck?.Show(card, statName, "");
    deck?.SetLocked(true);

    PlayerController chosenPartner = null;
    if (greenCardUI == null || candidates.Count == 1)
    {
        chosenPartner = player;
    }
    else
    {
        yield return greenCardUI.ShowAndWait(coopPartnerStat, candidates, p => chosenPartner = p);
    }

    deck?.SetLocked(false);

    if (chosenPartner == null)
        yield break;

    if (chosenPartner == player)
    {
        if (!string.IsNullOrEmpty(soloLeaderStat) && soloLeaderBonus != 0)
            player.ChangeStat(soloLeaderStat, soloLeaderBonus);
        if (!string.IsNullOrEmpty(soloPartnerStat) && soloPartnerBonus != 0)
            player.ChangeStat(soloPartnerStat, soloPartnerBonus);
    }
    else
    {
        if (!string.IsNullOrEmpty(coopLeaderStat) && coopLeaderBonus != 0)
            player.ChangeStat(coopLeaderStat, coopLeaderBonus);
        if (!string.IsNullOrEmpty(coopPartnerStat) && coopPartnerBonus != 0)
            chosenPartner.ChangeStat(coopPartnerStat, coopPartnerBonus);
    }
}

private void ShowCard(string title, string desc, CardType type, string stat)
{   
    string message = string.IsNullOrWhiteSpace(desc) ? title : $"{title}\n{desc}";
    uiManager?.ShowNotification(message);
}

private void PlayNodeSound(BoardNode.NodeType nodeType)
{
     if (UnityEngine.Random.Range(0f, 1f) > 0.4f) return;
    AudioClip clip = nodeType switch
    {
        BoardNode.NodeType.IT         => soundIT,
        BoardNode.NodeType.Science    => soundScience,
        BoardNode.NodeType.Art        => soundArt,
        BoardNode.NodeType.Media      => soundMedia,
        BoardNode.NodeType.Business   => soundBusiness,
        BoardNode.NodeType.Sport      => soundSport,
        BoardNode.NodeType.Tourism    => soundTourism,
        BoardNode.NodeType.Volounteer => soundVolounteer,
        _ => null
    };

    if (clip != null && audioSource != null)
        audioSource.PlayOneShot(clip);
}
}
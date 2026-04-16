using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public enum CardType { Surprise, Yellow, Blue, Red, Green, Travel, Grant }

public class GameManager : MonoBehaviour
{   
    private struct TurnSnapshot
    {
        public int money;
        public int experience;
        public int success;
        public int volounteer;
        public int science;
        public int art;
        public int media;
        public int business;
        public int sport;
        public int tourism;
        public int it;
        public int activeGrants;
    }


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
    private readonly Dictionary<PlayerController, string> lastValidSphereByPlayer = new Dictionary<PlayerController, string>();

    private void Awake()
{
    dice.OnDiceRolled += RegisterRoll;
    foreach (var player in players)
    {
        if (player != null)
            player.OnStatsChanged += HandlePlayerStatsChanged;
    }
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
    private void OnDestroy()
    {
        dice.OnDiceRolled -= RegisterRoll;
        foreach (var player in players)
        {
            if (player != null)
                player.OnStatsChanged -= HandlePlayerStatsChanged;
        }
    }
    private void Update() => HandleSelectionInput();

    private void HandlePlayerStatsChanged(PlayerController changedPlayer)
    {
        if (changedPlayer == null || expectedPlayerCount <= 0) return;
        if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count) return;
        if (players[currentPlayerIndex] != changedPlayer) return;

        uiManager?.UpdateAllStats(changedPlayer);
    }

    private void LogGame(string message) => Debug.Log($"[ИГРА] {message}");
    private void LogPlayerEvent(PlayerController player, string message)
    {
        string playerName = player != null ? player.playerName : "Неизвестный игрок";
        Debug.Log($"[ИГРА] {playerName}: {message}");
    }

    private void Start()
    {
        if (expectedPlayerCount == 0)
        {
            LogGame("Тестовый режим: имитируем данные игроков.");
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
        LogGame($"Ожидаемое количество игроков: {count}.");
    }

    public void SetPlayerName(string name)
    {
        playerNames.Add(name);
        LogGame($"Получено имя игрока: {name}.");
        if (playerNames.Count == expectedPlayerCount)
            InitializeGameFromReact();
    }

    public void SetPlayerId(string id)
    {
        playerIds.Add(id);
        LogGame($"Получен ID игрока: {id}.");
    }

    private void InitializeGameFromReact()
    {
        LogGame($"Инициализация игры. Игроков: {expectedPlayerCount}.");

        for (int i = 0; i < players.Count; i++)
        {
            players[i].gameObject.SetActive(i < expectedPlayerCount);
            LogGame(i < expectedPlayerCount
                ? $"Активирован игрок {i + 1}: {playerNames[i]}."
                : $"Отключен лишний токен {i + 1}.");
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
        LogGame("Подготовка завершена. Игра готова к старту.");
    }

    public void TryRollDice()
    {
        if (isMoving) { LogGame("Нельзя бросить кубик во время движения фишки."); return; }
        if (hasRolledThisTurn) { LogGame("Кубик уже брошен. Выберите точку назначения."); return; }

    PlayerController currentPlayer = players[currentPlayerIndex];
    RememberLastSphereFromNode(currentPlayer, currentPlayer.currentNode != null ? currentPlayer.currentNode.nodeStat : BoardNode.NodeType.None);
    if (currentPlayer.skipTurns > 0)
    {
        TurnSnapshot turnStart = CaptureTurnSnapshot(currentPlayer);
        currentPlayer.skipTurns--;
        LogPlayerEvent(currentPlayer, $"пропускает ход. Осталось пропусков: {currentPlayer.skipTurns}.");
        ShowCard(
    "TURN SKIPPED",
    "You must skip this turn.",
    CardType.Surprise,
    "none"
);
        hasRolledThisTurn = true; // чтобы конец хода отработал
        StartCoroutine(EndTurnAfterDelay(currentPlayer, turnStart));
        return;
    }
        dice.RollDice();
    }
private IEnumerator EndTurnAfterDelay(PlayerController player, TurnSnapshot turnStart)
{
    yield return new WaitForSeconds(2f);
    ShowTurnSummary(player, turnStart);
    hasRolledThisTurn = false;
    currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
    UpdatePlayersVisuals();
}
    private void RegisterRoll(int result)
    {
        lastRoll = result;
        hasRolledThisTurn = true;
        LogPlayerEvent(players[currentPlayerIndex], $"бросил кубик: {lastRoll}.");
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
        LogPlayerEvent(players[currentPlayerIndex], $"начал перемещение на {totalRoll} шаг(ов) к узлу {target.nodeName}.");

        List<BoardNode> path = GetPathToTarget(players[currentPlayerIndex].currentNode, target, totalRoll);

        foreach (var node in clickableNodes) node.SetHighlight(false);
        clickableNodes.Clear();

        PlayerController currentPlayer = players[currentPlayerIndex];
        TurnSnapshot turnStart = CaptureTurnSnapshot(currentPlayer);

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
                LogPlayerEvent(currentPlayer, "получил +1 деньги на денежной клетке.");
                break;
            case BoardNode.NodeType.Travel:
                if (currentPlayer.GetStatValue("money") > 0)
                {
                    currentPlayer.ChangeStat("money", -1);
                    LogPlayerEvent(currentPlayer, "оплатил 1 деньги за путешествие.");
                    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(travelSound);
                    yield return StartCoroutine(ChooseTravelDestination(currentPlayer));
                }
                else
                {
                    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
                    LogPlayerEvent(currentPlayer, "не может путешествовать: недостаточно денег.");
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
                LogPlayerEvent(currentPlayer, "остановился на пустой клетке.");
                break;
            default:
                yield return StartCoroutine(PullCardCoroutine(currentPlayer));
                break;
        }

        CheckVictory(currentPlayer);
        RememberLastSphereFromNode(currentPlayer, currentPlayer.currentNode != null ? currentPlayer.currentNode.nodeStat : BoardNode.NodeType.None);
        if (isMoving && hasRolledThisTurn && currentPlayer.success >= 12)
{
    table.UpdateTablePositions();
    isMoving = false;
    yield break;
}
        table.UpdateTablePositions();
        ShowTurnSummary(currentPlayer, turnStart);
        isMoving = false;
        hasRolledThisTurn = false;
        currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
        LogGame($"Ход завершен. Следующий игрок: {players[currentPlayerIndex].playerName}.");
        UpdatePlayersVisuals();
    }

private IEnumerator PullCardCoroutine(PlayerController player, string forcedSphere = null, int chainDepth = 0)
{
    BoardNode node = player.currentNode;
    if (node.nodeStat == BoardNode.NodeType.None) yield break;

    string statName = string.IsNullOrWhiteSpace(forcedSphere)
        ? node.nodeStat.ToString().ToLower()
        : forcedSphere.ToLower();
    int statLevel = player.GetStatValue(statName);
    int expLevel = player.GetStatValue("experience");
    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(drawCardSound);
    CardData card = CardDatabase.GetRandomBySphere(statName);
    LogPlayerEvent(player, $"тянет карту сферы '{statName}' типа '{card.cardType}'.");

    CardVisual deck = deckManager.GetCardForNode(node.nodeStat);
    bool shouldDrawNextCard = false;
    switch (card.cardType)
{
    case CardType.Surprise:
        shouldDrawNextCard = HandleSurpriseCard(player, card);
        break;

    case CardType.Yellow:
        shouldDrawNextCard = HandleYellowCard(player, card, statName);
        break;

    case CardType.Blue:
        shouldDrawNextCard = HandleBlueCard(player, card, statName, expLevel);
        break;

    case CardType.Red:
        shouldDrawNextCard = HandleRedCard(player, card, statName, statLevel);
        break;

    case CardType.Green:
        yield return HandleGreenCard(player, card, statName, deck);
        break;
}

    if (card.cardType!=CardType.Green){
        deck?.Show(card, statName);
    }

    // Новый эффект: после закрытия карты сразу тянем следующую из той же сферы.
    if (shouldDrawNextCard && chainDepth < 5)
    {
        LogPlayerEvent(player, $"срабатывает эффект добора. Будет вытянута следующая карта из сферы '{statName}'.");
        yield return WaitForCardHidden(deck, 30f);
        yield return StartCoroutine(PullCardCoroutine(player, statName, chainDepth + 1));
    }
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
    LogPlayerEvent(player, "выбирает клетку для путешествия.");

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
    LogPlayerEvent(player, $"переместился путешествием на узел {chosen.nodeName}.");

    PullTravelCard(player);
}

private void PullTravelCard(PlayerController player)
{
    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(drawCardSound);
    CardData travelCard = CardDatabase.GetRandomBySphere("travel");
    ApplyGenericEffects(player, travelCard.effects, LastSphere(player));

    LogPlayerEvent(player, "вытянул карту путешествия.");
    CardVisual travelDeck = deckManager.GetCardForNode(BoardNode.NodeType.Travel);
    travelDeck?.Show(travelCard, "travel");
}
private IEnumerator TryApplyGrant(PlayerController player, List<string> availableStats)
{
    string chosenStat = availableStats[0];

    player.appliedGrants.Add(chosenStat);

    int roll = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
    int expLevel = player.GetStatValue("experience");

    LogPlayerEvent(player, $"подает заявку на грант в сфере '{chosenStat}'. Бросок: {roll}, опыт: {expLevel}.");

    yield return new WaitForSeconds(0.3f);

    if (expLevel > roll)
    {
        player.earnedGrants.Add(chosenStat);
        player.ChangeStat("success", 1);
        CardData grantCard = CardDatabase.GetRandomBySphere("grant_success");

        CardVisual grantDeck = deckManager.GetCardForNode(BoardNode.NodeType.Grant);
        grantDeck?.Show(grantCard, chosenStat);

        LogPlayerEvent(player, $"получил грант в сфере '{chosenStat}'. Всего активных грантов: {player.earnedGrants.Count}.");
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

        LogPlayerEvent(player, $"грант отклонен. Бросок {roll} >= опыт {expLevel}.");
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

    LogPlayerEvent(player, $"завершил проект в сфере '{chosenSphere}', оплата: {paymentMethod}. Награда: +5 успех.");

    ShowCard(
    "PROJECT COMPLETE! 🏆",
    $"Sphere: {chosenSphere.ToUpper()}\nPaid: {paymentMethod}\n+5 Success",
    CardType.Green,
    chosenSphere
    );
}
private bool ApplyGenericEffects(
    PlayerController player,
    List<CardEffectData> effects,
    string defaultStat = "",
    CardEffectCondition triggerCondition = CardEffectCondition.Always)
{
    if (player == null || effects == null || effects.Count == 0)
        return false;

    bool shouldDrawNextCard = false;

    foreach (var eff in effects)
    {
        if (eff == null || eff.effect == CardEffect.None) continue;
        if (eff.condition != CardEffectCondition.Always && eff.condition != triggerCondition) continue;

        switch (eff.effect)
        {
            case CardEffect.SkipNextTurn:
                player.skipTurns += eff.amount;
                LogPlayerEvent(player, $"эффект карты: пропуск ходов +{eff.amount}. Теперь пропусков: {player.skipTurns}.");
                break;
            case CardEffect.GainStat:
                {
                string statToGain = ResolveEffectStatName(eff.statName, defaultStat, player);
                if (!string.IsNullOrEmpty(statToGain))
                {
                    player.ChangeStat(statToGain, eff.amount);
                    LogPlayerEvent(player, $"эффект карты: +{eff.amount} к стату '{statToGain}'.");
                }
                break;
                }
            case CardEffect.LoseStat:
                {
                string statToLose = ResolveEffectStatName(eff.statName, defaultStat, player);
                if (!string.IsNullOrEmpty(statToLose))
                {
                    player.ChangeStat(statToLose, -eff.amount);
                    LogPlayerEvent(player, $"эффект карты: -{eff.amount} к стату '{statToLose}'.");
                }
                break;
                }
            case CardEffect.DrawNextCardFromSameSphere:
                shouldDrawNextCard = true;
                LogPlayerEvent(player, "эффект карты: добор следующей карты из этой же сферы.");
                break;
        }
    }

    return shouldDrawNextCard;
}
private string ResolveEffectStatName(CardStat stat, string fallback, PlayerController player = null)
{
    return stat switch
    {
        CardStat.CurrentSphere => fallback,
        CardStat.LastSphere => LastSphere(player),
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

private void RememberLastSphereFromNode(PlayerController player, BoardNode.NodeType nodeType)
{
    string sphere = GetSphereStatFromNode(nodeType);
    if (string.IsNullOrEmpty(sphere)) return;
    RememberLastSphere(player, sphere);
}

private void RememberLastSphere(PlayerController player, string sphere)
{
    if (player == null || string.IsNullOrWhiteSpace(sphere)) return;
    lastValidSphereByPlayer[player] = sphere;
}

private string LastSphere(PlayerController player)
{
    if (player == null) return "";
    return lastValidSphereByPlayer.TryGetValue(player, out string sphere) ? sphere : "";
}

private string GetSphereStatFromNode(BoardNode.NodeType nodeType)
{
    return nodeType switch
    {
        BoardNode.NodeType.Volounteer => "volounteer",
        BoardNode.NodeType.Science => "science",
        BoardNode.NodeType.Art => "art",
        BoardNode.NodeType.Media => "media",
        BoardNode.NodeType.Business => "business",
        BoardNode.NodeType.Sport => "sport",
        BoardNode.NodeType.Tourism => "tourism",
        BoardNode.NodeType.IT => "it",
        _ => ""
    };
}

private IEnumerator WaitForCardHidden(CardVisual deck, float timeoutSeconds)
{
    if (deck == null) yield break;

    float timer = timeoutSeconds;
    while ((deck.IsShown || deck.IsAnimating) && timer > 0f)
    {
        timer -= Time.deltaTime;
        yield return null;
    }
}

private void CheckVictory(PlayerController player)
{
    if (player.success < 12) return;

    LogPlayerEvent(player, $"победил! Очки успеха: {player.success}.");

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
private bool HandleSurpriseCard(PlayerController player, CardData card)
{
    return ApplyGenericEffects(player, card.effects);
}

// YELLOW
private bool HandleYellowCard(PlayerController player, CardData card, string statName)
{
    return ApplyGenericEffects(player, card.effects, statName);
}

// BLUE
private bool HandleBlueCard(PlayerController player, CardData card, string statName, int expLevel)
{
    int diceSum = UnityEngine.Random.Range(1, 7) + UnityEngine.Random.Range(1, 7);
    bool success = expLevel > diceSum;

    bool shouldDrawNextCard = ApplyGenericEffects(
        player,
        card.effects,
        statName,
        success ? CardEffectCondition.OnSuccess : CardEffectCondition.OnFailure
    );

    if (expLevel > diceSum)
        LogPlayerEvent(player, $"синяя карта: успех проверки (бросок {diceSum} < опыт {expLevel}).");
    else
        LogPlayerEvent(player, $"синяя карта: провал проверки (бросок {diceSum} >= опыт {expLevel}).");

    return shouldDrawNextCard;
}

// RED
private bool HandleRedCard(PlayerController player, CardData card, string statName, int statLevel)
{
    bool success = statLevel >= 5;

    bool shouldDrawNextCard = ApplyGenericEffects(
        player,
        card.effects,
        statName,
        success ? CardEffectCondition.OnSuccess : CardEffectCondition.OnFailure
    );

    if (success)
        LogPlayerEvent(player, $"красная карта: успех проверки ({statName} = {statLevel}, нужно >= 5).");
    else
        LogPlayerEvent(player, $"красная карта: провал проверки ({statName} = {statLevel}, нужно >= 5).");

    return shouldDrawNextCard;
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
        ? ResolveEffectStatName(soloLeaderEff.statName, statName, player)
        : statName;
    string soloPartnerStat = soloPartnerEff != null
        ? ResolveEffectStatName(soloPartnerEff.statName, statName, player)
        : statName;
    int soloLeaderBonus = soloLeaderEff != null ? soloLeaderEff.amount : 0;
    int soloPartnerBonus = soloPartnerEff != null ? soloPartnerEff.amount : 0;

    string coopLeaderStat = coopLeaderEff != null
        ? ResolveEffectStatName(coopLeaderEff.statName, statName, player)
        : statName;
    string coopPartnerStat = coopPartnerEff != null
        ? ResolveEffectStatName(coopPartnerEff.statName, statName, player)
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
        yield return greenCardUI.ShowAndWait(coopPartnerStat, candidates, player, p => chosenPartner = p);
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
    LogGame($"Системное уведомление: {message.Replace('\n', ' ')}");
    uiManager?.ShowNotification(message);
}

private TurnSnapshot CaptureTurnSnapshot(PlayerController player)
{
    return new TurnSnapshot
    {
        money = player.money,
        experience = player.experience,
        success = player.success,
        volounteer = player.volounteer,
        science = player.science,
        art = player.art,
        media = player.media,
        business = player.business,
        sport = player.sport,
        tourism = player.tourism,
        it = player.IT,
        activeGrants = player.earnedGrants != null ? player.earnedGrants.Count : 0
    };
}

private void ShowTurnSummary(PlayerController player, TurnSnapshot start)
{
    if (player == null || uiManager == null) return;

    List<string> changes = new List<string>();
    AppendDelta(changes, "Money", player.money - start.money);
    AppendDelta(changes, "XP", player.experience - start.experience);
    AppendDelta(changes, "Success", player.success - start.success);
    AppendDelta(changes, "Volunteer", player.volounteer - start.volounteer);
    AppendDelta(changes, "Science", player.science - start.science);
    AppendDelta(changes, "Art", player.art - start.art);
    AppendDelta(changes, "Media", player.media - start.media);
    AppendDelta(changes, "Business", player.business - start.business);
    AppendDelta(changes, "Sport", player.sport - start.sport);
    AppendDelta(changes, "Tourism", player.tourism - start.tourism);
    AppendDelta(changes, "IT", player.IT - start.it);
    AppendDelta(changes, "Grants", (player.earnedGrants != null ? player.earnedGrants.Count : 0) - start.activeGrants);

    StringBuilder builder = new StringBuilder();
    builder.Append("Turn result");
    if (changes.Count == 0)
    {
        builder.Append("\nNo stat changes.");
    }
    else
    {
        builder.Append(": ");
        builder.Append(string.Join(", ", changes));
    }

    uiManager.ShowNotification(builder.ToString(), 3.5f);
}

private void AppendDelta(List<string> changes, string label, int delta)
{
    if (delta == 0) return;
    string sign = delta > 0 ? "+" : "";
    changes.Add($"{label} {sign}{delta}");
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
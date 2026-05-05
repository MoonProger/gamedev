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
    public CharacterSelectionUI characterSelectionUI;

    [Header("Characters")]
    public CharacterDatabase characterDatabase;

    [Header("Setup")]
    public List<PlayerController> players;
    public BoardNode startNode;
    public DiceController dice;
    public UIManager uiManager;

    [Header("Settings")]
    public float jumpHeight = 5;
    public float jumpDuration = 0.7f;
    public float stepDelay = 0.25f;

    [Header("Token Slot Offsets On Node")]
    public List<Vector3> playerNodeOffsets = new List<Vector3>
    {
        new Vector3(0f, 0f, 2.6f),
        new Vector3(2.6f, 0f, 0f),
        new Vector3(0f, 0f, -2.6f),
        new Vector3(-2.6f, 0f, 0f)
    };
    public float fallbackOffsetRadius = 2.6f;

    [Header("Log Style")]
    public Color player1LogColor = new Color(0.95f, 0.45f, 0.45f);
    public Color player2LogColor = new Color(0.45f, 0.65f, 1f);
    public Color player3LogColor = new Color(0.45f, 0.85f, 0.5f);
    public Color player4LogColor = new Color(1f, 0.85f, 0.35f);
    [Range(0f, 1f)] public float logPaleAmount = 0.35f;
    [Range(0f, 1f)] public float summaryDarkenAmount = 0.2f;
    [Range(0f, 1f)] public float summarySaturationBoost = 0.4f;

    private List<BoardNode> clickableNodes = new List<BoardNode>();
    private int currentPlayerIndex = 0;
    private int lastRoll;
    private bool isMoving = false;
    private bool hasRolledThisTurn = false;
    private bool isGameInitialized = false;
    private bool hasGameEnded = false;
    private PlayerController pendingVictoryPlayer = null;
    private Coroutine pendingVictoryRoutine = null;
    private int currentTurnNumber = 1;

    public int expectedPlayerCount = 0;
    private List<string> playerNames = new List<string>();
    private List<string> playerIds = new List<string>();

    private static readonly string[] allStats = { "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };
    private readonly Dictionary<PlayerController, string> lastValidSphereByPlayer = new Dictionary<PlayerController, string>();
    private readonly Dictionary<PlayerController, CharacterData> selectedCharacterByPlayer = new Dictionary<PlayerController, CharacterData>();
    private readonly Dictionary<PlayerController, int> playerIndexLookup = new Dictionary<PlayerController, int>();
    private readonly List<string> pendingPartnerTurnChanges = new List<string>();
    private readonly List<CardVisual> cachedDeckCards = new List<CardVisual>();
    private bool deckCardCacheInitialized;
    private TableManager cachedTableManager;

    private void Awake()
{
    RebuildPlayerIndexLookup();
    deckCardCacheInitialized = false;
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
        if (changedPlayer == null || expectedPlayerCount <= 0)
            return;

        // Важно: победа может произойти в чужой ход (например, от зелёной карты).
        CheckVictory(changedPlayer);

        if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count)
            return;
        if (players[currentPlayerIndex] != changedPlayer)
            return;

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
        isGameInitialized = false;
        hasGameEnded = false;
        pendingVictoryPlayer = null;
        if (pendingVictoryRoutine != null)
        {
            StopCoroutine(pendingVictoryRoutine);
            pendingVictoryRoutine = null;
        }
        selectedCharacterByPlayer.Clear();
        currentTurnNumber = 1;
        RebuildPlayerIndexLookup();
        cachedTableManager = Object.FindFirstObjectByType<TableManager>();
        SyncLogTurnNumber();
        LogGame($"Инициализация игры. Игроков: {expectedPlayerCount}.");

        for (int i = 0; i < players.Count; i++)
        {
            players[i].gameObject.SetActive(i < expectedPlayerCount);
            LogGame(i < expectedPlayerCount
                ? $"Активирован игрок {i + 1}: {playerNames[i]}."
                : $"Отключен лишний токен {i + 1}.");
        }

        cachedTableManager?.InitializeTable();

        StartCoroutine(InitializePlayersRoutine());
    }

    private IEnumerator InitializePlayersRoutine()
    {
        yield return StartCoroutine(RunCharacterSelectionIfAvailable());

        for (int i = 0; i < expectedPlayerCount; i++)
        {
            int prev = currentPlayerIndex;
            currentPlayerIndex = i;
            if (selectedCharacterByPlayer.TryGetValue(players[i], out CharacterData pickedCharacter) && pickedCharacter != null)
                players[i].ApplyCharacter(pickedCharacter);
            else
                players[i].RandomizeStats();
            players[i].TeleportToNode(startNode);
            players[i].transform.position = GetPlayerNodePosition(startNode, i);
            currentPlayerIndex = prev;
        }

        UpdatePlayersVisuals();
        ShowCurrentTurnInLog();
        isGameInitialized = true;
        LogGame("Подготовка завершена. Игра готова к старту.");
    }

    private IEnumerator RunCharacterSelectionIfAvailable()
    {
        IReadOnlyList<CharacterData> availableCharacters = characterDatabase != null
            ? characterDatabase.GetAllCharacters()
            : null;

        if (characterSelectionUI == null || availableCharacters == null || availableCharacters.Count == 0)
        {
            LogGame("Выбор персонажей пропущен (UI или база персонажей не настроены).");
            yield break;
        }

        List<PlayerController> activePlayers = new List<PlayerController>();
        for (int i = 0; i < expectedPlayerCount && i < players.Count; i++)
        {
            if (players[i] != null)
                activePlayers.Add(players[i]);
        }

        yield return characterSelectionUI.ShowAndPickForPlayers(
            activePlayers,
            availableCharacters,
            (player, character) =>
            {
                if (player != null && character != null)
                {
                    selectedCharacterByPlayer[player] = character;
                    LogGame($"Игрок {player.playerName} выбрал персонажа '{character.displayName}'.");
                }
            });
    }

    public void TryRollDice()
    {
        if (hasGameEnded) { LogGame("Игра уже завершена."); return; }
        if (!isGameInitialized) { LogGame("Игра еще не готова. Сначала завершите выбор персонажей."); return; }
        if (isMoving) { LogGame("Нельзя бросить кубик во время движения фишки."); return; }
        if (hasRolledThisTurn) { LogGame("Кубик уже брошен. Выберите точку назначения."); return; }

    PlayerController currentPlayer = players[currentPlayerIndex];
    RememberLastSphereFromNode(currentPlayer, currentPlayer.currentNode != null ? currentPlayer.currentNode.nodeStat : BoardNode.NodeType.None);
    if (currentPlayer.skipTurns > 0)
    {
        pendingPartnerTurnChanges.Clear();
        TurnSnapshot turnStart = CaptureTurnSnapshot(currentPlayer);
        currentPlayer.skipTurns--;
        LogPlayerEvent(currentPlayer, $"пропускает ход. Осталось пропусков: {currentPlayer.skipTurns}.");
        ShowCard(
    "ХОД ПРОПУЩЕН",
    "Вы должны пропустить этот ход.",
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
    yield return WaitForAllCardsHidden(60f);
    ShowTurnSummary(player, turnStart);
    hasRolledThisTurn = false;
    currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
    currentTurnNumber++;
    SyncLogTurnNumber();
    UpdatePlayersVisuals();
    ShowCurrentTurnInLog();
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
        pendingPartnerTurnChanges.Clear();
        TurnSnapshot turnStart = CaptureTurnSnapshot(currentPlayer);

        if (path != null)
        {
            foreach (BoardNode stepNode in path)
            {
                Vector3 targetPos = GetPlayerNodePosition(stepNode, currentPlayerIndex);
                yield return StartCoroutine(JumpToNode(currentPlayer, targetPos));
                currentPlayer.currentNode = stepNode;
                yield return new WaitForSeconds(stepDelay);
            }
        }

        PlayNodeSound(currentPlayer.currentNode.nodeStat);
        TableManager table = GetTableManager();
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
                    PullTravelCard(currentPlayer);
                }
                else
                {
                    if (drawCardSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
                    LogPlayerEvent(currentPlayer, "не может путешествовать: недостаточно денег.");
                    ShowCard(
                    "ПУТЕШЕСТВИЕ — НЕДОСТАТОЧНО СРЕДСТВ",
                    "Недостаточно денег для путешествия. Останьтесь на месте.",
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
                "ГРАНТ — НЕДОСТУПЕН",
                "Нужен 10-й уровень хотя бы в одной сфере,\nчтобы подать заявку на грант.",
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
        yield return WaitForAllCardsHidden(60f);
        ShowTurnSummary(currentPlayer, turnStart);
        isMoving = false;
        hasRolledThisTurn = false;
        currentPlayerIndex = (currentPlayerIndex + 1) % expectedPlayerCount;
        currentTurnNumber++;
        SyncLogTurnNumber();
        LogGame($"Ход завершен. Следующий игрок: {players[currentPlayerIndex].playerName}.");
        UpdatePlayersVisuals();
        ShowCurrentTurnInLog();
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
    WriteLog($"{player.playerName} вытянул {GetCardTypeLabel(card.cardType)} из сферы «{GetSphereLabel(statName)}».", player);

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

    private Vector3 GetPlayerNodePosition(BoardNode node, int playerIndex)
    {
        if (node == null)
            return Vector3.zero;

        Vector3 localOffset = GetPlayerSlotOffset(playerIndex);
        return node.transform.TransformPoint(localOffset);
    }

    private Vector3 GetPlayerSlotOffset(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerNodeOffsets.Count)
            return playerNodeOffsets[playerIndex];

        // fallback для случая, если игроков больше, чем заранее заданных слотов
        float radius = Mathf.Max(0.1f, fallbackOffsetRadius);
        int safeCount = Mathf.Max(expectedPlayerCount, playerIndex + 1, 1);
        float angleDeg = (360f / safeCount) * Mathf.Max(0, playerIndex);
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
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
    int playerIndex = GetPlayerIndex(player);
    yield return StartCoroutine(JumpToNode(player, GetPlayerNodePosition(chosen, playerIndex)));
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
    WriteLog($"{player.playerName} вытянул карту путешествия ({GetCardTypeLabel(travelCard.cardType)}).", player);
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
        WriteLog($"{player.playerName} вытянул карту гранта в сфере «{GetSphereLabel(chosenStat)}».", player);

        CardVisual grantDeck = deckManager.GetCardForNode(BoardNode.NodeType.Grant);
        grantDeck?.Show(grantCard, chosenStat);

        LogPlayerEvent(player, $"получил грант в сфере '{chosenStat}'. Всего активных грантов: {player.earnedGrants.Count}.");
    }
    else
    {
        if (sadSound != null && audioSource != null) audioSource.PlayOneShot(sadSound);
        ShowCard(
    "ГРАНТ ОТКЛОНЕН",
    $"Сфера: {chosenStat}\n" +
    $"Бросок: {roll} >= Ваш опыт: {expLevel}\n" +
    $"Заявка отклонена.",
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
    "ПРОЕКТ — НЕДОСТУПЕН",
    "Нужен 10-й уровень хотя бы в одной сфере,\nчтобы начать проект.",
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
    "ПРОЕКТ — НЕДОСТАТОЧНО РЕСУРСОВ",
    "Нужен грант или 5 монет,\nчтобы начать проект.",
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
        paymentMethod = $"грант ({GetSphereLabel(grantToUse)})";
    }
    else
    {
        player.ChangeStat("money", -5);
        paymentMethod = "5 монет";
    }

    player.completedProjects.Add(chosenSphere);
    player.ChangeStat("success", 5);

    LogPlayerEvent(player, $"завершил проект в сфере '{chosenSphere}', оплата: {paymentMethod}. Награда: +5 успех.");

    ShowCard(
    "ПРОЕКТ ЗАВЕРШЕН! 🏆",
    $"Сфера: {chosenSphere.ToUpper()}\nОплата: {paymentMethod}\n+5 к успеху",
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
                WriteLog($"{player.playerName}: эффект карты — пропуск хода +{eff.amount}.", player);
                break;
            case CardEffect.GainStat:
                {
                string statToGain = ResolveEffectStatName(eff.statName, defaultStat, player);
                if (!string.IsNullOrEmpty(statToGain))
                {
                    player.ChangeStat(statToGain, eff.amount);
                    LogPlayerEvent(player, $"эффект карты: +{eff.amount} к стату '{statToGain}'.");
                    WriteLog($"{player.playerName}: эффект карты — +{eff.amount} к «{GetSphereLabel(statToGain)}».", player);
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
                    WriteLog($"{player.playerName}: эффект карты — -{eff.amount} к «{GetSphereLabel(statToLose)}».", player);
                }
                break;
                }
            case CardEffect.DrawNextCardFromSameSphere:
                shouldDrawNextCard = true;
                LogPlayerEvent(player, "эффект карты: добор следующей карты из этой же сферы.");
                WriteLog($"{player.playerName}: эффект карты — добор следующей карты из той же сферы.", player);
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

private IEnumerator WaitForAllCardsHidden(float timeoutSeconds)
{
    float timer = Mathf.Max(0.1f, timeoutSeconds);
    while (timer > 0f)
    {
        bool anyVisible = false;
        foreach (CardVisual card in EnumerateDeckCards())
        {
            if (card != null && (card.IsShown || card.IsAnimating))
            {
                anyVisible = true;
                break;
            }
        }

        if (!anyVisible)
            yield break;

        timer -= Time.deltaTime;
        yield return null;
    }
}

private IEnumerable<CardVisual> EnumerateDeckCards()
{
    RebuildDeckCardCacheIfNeeded();
    for (int i = 0; i < cachedDeckCards.Count; i++)
        yield return cachedDeckCards[i];
}

private void CheckVictory(PlayerController player)
{
    if (hasGameEnded) return;
    if (player == null || player.success < 12) return;

    // Победу показываем только когда все карты уже закрыты.
    if (pendingVictoryPlayer == null || pendingVictoryPlayer.success < 12)
        pendingVictoryPlayer = player;

    if (pendingVictoryRoutine == null)
        pendingVictoryRoutine = StartCoroutine(ResolveVictoryWhenCardsHidden());
}

private IEnumerator ResolveVictoryWhenCardsHidden()
{
    yield return WaitForAllCardsHidden(60f);

    PlayerController winner = pendingVictoryPlayer;
    pendingVictoryPlayer = null;
    pendingVictoryRoutine = null;

    if (hasGameEnded) yield break;
    if (winner == null || winner.success < 12) yield break;

    FinalizeVictory(winner);
}

private void FinalizeVictory(PlayerController player)
{
    hasGameEnded = true;
    LogPlayerEvent(player, $"победил! Очки успеха: {player.success}.");

    if (victorySound != null)
        audioSource.PlayOneShot(victorySound);
    if (bgMusicSource != null)
        bgMusicSource.Stop();

    ShowCard(
    "🏆 ПОБЕДА!",
    $"{player.playerName} побеждает!\nУспех: {player.success}",
    CardType.Red,
    "success"
    );
    uiManager?.ShowVictoryScreen(player.playerName, GetLogColorForPlayer(player));

    isMoving = true;
    hasRolledThisTurn = true;
    isGameInitialized = false;
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
    WriteLog(
        $"Условие синей карты ({GetSphereLabel("experience")} > сумма кубиков): " +
        $"{(success ? "выполнено" : "не выполнено")} ({expLevel} {(success ? ">" : "<=")} {diceSum}).",
        player);

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
    WriteLog(
        $"Условие красной карты ({GetSphereLabel(statName)} >= 5): " +
        $"{(success ? "выполнено" : "не выполнено")} ({statLevel} {(success ? ">=" : "<")} 5).",
        player);

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
        List<string> greenEffects = new List<string>();
        WriteLog("Условие зелёной карты (кооперация с подходящим партнером): не выполнено. Применен соло-режим.", player);
        if (!string.IsNullOrEmpty(soloLeaderStat) && soloLeaderBonus != 0)
        {
            player.ChangeStat(soloLeaderStat, soloLeaderBonus);
            greenEffects.Add($"+{soloLeaderBonus} к «{GetSphereLabel(soloLeaderStat)}» игроку {player.playerName}");
        }
        if (!string.IsNullOrEmpty(soloPartnerStat) && soloPartnerBonus != 0)
        {
            player.ChangeStat(soloPartnerStat, soloPartnerBonus);
            greenEffects.Add($"+{soloPartnerBonus} к «{GetSphereLabel(soloPartnerStat)}» игроку {player.playerName}");
        }
        WriteLog(greenEffects.Count > 0
            ? $"Зелёная карта (соло): {string.Join(", ", greenEffects)}."
            : "Зелёная карта (соло): эффекты не изменили характеристики.", player);
    }
    else
    {
        List<string> greenEffects = new List<string>();
        WriteLog($"Условие зелёной карты (кооперация с подходящим партнером): выполнено. Партнер: {chosenPartner.playerName}.", player);
        if (!string.IsNullOrEmpty(coopLeaderStat) && coopLeaderBonus != 0)
        {
            player.ChangeStat(coopLeaderStat, coopLeaderBonus);
            greenEffects.Add($"+{coopLeaderBonus} к «{GetSphereLabel(coopLeaderStat)}» игроку {player.playerName}");
        }
        if (!string.IsNullOrEmpty(coopPartnerStat) && coopPartnerBonus != 0)
        {
            chosenPartner.ChangeStat(coopPartnerStat, coopPartnerBonus);
            greenEffects.Add($"+{coopPartnerBonus} к «{GetSphereLabel(coopPartnerStat)}» игроку {chosenPartner.playerName}");
            pendingPartnerTurnChanges.Add($"{chosenPartner.playerName}: +{coopPartnerBonus} к «{GetSphereLabel(coopPartnerStat)}»");
        }
        WriteLog(greenEffects.Count > 0
            ? $"Зелёная карта (кооперация): {string.Join(", ", greenEffects)}."
            : "Зелёная карта (кооперация): эффекты не изменили характеристики.", player);
    }
}

private void ShowCard(string title, string desc, CardType type, string stat)
{   
    string message = string.IsNullOrWhiteSpace(desc) ? title : $"{title}\n{desc}";
    LogGame($"Системное уведомление: {message.Replace('\n', ' ')}");
    WriteLog(message);
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
    AppendDelta(changes, "Деньги", player.money - start.money);
    AppendDelta(changes, "Опыт", player.experience - start.experience);
    AppendDelta(changes, "Успех", player.success - start.success);
    AppendDelta(changes, "Волонтерство", player.volounteer - start.volounteer);
    AppendDelta(changes, "Наука", player.science - start.science);
    AppendDelta(changes, "Искусство", player.art - start.art);
    AppendDelta(changes, "Медиа", player.media - start.media);
    AppendDelta(changes, "Бизнес", player.business - start.business);
    AppendDelta(changes, "Спорт", player.sport - start.sport);
    AppendDelta(changes, "Туризм", player.tourism - start.tourism);
    AppendDelta(changes, "ИТ", player.IT - start.it);
    AppendDelta(changes, "Гранты", (player.earnedGrants != null ? player.earnedGrants.Count : 0) - start.activeGrants);

    StringBuilder builder = new StringBuilder();
    builder.Append("Итоги хода");
    if (changes.Count == 0)
    {
        builder.Append("\nИзменений характеристик нет.");
    }
    else
    {
        builder.Append(": ");
        builder.Append(string.Join(", ", changes));
    }

    if (pendingPartnerTurnChanges.Count > 0)
    {
        builder.Append("\nКооперация (партнер): ");
        builder.Append(string.Join(", ", pendingPartnerTurnChanges));
    }

    WriteLog(builder.ToString(), player, true);
}

private void AppendDelta(List<string> changes, string label, int delta)
{
    if (delta == 0) return;
    string sign = delta > 0 ? "+" : "";
    changes.Add($"{label} {sign}{delta}");
}

private void ShowCurrentTurnInLog()
{
    if (uiManager == null) return;
    if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count) return;
    PlayerController current = players[currentPlayerIndex];
    if (current == null) return;
    string colorHex = ColorUtility.ToHtmlStringRGB(GetLogColorForPlayer(current));
    uiManager.SetCurrentTurnStatus($"<b>Сейчас ход: <color=#{colorHex}>{current.playerName}</color></b>");
}

private void SyncLogTurnNumber()
{
    uiManager?.SetCurrentTurnNumber(currentTurnNumber);
}

private void WriteLog(string message, PlayerController sourcePlayer = null, bool summary = false)
{
    if (uiManager == null || string.IsNullOrWhiteSpace(message)) return;
    SyncLogTurnNumber();

    PlayerController fallbackPlayer = sourcePlayer;
    if (fallbackPlayer == null && players != null && currentPlayerIndex >= 0 && currentPlayerIndex < players.Count)
        fallbackPlayer = players[currentPlayerIndex];

    Color baseColor = GetLogColorForPlayer(fallbackPlayer);
    Color color = summary
        ? SaturateColor(DarkenColor(baseColor, summaryDarkenAmount), summarySaturationBoost)
        : MakeColorPale(baseColor, logPaleAmount);
    string hex = ColorUtility.ToHtmlStringRGB(color);
    string formatted = summary
        ? $"<b><color=#{hex}>{message}</color></b>"
        : $"<color=#{hex}>{message}</color>";

    uiManager.ShowNotification(formatted);
}

private Color GetLogColorForPlayer(PlayerController player)
{
    int idx = GetPlayerIndex(player);
    return idx switch
    {
        0 => player1LogColor,
        1 => player2LogColor,
        2 => player3LogColor,
        3 => player4LogColor,
        _ => Color.white
    };
}

private void RebuildPlayerIndexLookup()
{
    playerIndexLookup.Clear();
    if (players == null) return;
    for (int i = 0; i < players.Count; i++)
    {
        PlayerController p = players[i];
        if (p != null && !playerIndexLookup.ContainsKey(p))
            playerIndexLookup.Add(p, i);
    }
}

private int GetPlayerIndex(PlayerController player)
{
    if (player == null) return -1;
    if (playerIndexLookup.TryGetValue(player, out int idx))
        return idx;
    int fallback = players != null ? players.IndexOf(player) : -1;
    if (fallback >= 0)
        playerIndexLookup[player] = fallback;
    return fallback;
}

private TableManager GetTableManager()
{
    if (cachedTableManager == null)
        cachedTableManager = Object.FindFirstObjectByType<TableManager>();
    return cachedTableManager;
}

private void RebuildDeckCardCacheIfNeeded()
{
    if (deckManager == null)
    {
        cachedDeckCards.Clear();
        deckCardCacheInitialized = false;
        return;
    }

    if (deckCardCacheInitialized)
        return;

    cachedDeckCards.Clear();
    if (deckManager.sphereDecks != null)
    {
        for (int i = 0; i < deckManager.sphereDecks.Length; i++)
        {
            CardVisual sphereDeck = deckManager.sphereDecks[i];
            if (sphereDeck != null) cachedDeckCards.Add(sphereDeck);
        }
    }
    if (deckManager.travelDeck != null) cachedDeckCards.Add(deckManager.travelDeck);
    if (deckManager.grantDeck != null) cachedDeckCards.Add(deckManager.grantDeck);
    deckCardCacheInitialized = true;
}

private Color MakeColorPale(Color source, float amount)
{
    float t = Mathf.Clamp01(amount);
    return Color.Lerp(source, Color.gray, t);
}

private Color DarkenColor(Color source, float amount)
{
    float t = Mathf.Clamp01(amount);
    return Color.Lerp(source, Color.black, t);
}

private Color SaturateColor(Color source, float amount)
{
    Color.RGBToHSV(source, out float h, out float s, out float v);
    float boostedS = Mathf.Clamp01(s + Mathf.Clamp01(amount) * (1f - s));
    return Color.HSVToRGB(h, boostedS, v);
}

private string GetSphereLabel(string statKey)
{
    if (string.IsNullOrWhiteSpace(statKey)) return "неизвестная сфера";
    return statKey.Trim().ToLower() switch
    {
        "volounteer" => "Волонтерство",
        "science" => "Наука",
        "art" => "Искусство",
        "media" => "Медиа",
        "business" => "Бизнес",
        "sport" => "Спорт",
        "tourism" => "Туризм",
        "it" => "ИТ",
        "money" => "Деньги",
        "experience" => "Опыт",
        "success" => "Успех",
        "travel" => "Путешествие",
        "grant" => "Грант",
        "project" => "Проект",
        _ => statKey
    };
}

private string GetCardTypeLabel(CardType type)
{
    return type switch
    {
        CardType.Surprise => "карту-сюрприз",
        CardType.Yellow => "жёлтую карту",
        CardType.Blue => "синюю карту",
        CardType.Red => "красную карту",
        CardType.Green => "зелёную карту",
        CardType.Travel => "карту путешествия",
        CardType.Grant => "карту гранта",
        _ => "карту"
    };
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
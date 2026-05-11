using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    [Serializable]
    private class MovePayload
    {
        public string playerId;
        public int fromSector;
        public int toSector;
        public int dice;
    }

    [Serializable]
    private class TokenMovedPayload
    {
        public string playerId;
        public int pos;
        public int steps;
    }

    [Serializable]
    private class FinishedPayload
    {
        public string winnerUserId;
    }

    [Serializable]
    private class PlayerStatePayload
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
    }

    [Serializable]
    private class UnityStatePlayerPayload
    {
        public string userId;
        public int position;
        public int grants;
        public string selectedCharacterId;
        public string[] completedProjects;
        public PlayerStatePayload playerState;
    }

    [Serializable]
    private class UnityGameStatePayload
    {
        public bool started;
        public bool isPaused;
        public string activePlayerId;
        public string phase;
        public bool hasLastDice;
        public int lastDice;
        public int currentTurnNumber;
        public UnityStatePlayerPayload[] players;
    }

    [Serializable]
    private class CardPlayedPayload
    {
        public string playerId;
        public string cardId;
        public string deckKey;
        public int cardType;
        public string imageGuid;
        public bool greenChoiceRequired;
        public int grants = -1;
        public CardChecksPayload checks;
        public PlayerStatePayload playerState;
        public AffectedPlayerPayload[] affectedPlayers;
        public CardDeltaPayload[] deltas;
        public ChainedCardPayload[] chainedCards;
    }

    [Serializable]
    private class CardChecksPayload
    {
        public int blueDiceSum = -1;
        public bool blueSuccess;
        public bool redSuccess;
        public int grantDiceSum = -1;
        public bool grantSuccess;
        public string greenMode;
        public string greenPartnerId;
    }

    [Serializable]
    private class CardDeltaPayload
    {
        public string stat;
        public int delta;
    }

    [Serializable]
    private class ChainedCardPayload
    {
        public string cardId;
        public int cardType;
        public string imageGuid;
        public CardDeltaPayload[] deltas;
    }

    [Serializable]
    private class AffectedPlayerPayload
    {
        public string playerId;
        public PlayerStatePayload playerState;
    }

    [Serializable]
    private class ProjectCompletedPayload
    {
        public string playerId;
        public string projectId;
        public int successPoints;
        public int totalSuccess;
        public string payment;
        public PlayerStatePayload playerState;
    }

    [Serializable]
    private class CardClosedPayload
    {
        public string playerId;
        public string cardId;
    }

    [Serializable]
    private class ServerErrorPayload
    {
        public string code;
        public string message;
        public bool playSadSound;
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
    public AudioClip turnStartSound;
    public AudioClip pauseSound;
    public AudioClip resumeSound;

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
    public bool serverAuthoritativeFlow = true;

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
    private string localUserId = "";
    private string pendingGreenChoiceCardId = "";
    private readonly HashSet<string> serverAnimatingPlayerIds = new HashSet<string>();

    private static readonly string[] allStats = { "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };
    private readonly Dictionary<PlayerController, string> lastValidSphereByPlayer = new Dictionary<PlayerController, string>();
    private readonly Dictionary<PlayerController, CharacterData> selectedCharacterByPlayer = new Dictionary<PlayerController, CharacterData>();
    private readonly Dictionary<string, string> persistedCharacterIdByUserId = new Dictionary<string, string>();
    private readonly Dictionary<PlayerController, int> playerIndexLookup = new Dictionary<PlayerController, int>();
    private readonly List<string> pendingPartnerTurnChanges = new List<string>();
    private readonly List<CardVisual> cachedDeckCards = new List<CardVisual>();
    private readonly Dictionary<BoardNode, int> sectorIdByNode = new Dictionary<BoardNode, int>();
    private readonly Dictionary<int, BoardNode> nodeBySectorId = new Dictionary<int, BoardNode>();
    private readonly HashSet<string> ownerClosedCardSignals = new HashSet<string>();
    private readonly Dictionary<string, TurnSnapshot> turnStartSnapshotByUserId = new Dictionary<string, TurnSnapshot>();
    private string pendingGameStateJson;
    private Coroutine serverCardVisualRoutine;
    private bool deckCardCacheInitialized;
    private TableManager cachedTableManager;

    private void Awake()
{
    RebuildPlayerIndexLookup();
    BuildBoardSectorLookup();
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

        PlayerController uiPlayer = GetPreferredUiPlayer();
        if (uiPlayer == null || uiPlayer != changedPlayer)
            return;
        uiManager?.UpdateAllStats(uiPlayer);
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
        playerNames.Clear();
        playerIds.Clear();
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
        TryApplyPendingGameState();
    }

    private void InitializeGameFromReact()
    {
        isGameInitialized = false;
        pendingGameStateJson = null;
        hasGameEnded = false;
        pendingVictoryPlayer = null;
        if (pendingVictoryRoutine != null)
        {
            StopCoroutine(pendingVictoryRoutine);
            pendingVictoryRoutine = null;
        }
        selectedCharacterByPlayer.Clear();
        persistedCharacterIdByUserId.Clear();
        turnStartSnapshotByUserId.Clear();
        currentTurnNumber = 1;
        RebuildPlayerIndexLookup();
        cachedTableManager = UnityEngine.Object.FindFirstObjectByType<TableManager>();
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

        SetGameplayChromeVisible(false);

        StartCoroutine(InitializePlayersRoutine());
    }

    /// <summary>Игровой HUD, кубик и зелёные карты — скрываем до конца подготовки (после выбора персонажа).</summary>
    private void SetGameplayChromeVisible(bool visible)
    {
        uiManager?.SetGameplayHudVisible(visible);
        if (dice != null)
            dice.gameObject.SetActive(visible);
        if (greenCardUI != null)
            greenCardUI.gameObject.SetActive(visible);
        if (utilityCard != null)
            utilityCard.gameObject.SetActive(visible);
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
            else if (!serverAuthoritativeFlow)
                players[i].RandomizeStats();

            BoardNode anchorNode = startNode;
            if (serverAuthoritativeFlow && players[i].currentNode != null)
                anchorNode = players[i].currentNode;

            players[i].TeleportToNode(anchorNode);
            players[i].transform.position = GetPlayerNodePosition(anchorNode, i);
            currentPlayerIndex = prev;
        }

        UpdatePlayersVisuals();
        ShowCurrentTurnInLog();
        TryApplyPendingGameState();
        SetGameplayChromeVisible(true);
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

        // В авторитативном мультиплеере каждый клиент выбирает только своего персонажа.
        if (serverAuthoritativeFlow)
        {
            int localIdx = GetPlayerIndexByUserId(localUserId);
            float waitLocalSeconds = 3f;
            while (localIdx < 0 && waitLocalSeconds > 0f)
            {
                waitLocalSeconds -= Time.deltaTime;
                yield return null;
                localIdx = GetPlayerIndexByUserId(localUserId);
            }
            if (localIdx < 0 || localIdx >= players.Count || localIdx >= expectedPlayerCount)
            {
                LogGame("Выбор персонажа пропущен: локальный игрок не определен.");
                yield break;
            }

            PlayerController localPlayer = players[localIdx];
            float waitPersistedChoiceSeconds = 1.5f;
            while (waitPersistedChoiceSeconds > 0f)
            {
                TryApplyPendingGameState();
                if (persistedCharacterIdByUserId.ContainsKey(localUserId))
                    break;
                waitPersistedChoiceSeconds -= Time.deltaTime;
                yield return null;
            }

            if (persistedCharacterIdByUserId.TryGetValue(localUserId, out string persistedCharacterId) &&
                !string.IsNullOrWhiteSpace(persistedCharacterId))
            {
                CharacterData persistedCharacter = FindCharacterById(availableCharacters, persistedCharacterId);
                if (persistedCharacter != null)
                {
                    selectedCharacterByPlayer[localPlayer] = persistedCharacter;
                    LogGame($"Восстановлен выбранный персонаж '{persistedCharacter.displayName}' из серверного состояния.");
                    yield break;
                }
            }

            var localOnly = new List<PlayerController> { localPlayer };
            yield return characterSelectionUI.ShowAndPickForPlayers(
                localOnly,
                availableCharacters,
                (player, character) =>
                {
                    if (player != null && character != null)
                    {
                        selectedCharacterByPlayer[player] = character;
                        if (player == localPlayer)
                            EmitCharacterSelectIntent(character);
                        LogGame($"Игрок {player.playerName} выбрал персонажа '{character.displayName}'.");
                    }
                },
                p => GetPlayerColorTitle(p));
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
            },
            p => GetPlayerColorTitle(p));
    }

    private CharacterData FindCharacterById(IReadOnlyList<CharacterData> availableCharacters, string characterId)
    {
        if (availableCharacters == null || string.IsNullOrWhiteSpace(characterId))
            return null;

        for (int i = 0; i < availableCharacters.Count; i++)
        {
            CharacterData candidate = availableCharacters[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                continue;
            if (string.Equals(candidate.id, characterId, StringComparison.Ordinal))
                return candidate;
        }
        return null;
    }

    public void TryRollDice()
    {
        if (hasGameEnded) { LogGame("Игра уже завершена."); return; }
        if (!isGameInitialized) { LogGame("Игра еще не готова. Сначала завершите выбор персонажей."); return; }
        if (isMoving) { LogGame("Нельзя бросить кубик во время движения фишки."); return; }
        if (hasRolledThisTurn) { LogGame("Кубик уже брошен. Выберите точку назначения."); return; }

        if (serverAuthoritativeFlow)
        {
            hasRolledThisTurn = true;
            EmitRollDiceIntent();
            return;
        }

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
        if (serverAuthoritativeFlow && !IsLocalPlayersTurn())
        {
            if (clickableNodes.Count > 0)
            {
                foreach (var node in clickableNodes) node.SetHighlight(false);
                clickableNodes.Clear();
            }
            return;
        }
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
            {
                if (serverAuthoritativeFlow)
                {
                    if (!TryGetSectorId(target, out int toSector))
                    {
                        LogGame("Не удалось сопоставить узел с sectorId для отправки game.move.");
                        return;
                    }
                    foreach (var node in clickableNodes) node.SetHighlight(false);
                    clickableNodes.Clear();
                    EmitMoveIntent(lastRoll, toSector);
                    return;
                }
                StartCoroutine(MoveSequence(target, lastRoll));
            }
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

        if (!serverAuthoritativeFlow && TryGetSectorId(target, out int localToSector))
            EmitMoveIntent(totalRoll, localToSector);

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

    if (!serverAuthoritativeFlow)
        EmitCardIntent(card, statName);
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
        int highlightedIdx = GetPlayerIndexByUserId(localUserId);
        if (highlightedIdx < 0 || highlightedIdx >= expectedPlayerCount)
            highlightedIdx = currentPlayerIndex;
        for (int i = 0; i < expectedPlayerCount; i++)
            players[i].SetTransparency(i != highlightedIdx);
        PlayerController uiPlayer = GetPreferredUiPlayer();
        if (uiPlayer != null)
            uiManager?.UpdateAllStats(uiPlayer);
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

    if (!serverAuthoritativeFlow)
        EmitProjectIntent(chosenSphere, 5);
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
    int localIdx = GetPlayerIndexByUserId(localUserId);
    string youLine = localIdx >= 0 && localIdx < players.Count
        ? $"<b>Вы — <color=#{ColorUtility.ToHtmlStringRGB(GetLogColorForPlayer(players[localIdx]))}>{GetPlayerColorTitle(players[localIdx])}</color></b>"
        : "<b>Вы — наблюдатель</b>";
    uiManager.SetCurrentTurnStatus($"{youLine}\n<b>Сейчас ход: <color=#{colorHex}>{current.playerName}</color></b>");
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
        cachedTableManager = UnityEngine.Object.FindFirstObjectByType<TableManager>();
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
        "grant_success" => "Грант",
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

public void UpdateGameState(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    if (!CanApplyGameStateNow())
    {
        pendingGameStateJson = payloadJson;
        LogGame("game.state отложен: ждем инициализацию игроков.");
        return;
    }

    try
    {
        UnityGameStatePayload payload = JsonUtility.FromJson<UnityGameStatePayload>(payloadJson);
        if (payload == null)
            return;

        ApplyGameStatePayload(payload);
        pendingGameStateJson = null;
        LogGame($"game.state применен: phase={payload.phase}, active={payload.activePlayerId}");
    }
    catch (Exception e)
    {
        LogGame($"UpdateGameState parse error: {e.Message}");
    }
}

private bool CanApplyGameStateNow()
{
    return expectedPlayerCount > 0 && playerIds.Count >= expectedPlayerCount && nodeBySectorId.Count > 0;
}

private void TryApplyPendingGameState()
{
    if (string.IsNullOrWhiteSpace(pendingGameStateJson))
        return;
    if (!CanApplyGameStateNow())
        return;
    string snapshot = pendingGameStateJson;
    pendingGameStateJson = null;
    UpdateGameState(snapshot);
}

private void ApplyGameStatePayload(UnityGameStatePayload payload)
{
    if (payload == null)
        return;

    hasGameEnded = !payload.started;

    if (payload.currentTurnNumber > 0)
    {
        currentTurnNumber = payload.currentTurnNumber;
        SyncLogTurnNumber();
    }

    if (payload.players != null)
    {
        for (int i = 0; i < payload.players.Length; i++)
        {
            UnityStatePlayerPayload p = payload.players[i];
            int idx = GetPlayerIndexByUserId(p.userId);
            if (idx < 0 || idx >= players.Count)
                continue;

            PlayerController controller = players[idx];
            if (!string.IsNullOrWhiteSpace(p.selectedCharacterId))
                persistedCharacterIdByUserId[p.userId] = p.selectedCharacterId;
            else
                persistedCharacterIdByUserId.Remove(p.userId);
            ApplyServerPlayerState(controller, p.playerState);
            controller.earnedGrants = CreateGrantTokens(Mathf.Max(0, p.grants));
            controller.completedProjects = p.completedProjects != null
                ? new List<string>(p.completedProjects)
                : new List<string>();

            if (nodeBySectorId.TryGetValue(p.position, out BoardNode node))
            {
                controller.currentNode = node;
                if (!serverAnimatingPlayerIds.Contains(p.userId))
                    controller.transform.position = GetPlayerNodePosition(node, idx);
            }
        }
    }

    int activeIdx = GetPlayerIndexByUserId(payload.activePlayerId);
    if (activeIdx >= 0)
        currentPlayerIndex = activeIdx;
    StoreAllTurnStartSnapshots();

    if (payload.hasLastDice)
    {
        lastRoll = Mathf.Clamp(payload.lastDice, 1, 6);
        hasRolledThisTurn = payload.phase == "WAITING_MOVE" || payload.phase == "WAITING_ACTION";
    }
    else
    {
        lastRoll = 0;
        hasRolledThisTurn = payload.phase == "WAITING_ACTION";
    }

    if (clickableNodes.Count > 0)
    {
        foreach (var node in clickableNodes)
            node.SetHighlight(false);
        clickableNodes.Clear();
    }

    if (payload.phase == "WAITING_MOVE" && payload.hasLastDice && activeIdx >= 0 && activeIdx == currentPlayerIndex && IsLocalPlayersTurn())
        StartCoroutine(ShowMovesAfterServerDiceAnimation(lastRoll));

    GetTableManager()?.UpdateTablePositions();
    UpdatePlayersVisuals();
    ShowCurrentTurnInLog();
}

private List<string> CreateGrantTokens(int count)
{
    var result = new List<string>();
    for (int i = 0; i < count; i++)
        result.Add($"server_grant_{i + 1}");
    return result;
}

public void OnDiceRolled(int value)
{
    if (value < 1 || value > 6)
        return;

    lastRoll = value;
    hasRolledThisTurn = true;
    LogPlayerEvent(players[currentPlayerIndex], $"сервер прислал бросок кубика: {lastRoll}.");
    PlayerController actor = players[currentPlayerIndex];
    WriteLog($"{actor.playerName} бросил кубик: {lastRoll}.", actor);
    dice?.RollDiceToValue(lastRoll);
    if (IsLocalPlayersTurn())
        StartCoroutine(ShowMovesAfterServerDiceAnimation(lastRoll));
}

public void OnPlayerMove(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    try
    {
        MovePayload move = JsonUtility.FromJson<MovePayload>(payloadJson);
        int idx = GetPlayerIndexByUserId(move.playerId);
        if (idx < 0 || idx >= players.Count)
            return;
        PlayerController actor = players[idx];
        WriteLog($"{actor.playerName} переместился на новую клетку (кубик: {move.dice}).", actor);

        if (nodeBySectorId.TryGetValue(move.toSector, out BoardNode targetNode))
        {
            if (!string.IsNullOrWhiteSpace(move.playerId))
                serverAnimatingPlayerIds.Add(move.playerId);
            StartCoroutine(ApplyServerMove(idx, targetNode, Mathf.Max(1, move.dice), move.playerId));
        }
    }
    catch (Exception e)
    {
        LogGame($"OnPlayerMove parse error: {e.Message}");
    }
}

public void OnTokenMoved(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    try
    {
        TokenMovedPayload payload = JsonUtility.FromJson<TokenMovedPayload>(payloadJson);
        // game.move already carries full movement payload; token_moved is informational.
        // Avoid duplicate animation.
        LogGame($"token_moved: player={payload.playerId}, pos={payload.pos}, steps={payload.steps}");
    }
    catch (Exception e)
    {
        LogGame($"OnTokenMoved parse error: {e.Message}");
    }
}

public void OnServerError(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    try
    {
        ServerErrorPayload payload = JsonUtility.FromJson<ServerErrorPayload>(payloadJson);
        if (payload == null)
            return;

        if (!string.IsNullOrWhiteSpace(payload.message))
            WriteLog(payload.message);

        bool shouldPlaySad = payload.playSadSound;
        if (!shouldPlaySad && !string.IsNullOrWhiteSpace(payload.code))
        {
            string code = payload.code.Trim().ToUpperInvariant();
            shouldPlaySad =
                code == "GRANT_REQUIRES_LEVEL_10_SPHERE" ||
                code == "NOT_ENOUGH_RESOURCES_FOR_PROJECT" ||
                code == "NO_AVAILABLE_PROJECTS" ||
                code.Contains("TRAVEL");
        }

        if (shouldPlaySad)
            PlayUiSound(sadSound);
    }
    catch (Exception e)
    {
        LogGame($"OnServerError parse error: {e.Message}");
    }
}

public void OnCardPlayed(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    try
    {
        CardPlayedPayload payload = JsonUtility.FromJson<CardPlayedPayload>(payloadJson);
        if (payload == null)
            return;

        ApplyAffectedPlayerStates(payload.affectedPlayers);
        int idx = GetPlayerIndexByUserId(payload.playerId);
        if (idx >= 0 && idx < players.Count)
        {
            ApplyServerPlayerState(players[idx], payload.playerState);
            if (payload.grants >= 0)
                players[idx].earnedGrants = CreateGrantTokens(payload.grants);
        }
        PlayerController uiPlayer = GetPreferredUiPlayer();
        if (uiPlayer != null)
            uiManager?.UpdateAllStats(uiPlayer);

        bool isPendingGreenChoice = payload.greenChoiceRequired && payload.cardType == (int)CardType.Green;
        bool isResolvedPendingGreenChoice =
            !isPendingGreenChoice &&
            !string.IsNullOrWhiteSpace(pendingGreenChoiceCardId) &&
            payload.cardId == pendingGreenChoiceCardId;
        if (serverCardVisualRoutine != null && !isResolvedPendingGreenChoice)
            StopCoroutine(serverCardVisualRoutine);
        if (isPendingGreenChoice)
        {
            pendingGreenChoiceCardId = payload.cardId ?? "";
            serverCardVisualRoutine = StartCoroutine(ShowPendingGreenChoiceSequence(payload));
        }
        else
        {
            if (isResolvedPendingGreenChoice)
                pendingGreenChoiceCardId = "";
            if (!isResolvedPendingGreenChoice)
                serverCardVisualRoutine = StartCoroutine(ShowResolvedServerCardSequence(payload, false));
        }

        PlayerController actingPlayer = (idx >= 0 && idx < players.Count) ? players[idx] : null;
        if (actingPlayer != null && !isResolvedPendingGreenChoice)
        {
            CardType type = Enum.IsDefined(typeof(CardType), payload.cardType)
                ? (CardType)payload.cardType
                : CardType.Surprise;
            WriteLog($"{actingPlayer.playerName} вытянул {GetCardTypeLabel(type)} из сферы «{GetSphereLabel(payload.deckKey)}».", actingPlayer);
        }
        if (!isResolvedPendingGreenChoice)
            PlayUiSound(drawCardSound);

        string deltaSummary = BuildDeltaSummary(payload.deltas);
        if (!string.IsNullOrEmpty(deltaSummary))
            WriteLog(deltaSummary, actingPlayer);
        string checkSummary = BuildCardCheckSummary(payload);
        if (!string.IsNullOrEmpty(checkSummary))
            WriteLog(checkSummary, actingPlayer);
        if (isPendingGreenChoice)
            WriteLog("Ожидаем выбор цели по зелёной карте...", actingPlayer);
        if (payload.chainedCards != null && payload.chainedCards.Length > 0)
            WriteLog($"Сработал добор: +{payload.chainedCards.Length} карта(ы) из колоды.", actingPlayer);

        if (payload.deckKey != null && payload.deckKey.Trim().ToLower() == "grant_success")
        {
            if (payload.checks != null && payload.checks.grantDiceSum >= 0)
            {
                string grantResult = payload.checks.grantSuccess ? "одобрена" : "отклонена";
                WriteLog($"Заявка на грант {grantResult}. Бросок: {payload.checks.grantDiceSum}.", actingPlayer);
                PlayUiSound(payload.checks.grantSuccess ? moneySound : sadSound);
            }
            if (actingPlayer != null)
                WriteLog($"Активных грантов: {actingPlayer.earnedGrants.Count}.", actingPlayer);
        }

        LogGame($"Получен game.card: player={payload.playerId}, cardId={payload.cardId}, cardType={payload.cardType}");
    }
    catch (Exception e)
    {
        LogGame($"OnCardPlayed parse error: {e.Message}");
    }
}

private IEnumerator ShowResolvedServerCardSequence(CardPlayedPayload payload, bool skipPrimaryVisual = false)
{
    if (payload == null)
        yield break;

    string closeSignalKey = BuildCardOwnerCloseKey(payload.playerId, payload.cardId);
    CardVisual firstVisual = skipPrimaryVisual
        ? null
        : ShowResolvedServerCardOnce(payload.deckKey, payload.cardId, payload.cardType, "КАРТА");
    bool isLocalOwner = !string.IsNullOrWhiteSpace(localUserId) && payload.playerId == localUserId;
    if (firstVisual != null)
        firstVisual.SetLocked(!isLocalOwner);

    if (firstVisual != null)
    {
        if (isLocalOwner)
        {
            yield return WaitForOwnerManualCloseAndBroadcast(firstVisual, payload.playerId, payload.cardId, closeSignalKey);
        }
        else
        {
            yield return WaitForOwnerCloseSignalThenAutoHide(firstVisual, closeSignalKey, 1f);
        }
    }

    if (payload.chainedCards == null || payload.chainedCards.Length == 0)
        yield break;

    for (int i = 0; i < payload.chainedCards.Length; i++)
    {
        ChainedCardPayload chained = payload.chainedCards[i];
        if (chained == null)
            continue;

        CardVisual chainedVisual = ShowResolvedServerCardOnce(payload.deckKey, chained.cardId, chained.cardType, "ДОП. КАРТА");
        string chainDeltaSummary = BuildDeltaSummary(chained.deltas);
        if (!string.IsNullOrEmpty(chainDeltaSummary))
            WriteLog($"Добор: {chainDeltaSummary}");
        if (chainedVisual != null)
            yield return WaitForCardDismissOrAutoHide(chainedVisual, 2f);
    }
}

private IEnumerator ShowPendingGreenChoiceSequence(CardPlayedPayload payload)
{
    if (payload == null)
        yield break;

    CardVisual cardVisual = ShowResolvedServerCardOnce(payload.deckKey, payload.cardId, payload.cardType, "КАРТА");
    bool isLocalOwner = !string.IsNullOrWhiteSpace(localUserId) && payload.playerId == localUserId;
    if (cardVisual != null)
        cardVisual.SetLocked(!isLocalOwner);

    if (!isLocalOwner)
    {
        string closeSignalKey = BuildCardOwnerCloseKey(payload.playerId, payload.cardId);
        if (cardVisual != null)
            yield return WaitForOwnerCloseSignalThenAutoHide(cardVisual, closeSignalKey, 1f);
        yield break;
    }

    int actorIdx = GetPlayerIndexByUserId(payload.playerId);
    PlayerController selfPlayer = (actorIdx >= 0 && actorIdx < players.Count) ? players[actorIdx] : null;
    var candidates = new List<PlayerController>();
    if (selfPlayer != null)
        candidates.Add(selfPlayer);

    if (payload.affectedPlayers != null)
    {
        for (int i = 0; i < payload.affectedPlayers.Length; i++)
        {
            var affected = payload.affectedPlayers[i];
            if (affected == null || string.IsNullOrWhiteSpace(affected.playerId)) continue;
            int cIdx = GetPlayerIndexByUserId(affected.playerId);
            if (cIdx < 0 || cIdx >= players.Count) continue;
            PlayerController candidate = players[cIdx];
            if (!candidates.Contains(candidate))
                candidates.Add(candidate);
        }
    }

    PlayerController chosenPartner = null;
    if (greenCardUI != null && selfPlayer != null && candidates.Count > 1)
        yield return greenCardUI.ShowAndWait("cooperation", candidates, selfPlayer, p => chosenPartner = p);
    else
        chosenPartner = selfPlayer;

    string selectedPartnerId = payload.playerId;
    if (chosenPartner != null)
    {
        int chosenIdx = GetPlayerIndex(chosenPartner);
        string mappedUserId = GetUserIdByPlayerIndex(chosenIdx);
        if (!string.IsNullOrWhiteSpace(mappedUserId))
            selectedPartnerId = mappedUserId;
    }
    EmitGreenChoiceIntent(payload.cardId, selectedPartnerId);
    if (cardVisual != null)
        yield return WaitForOwnerManualCloseAndBroadcast(cardVisual, payload.playerId, payload.cardId, BuildCardOwnerCloseKey(payload.playerId, payload.cardId));
}

private CardVisual ShowResolvedServerCardOnce(string deckKey, string cardId, int cardType, string title)
{
    CardData card = CardDatabase.GetByDeckAndId(deckKey, cardId);
    CardVisual deckVisual = GetDeckVisualForDeckKey(deckKey);
    if (card != null && deckVisual != null)
    {
        deckVisual.Show(card, deckKey);
        return deckVisual;
    }

    CardType fallbackType = Enum.IsDefined(typeof(CardType), cardType)
        ? (CardType)cardType
        : CardType.Surprise;
    utilityCard?.ShowRaw(title, cardId, fallbackType, deckKey ?? "");
    return utilityCard;
}

private void ApplyAffectedPlayerStates(AffectedPlayerPayload[] affectedPlayers)
{
    if (affectedPlayers == null)
        return;
    for (int i = 0; i < affectedPlayers.Length; i++)
    {
        AffectedPlayerPayload affected = affectedPlayers[i];
        if (affected == null)
            continue;
        int idx = GetPlayerIndexByUserId(affected.playerId);
        if (idx < 0 || idx >= players.Count)
            continue;
        ApplyServerPlayerState(players[idx], affected.playerState);
    }
}

private CardVisual GetDeckVisualForDeckKey(string deckKey)
{
    if (deckManager == null || string.IsNullOrWhiteSpace(deckKey))
        return utilityCard;

    return deckKey.Trim().ToLower() switch
    {
        "science" => deckManager.GetCardForNode(BoardNode.NodeType.Science),
        "art" => deckManager.GetCardForNode(BoardNode.NodeType.Art),
        "business" => deckManager.GetCardForNode(BoardNode.NodeType.Business),
        "sport" => deckManager.GetCardForNode(BoardNode.NodeType.Sport),
        "media" => deckManager.GetCardForNode(BoardNode.NodeType.Media),
        "volounteer" => deckManager.GetCardForNode(BoardNode.NodeType.Volounteer),
        "tourism" => deckManager.GetCardForNode(BoardNode.NodeType.Tourism),
        "it" => deckManager.GetCardForNode(BoardNode.NodeType.IT),
        "travel" => deckManager.GetCardForNode(BoardNode.NodeType.Travel),
        "grant_success" => deckManager.GetCardForNode(BoardNode.NodeType.Grant),
        _ => utilityCard
    };
}

private string BuildDeltaSummary(CardDeltaPayload[] deltas)
{
    if (deltas == null || deltas.Length == 0)
        return "";

    var parts = new List<string>();
    for (int i = 0; i < deltas.Length; i++)
    {
        CardDeltaPayload d = deltas[i];
        if (d == null || string.IsNullOrWhiteSpace(d.stat) || d.delta == 0)
            continue;
        string sign = d.delta > 0 ? "+" : "";
        parts.Add($"{GetSphereLabel(d.stat)} {sign}{d.delta}");
    }
    return parts.Count == 0 ? "" : string.Join(", ", parts);
}

private string BuildCardCheckSummary(CardPlayedPayload payload)
{
    if (payload == null || payload.checks == null)
        return "";

    if (payload.cardType == (int)CardType.Blue && payload.checks.blueDiceSum >= 0)
        return payload.checks.blueSuccess
            ? $"Синяя карта: проверка пройдена (сумма кубиков {payload.checks.blueDiceSum})."
            : $"Синяя карта: проверка не пройдена (сумма кубиков {payload.checks.blueDiceSum}).";

    if (payload.cardType == (int)CardType.Red)
        return payload.checks.redSuccess
            ? "Красная карта: проверка пройдена."
            : "Красная карта: проверка не пройдена.";

    if (payload.cardType == (int)CardType.Green)
    {
        string mode = string.IsNullOrWhiteSpace(payload.checks.greenMode) ? "" : payload.checks.greenMode.Trim().ToLower();
        if (mode == "pending")
            return "Зелёная карта: ждём выбор цели игроком.";
        if (mode == "coop")
            return "Зелёная карта: режим кооперации с партнером.";
        if (mode == "solo")
            return "Зелёная карта: режим соло.";
    }

    return "";
}

public void OnProjectCompleted(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    try
    {
        ProjectCompletedPayload payload = JsonUtility.FromJson<ProjectCompletedPayload>(payloadJson);
        int idx = GetPlayerIndexByUserId(payload.playerId);
        if (idx < 0 || idx >= players.Count)
            return;

        ApplyServerPlayerState(players[idx], payload.playerState);
        PlayerController uiPlayer = GetPreferredUiPlayer();
        if (uiPlayer != null)
            uiManager?.UpdateAllStats(uiPlayer);
        PlayerController actor = players[idx];
        string paymentText = string.IsNullOrWhiteSpace(payload.payment) ? "ресурс" : payload.payment;
        WriteLog($"{actor.playerName} завершил проект «{GetSphereLabel(payload.projectId)}»: +{payload.successPoints} успех, оплата: {paymentText}, всего успеха: {payload.totalSuccess}.", actor);
        PlayUiSound(moneySound);

        LogGame($"Получен game.project: player={payload.playerId}, project={payload.projectId}, +{payload.successPoints} success");
    }
    catch (Exception e)
    {
        LogGame($"OnProjectCompleted parse error: {e.Message}");
    }
}

public void OnTurnChanged(string activePlayerId)
{
    string previousActiveUserId = GetUserIdByPlayerIndex(currentPlayerIndex);
    if (serverAuthoritativeFlow)
        ShowAuthoritativeTurnSummaryFromSnapshots(previousActiveUserId);

    int idx = GetPlayerIndexByUserId(activePlayerId);
    if (idx >= 0)
    {
        currentPlayerIndex = idx;
        StoreAllTurnStartSnapshots();
        SyncLogTurnNumber();
        hasRolledThisTurn = false;
        UpdatePlayersVisuals();
        ShowCurrentTurnInLog();
        if (idx < players.Count)
            WriteLog($"Начался ход игрока {players[idx].playerName}.", players[idx]);
        PlayUiSound(turnStartSound);
        LogGame($"Ход передан игроку {activePlayerId} (index={idx}).");
    }
}

public void OnGameFinished(string payloadJson)
{
    try
    {
        FinishedPayload payload = JsonUtility.FromJson<FinishedPayload>(payloadJson);
        int idx = GetPlayerIndexByUserId(payload.winnerUserId);
        if (idx >= 0 && idx < players.Count)
            FinalizeVictory(players[idx]);
    }
    catch (Exception e)
    {
        LogGame($"OnGameFinished parse error: {e.Message}");
    }
}

public void OnGamePaused(string payloadJson)
{
    LogGame($"Игра на паузе: {payloadJson}");
    string reason = "Ожидание переподключения игроков";
    try
    {
        PausePayload payload = JsonUtility.FromJson<PausePayload>(payloadJson);
        if (payload != null && !string.IsNullOrWhiteSpace(payload.reason))
            reason = payload.reason;
    }
    catch
    {
        // Keep fallback reason.
    }
    uiManager?.ShowPauseScreen(reason);
    PlayUiSound(pauseSound);
}

public void OnGameResumed(string payloadJson)
{
    LogGame("Игра возобновлена.");
    uiManager?.HideOverlayScreen();
    PlayUiSound(resumeSound);
}

public void OnOwnerCardClosed(string payloadJson)
{
    if (string.IsNullOrWhiteSpace(payloadJson))
        return;

    try
    {
        CardClosedPayload payload = JsonUtility.FromJson<CardClosedPayload>(payloadJson);
        if (payload == null || string.IsNullOrWhiteSpace(payload.playerId) || string.IsNullOrWhiteSpace(payload.cardId))
            return;
        ownerClosedCardSignals.Add(BuildCardOwnerCloseKey(payload.playerId, payload.cardId));
    }
    catch (Exception e)
    {
        LogGame($"OnOwnerCardClosed parse error: {e.Message}");
    }
}

[Serializable]
private class PausePayload
{
    public string reason;
}

private IEnumerator ShowMovesAfterServerDiceAnimation(int value)
{
    float timeout = 3f;
    while (dice != null && dice.IsRolling && timeout > 0f)
    {
        timeout -= Time.deltaTime;
        yield return null;
    }
    yield return new WaitForSeconds(0.05f);
    if (IsLocalPlayersTurn())
        ShowPossibleMoves(value);
}

private IEnumerator WaitForCardDismissOrAutoHide(CardVisual visual, float secondsBeforeAutoHide)
{
    if (visual == null)
        yield break;

    float timer = Mathf.Max(0f, secondsBeforeAutoHide);
    while (timer > 0f)
    {
        if (!visual.IsShown && !visual.IsAnimating)
            yield break;
        timer -= Time.deltaTime;
        yield return null;
    }

    if (visual.IsShown)
        visual.Hide();
    yield return WaitForCardHidden(visual, 5f);
}

private IEnumerator WaitForOwnerManualCloseAndBroadcast(CardVisual visual, string ownerPlayerId, string cardId, string closeSignalKey)
{
    if (visual == null)
        yield break;

    float timeout = 120f;
    bool closedByServerSignal = false;
    while (timeout > 0f)
    {
        if (!string.IsNullOrWhiteSpace(closeSignalKey) && ownerClosedCardSignals.Contains(closeSignalKey))
        {
            closedByServerSignal = true;
            ownerClosedCardSignals.Remove(closeSignalKey);
            break;
        }
        if (!visual.IsShown && !visual.IsAnimating)
            break;
        timeout -= Time.deltaTime;
        yield return null;
    }

    if (visual.IsShown)
        visual.Hide();
    yield return WaitForCardHidden(visual, 5f);
    if (!closedByServerSignal)
        EmitCardClosedByOwner(ownerPlayerId, cardId);
    if (!string.IsNullOrWhiteSpace(closeSignalKey))
        ownerClosedCardSignals.Remove(closeSignalKey);
}

private IEnumerator WaitForOwnerCloseSignalThenAutoHide(CardVisual visual, string closeSignalKey, float autoHideDelaySeconds)
{
    if (visual == null)
        yield break;
    if (string.IsNullOrWhiteSpace(closeSignalKey))
        yield break;

    float waitTimeout = 120f;
    while (waitTimeout > 0f && !ownerClosedCardSignals.Contains(closeSignalKey))
    {
        waitTimeout -= Time.deltaTime;
        if (!visual.IsShown && !visual.IsAnimating)
            yield break;
        yield return null;
    }

    ownerClosedCardSignals.Remove(closeSignalKey);
    if (!visual.IsShown)
        yield break;

    yield return new WaitForSeconds(Mathf.Max(0f, autoHideDelaySeconds));
    if (visual.IsShown)
        visual.Hide();
    yield return WaitForCardHidden(visual, 5f);
}

private string BuildCardOwnerCloseKey(string playerId, string cardId)
{
    if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(cardId))
        return "";
    return $"{playerId}:{cardId}";
}

private void EmitCardClosedByOwner(string playerId, string cardId)
{
    string safePlayer = EscapeJson(playerId ?? "");
    string safeCard = EscapeJson(cardId ?? "");
    if (string.IsNullOrWhiteSpace(safePlayer) || string.IsNullOrWhiteSpace(safeCard))
        return;
    string key = BuildCardOwnerCloseKey(playerId, cardId);
    if (!string.IsNullOrWhiteSpace(key))
        ownerClosedCardSignals.Add(key);
    UnityWebBridge.Emit("ws.game.card_closed", $"{{\"playerId\":\"{safePlayer}\",\"cardId\":\"{safeCard}\"}}");
}

private void PlayUiSound(AudioClip clip)
{
    if (clip != null && audioSource != null)
        audioSource.PlayOneShot(clip);
}

private IEnumerator ApplyServerMove(int playerIndex, BoardNode targetNode, int steps, string movedPlayerId = "")
{
    if (playerIndex < 0 || playerIndex >= players.Count || targetNode == null)
    {
        if (!string.IsNullOrWhiteSpace(movedPlayerId))
            serverAnimatingPlayerIds.Remove(movedPlayerId);
        yield break;
    }

    PlayerController player = players[playerIndex];
    BoardNode fromNode = player.currentNode;
    List<BoardNode> path = fromNode != null ? GetPathToTarget(fromNode, targetNode, steps) : null;

    if (path != null && path.Count > 0)
    {
        for (int i = 0; i < path.Count; i++)
        {
            BoardNode stepNode = path[i];
            Vector3 targetPos = GetPlayerNodePosition(stepNode, playerIndex);
            yield return StartCoroutine(JumpToNode(player, targetPos));
            player.currentNode = stepNode;
            yield return new WaitForSeconds(stepDelay);
        }
    }
    else
    {
        player.currentNode = targetNode;
        player.transform.position = GetPlayerNodePosition(targetNode, playerIndex);
    }

    GetTableManager()?.UpdateTablePositions();

    if (serverAuthoritativeFlow && !string.IsNullOrWhiteSpace(movedPlayerId) && movedPlayerId == localUserId)
        EmitAutoActionIntentForNode(targetNode);
    if (!string.IsNullOrWhiteSpace(movedPlayerId))
        serverAnimatingPlayerIds.Remove(movedPlayerId);
}

private void BuildBoardSectorLookup()
{
    sectorIdByNode.Clear();
    nodeBySectorId.Clear();

    BoardNode[] nodes = FindObjectsOfType<BoardNode>();
    Array.Sort(nodes, (a, b) =>
    {
        int byName = string.Compare(a.gameObject.name, b.gameObject.name, StringComparison.Ordinal);
        if (byName != 0) return byName;
        return a.GetInstanceID().CompareTo(b.GetInstanceID());
    });

    for (int i = 0; i < nodes.Length; i++)
    {
        sectorIdByNode[nodes[i]] = i;
        nodeBySectorId[i] = nodes[i];
    }
}

private bool TryGetSectorId(BoardNode node, out int sectorId) =>
    sectorIdByNode.TryGetValue(node, out sectorId);

private int GetPlayerIndexByUserId(string userId)
{
    if (string.IsNullOrWhiteSpace(userId))
        return -1;
    for (int i = 0; i < playerIds.Count; i++)
    {
        if (string.Equals(playerIds[i], userId, StringComparison.Ordinal))
            return i;
    }
    return -1;
}

private PlayerController GetPreferredUiPlayer()
{
    int localIdx = GetPlayerIndexByUserId(localUserId);
    if (localIdx >= 0 && localIdx < players.Count)
        return players[localIdx];
    if (currentPlayerIndex >= 0 && currentPlayerIndex < players.Count)
        return players[currentPlayerIndex];
    return null;
}

private bool IsLocalPlayersTurn()
{
    int localIdx = GetPlayerIndexByUserId(localUserId);
    return localIdx >= 0 && localIdx == currentPlayerIndex;
}

private string GetPlayerColorTitle(PlayerController player)
{
    int idx = GetPlayerIndex(player);
    return idx switch
    {
        0 => "Красный",
        1 => "Синий",
        2 => "Зелёный",
        3 => "Жёлтый",
        _ => "Игрок"
    };
}

private string GetUserIdByPlayerIndex(int idx)
{
    if (idx < 0 || idx >= playerIds.Count)
        return "";
    return playerIds[idx];
}

private bool TryGetTurnStartSnapshot(string userId, out TurnSnapshot snapshot)
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        snapshot = default;
        return false;
    }
    return turnStartSnapshotByUserId.TryGetValue(userId, out snapshot);
}

private void StoreAllTurnStartSnapshots()
{
    if (players == null || playerIds == null)
        return;
    int count = Mathf.Min(players.Count, playerIds.Count, expectedPlayerCount > 0 ? expectedPlayerCount : players.Count);
    for (int i = 0; i < count; i++)
    {
        string uid = playerIds[i];
        PlayerController player = players[i];
        if (string.IsNullOrWhiteSpace(uid) || player == null)
            continue;
        turnStartSnapshotByUserId[uid] = CaptureTurnSnapshot(player);
    }
}

private void ShowAuthoritativeTurnSummaryFromSnapshots(string previousActiveUserId)
{
    if (players == null || playerIds == null)
        return;

    bool hasAnyChanges = false;
    int count = Mathf.Min(players.Count, playerIds.Count, expectedPlayerCount > 0 ? expectedPlayerCount : players.Count);
    for (int i = 0; i < count; i++)
    {
        string uid = playerIds[i];
        PlayerController player = players[i];
        if (string.IsNullOrWhiteSpace(uid) || player == null)
            continue;
        if (!TryGetTurnStartSnapshot(uid, out TurnSnapshot startSnapshot))
            continue;

        List<string> changes = BuildTurnDeltaList(player, startSnapshot);
        if (changes.Count == 0)
            continue;

        hasAnyChanges = true;
        string text = $"Итоги хода ({player.playerName}): {string.Join(", ", changes)}";
        WriteLog(text, player, true);
    }

    if (hasAnyChanges)
        return;

    int prevIdx = GetPlayerIndexByUserId(previousActiveUserId);
    if (prevIdx >= 0 && prevIdx < players.Count && players[prevIdx] != null)
        WriteLog($"Итоги хода ({players[prevIdx].playerName}): изменений характеристик нет.", players[prevIdx], true);
}

private List<string> BuildTurnDeltaList(PlayerController player, TurnSnapshot start)
{
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
    return changes;
}

public void SetLocalUserId(string userId)
{
    localUserId = userId ?? "";
    LogGame($"Установлен localUserId: {localUserId}");
}

private void EmitRollDiceIntent()
{
    UnityWebBridge.Emit("ws.game.roll_dice", "{}");
    LogGame("Отправлен intent: game.roll_dice");
}

private void EmitCharacterSelectIntent(CharacterData character)
{
    if (character == null)
        return;
    string id = string.IsNullOrWhiteSpace(character.id) ? "character" : character.id;
    string payload =
        "{" +
        $"\"characterId\":\"{EscapeJson(id)}\"," +
        "\"stats\":{" +
        $"\"money\":{character.money}," +
        $"\"experience\":{character.experience}," +
        $"\"success\":{character.success}," +
        $"\"volounteer\":{character.volounteer}," +
        $"\"science\":{character.science}," +
        $"\"art\":{character.art}," +
        $"\"media\":{character.media}," +
        $"\"business\":{character.business}," +
        $"\"sport\":{character.sport}," +
        $"\"tourism\":{character.tourism}," +
        $"\"it\":{character.it}" +
        "}" +
        "}";
    UnityWebBridge.Emit("ws.game.character_select", payload);
    LogGame($"Отправлен intent: game.character_select id={id}");
}

private void EmitGreenChoiceIntent(string cardId, string partnerUserId)
{
    if (string.IsNullOrWhiteSpace(cardId))
        return;
    string safeCardId = EscapeJson(cardId);
    string safePartnerId = EscapeJson(string.IsNullOrWhiteSpace(partnerUserId) ? localUserId : partnerUserId);
    string payload = $"{{\"cardId\":\"{safeCardId}\",\"partnerUserId\":\"{safePartnerId}\"}}";
    UnityWebBridge.Emit("ws.game.green_choice", payload);
    LogGame($"Отправлен intent: game.green_choice card={cardId}, partner={partnerUserId}");
}

private void EmitMoveIntent(int steps, int toSector)
{
    UnityWebBridge.Emit("ws.game.move", $"{{\"steps\":{steps},\"toSector\":{toSector}}}");
    LogGame($"Отправлен intent: game.move steps={steps}, toSector={toSector}.");
}

private void EmitCardIntent(CardData card, string statName)
{
    UnityWebBridge.Emit("ws.game.card", "{}");
    LogGame("Отправлен intent: game.card");
}

private void EmitProjectIntent(string projectId, int successPoints)
{
    string safeProjectId = string.IsNullOrWhiteSpace(projectId) ? "project" : projectId;
    UnityWebBridge.Emit("ws.game.project", $"{{\"projectId\":\"{EscapeJson(safeProjectId)}\"}}");
    LogGame($"Отправлен intent: game.project projectId={safeProjectId}.");
}

private void EmitAutoActionIntentForNode(BoardNode node)
{
    if (node == null) return;

    switch (node.nodeStat)
    {
        case BoardNode.NodeType.Project:
            EmitProjectIntent("project", 5);
            break;
        case BoardNode.NodeType.None:
        case BoardNode.NodeType.Money:
            break;
        default:
            UnityWebBridge.Emit("ws.game.card", "{}");
            LogGame($"Авто intent после хода: game.card, node={node.nodeStat}");
            break;
    }
}

private static string EscapeJson(string value) =>
    string.IsNullOrEmpty(value) ? "" : value.Replace("\\", "\\\\").Replace("\"", "\\\"");

private void ApplyServerPlayerState(PlayerController player, PlayerStatePayload state)
{
    if (player == null || state == null)
        return;

    player.money = Mathf.Max(0, state.money);
    player.experience = Mathf.Clamp(state.experience, 0, 10);
    player.success = Mathf.Clamp(state.success, 0, 12);
    player.volounteer = Mathf.Clamp(state.volounteer, 0, 10);
    player.science = Mathf.Clamp(state.science, 0, 10);
    player.art = Mathf.Clamp(state.art, 0, 10);
    player.media = Mathf.Clamp(state.media, 0, 10);
    player.business = Mathf.Clamp(state.business, 0, 10);
    player.sport = Mathf.Clamp(state.sport, 0, 10);
    player.tourism = Mathf.Clamp(state.tourism, 0, 10);
    player.IT = Mathf.Clamp(state.it, 0, 10);
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
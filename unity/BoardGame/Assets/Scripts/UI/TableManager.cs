using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TableManager : MonoBehaviour
{
    private enum ScreenScaleMode
    {
        MatchWidth = 0,
        MatchHeight = 1,
        MatchMinAxis = 2,
        MatchAverage = 3
    }

    private static readonly string[] StatsByIndex = { "volounteer", "science", "art", "media", "business", "sport", "tourism", "it" };
    private static readonly Color[] DefaultPlayerColors = { Color.red, Color.blue, Color.green, Color.yellow };

    [Header("New Table Setup")]
    public GameObject newTableRoot;
    public Transform cellAnchorsRoot;
    public Transform markersParent;
    public GameObject tokenPrefab;

    [Header("Marker Offsets (per player index)")]
    public List<Vector3> playerMarkerOffsets = new List<Vector3>
    {
        new Vector3(-55f, 0f, 0f),
        new Vector3(-20f, 0f, 0f),
        new Vector3(15f, 0f, 0f),
        new Vector3(50f, 0f, 0f)
    };

    [Header("Screen Scaling")]
    public Vector2 baseResolution = new Vector2(1920f, 1080f);
    public bool scaleOffsetsWithScreen = true;
    public bool scaleMarkerSizeWithScreen = true;
    [Range(0.1f, 1.5f)] public float markerSizeScaleExponent = 0.5f;
    [Range(0.1f, 4f)] public float markerScaleMultiplier = 1f;
    [SerializeField] private ScreenScaleMode screenScaleMode = ScreenScaleMode.MatchMinAxis;

    private readonly Dictionary<string, Transform> anchorsByKey = new Dictionary<string, Transform>();
    private GameObject[,] playerMarkers;
    private Vector3[,] markerBaseScales;
    private bool isInitialized;
    private GameManager cachedGameManager;
    private bool hasAnchorWarnings;

    private GameManager GetGameManager()
    {
        if (cachedGameManager == null)
            cachedGameManager = Object.FindFirstObjectByType<GameManager>();
        return cachedGameManager;
    }

    public void InitializeTable()
    {
        GameManager gm = GetGameManager();
        if (gm == null || gm.expectedPlayerCount <= 0 || tokenPrefab == null)
            return;

        BuildAnchorLookup();
        if (anchorsByKey.Count == 0)
        {
            Debug.LogWarning("[TableManager] Не найдены TableCellAnchor. Проверь cellAnchorsRoot.");
            return;
        }

        Transform markerRoot = GetMarkerRoot();
        if (markerRoot == null)
        {
            Debug.LogWarning("[TableManager] Не задан markersParent и не найден newTableRoot/cellAnchorsRoot.");
            return;
        }

        playerMarkers = new GameObject[gm.expectedPlayerCount, StatsByIndex.Length];
        markerBaseScales = new Vector3[gm.expectedPlayerCount, StatsByIndex.Length];
        for (int playerIndex = 0; playerIndex < gm.expectedPlayerCount; playerIndex++)
        {
            for (int statIndex = 0; statIndex < StatsByIndex.Length; statIndex++)
            {
                GameObject marker = Instantiate(tokenPrefab, markerRoot);
                Image markerImage = marker.GetComponent<Image>();
                if (markerImage != null)
                {
                    Color color = playerIndex < DefaultPlayerColors.Length
                        ? DefaultPlayerColors[playerIndex]
                        : Color.white;
                    markerImage.color = color;
                }

                playerMarkers[playerIndex, statIndex] = marker;
                markerBaseScales[playerIndex, statIndex] = marker.transform.localScale;
                marker.SetActive(false);
            }
        }

        isInitialized = true;
    }

    public void UpdateTablePositions()
    {
        if (!isInitialized)
            return;

        GameManager gm = GetGameManager();
        if (gm == null || gm.players == null)
            return;

        if (anchorsByKey.Count == 0)
            BuildAnchorLookup();

        for (int playerIndex = 0; playerIndex < gm.expectedPlayerCount; playerIndex++)
        {
            if (playerIndex >= gm.players.Count || gm.players[playerIndex] == null)
                continue;

            for (int statIndex = 0; statIndex < StatsByIndex.Length; statIndex++)
            {
                GameObject marker = playerMarkers[playerIndex, statIndex];
                if (marker == null)
                    continue;

                string statName = StatsByIndex[statIndex];
                int level = gm.players[playerIndex].GetStatValue(statName);
                if (!TryGetAnchorPosition(statName, level, out Vector3 cellPosition))
                {
                    if (!hasAnchorWarnings)
                    {
                        Debug.LogWarning($"[TableManager] Нет якоря для {statName}:{Mathf.Clamp(level, 0, 10)}. " +
                                         "Добавь TableCellAnchor для этой ячейки.");
                        hasAnchorWarnings = true;
                    }
                    continue;
                }
                marker.transform.position = cellPosition + GetMarkerOffset(playerIndex);
                ApplyMarkerScale(marker, playerIndex, statIndex);
            }
        }
    }

    public void ToggleTable()
    {
        if (!isInitialized)
            InitializeTable();

        GameObject tableRoot = GetTableRoot();
        bool isActive = true;
        if (tableRoot != null)
        {
            isActive = !tableRoot.activeSelf;
            tableRoot.SetActive(isActive);
        }

        if (isInitialized && playerMarkers != null)
        {
            for (int playerIndex = 0; playerIndex < playerMarkers.GetLength(0); playerIndex++)
            {
                for (int statIndex = 0; statIndex < playerMarkers.GetLength(1); statIndex++)
                {
                    GameObject marker = playerMarkers[playerIndex, statIndex];
                    if (marker != null)
                        marker.SetActive(isActive);
                }
            }
        }

        if (isActive)
            UpdateTablePositions();
    }

    private Vector3 GetMarkerOffset(int playerIndex)
    {
        float screenScale = GetScreenScaleFactor();
        if (playerIndex >= 0 && playerIndex < playerMarkerOffsets.Count)
        {
            Vector3 offset = playerMarkerOffsets[playerIndex];
            return scaleOffsetsWithScreen ? offset * screenScale : offset;
        }

        Vector3 fallback = new Vector3(-55f + playerIndex * 35f, 0f, 0f);
        return scaleOffsetsWithScreen ? fallback * screenScale : fallback;
    }

    private bool TryGetAnchorPosition(string statName, int level, out Vector3 position)
    {
        string key = BuildAnchorKey(statName, Mathf.Clamp(level, 0, 10));
        if (anchorsByKey.TryGetValue(key, out Transform anchor) && anchor != null)
        {
            position = anchor.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    private Transform GetMarkerRoot()
    {
        if (markersParent != null)
            return markersParent;
        GameObject tableRoot = GetTableRoot();
        return tableRoot != null ? tableRoot.transform : null;
    }

    private GameObject GetTableRoot()
    {
        if (newTableRoot != null)
            return newTableRoot;
        return cellAnchorsRoot != null ? cellAnchorsRoot.gameObject : null;
    }

    private void BuildAnchorLookup()
    {
        hasAnchorWarnings = false;
        anchorsByKey.Clear();
        if (cellAnchorsRoot == null)
            return;

        TableCellAnchor[] anchors = cellAnchorsRoot.GetComponentsInChildren<TableCellAnchor>(true);
        for (int i = 0; i < anchors.Length; i++)
        {
            TableCellAnchor anchor = anchors[i];
            if (anchor == null)
                continue;

            string key = BuildAnchorKey(anchor.GetStatKeyString(), Mathf.Clamp(anchor.level, 0, 10));
            if (!anchorsByKey.ContainsKey(key))
                anchorsByKey.Add(key, anchor.transform);
        }
    }

    private static string BuildAnchorKey(string statKey, int level)
    {
        return $"{statKey.Trim().ToLower()}:{level}";
    }

    private void ApplyMarkerScale(GameObject marker, int playerIndex, int statIndex)
    {
        if (marker == null || markerBaseScales == null || !scaleMarkerSizeWithScreen)
            return;
        if (playerIndex < 0 || statIndex < 0 ||
            playerIndex >= markerBaseScales.GetLength(0) ||
            statIndex >= markerBaseScales.GetLength(1))
            return;

        float rawScale = Mathf.Max(0.0001f, GetScreenScaleFactor());
        float exponent = Mathf.Max(0.01f, markerSizeScaleExponent);
        float markerScale = Mathf.Pow(rawScale, exponent) * Mathf.Max(0.01f, markerScaleMultiplier);
        marker.transform.localScale = markerBaseScales[playerIndex, statIndex] * markerScale;
    }

    private float GetScreenScaleFactor()
    {
        float safeBaseWidth = Mathf.Max(1f, baseResolution.x);
        float safeBaseHeight = Mathf.Max(1f, baseResolution.y);
        float widthScale = Screen.width / safeBaseWidth;
        float heightScale = Screen.height / safeBaseHeight;

        return screenScaleMode switch
        {
            ScreenScaleMode.MatchWidth => widthScale,
            ScreenScaleMode.MatchHeight => heightScale,
            ScreenScaleMode.MatchAverage => (widthScale + heightScale) * 0.5f,
            _ => Mathf.Min(widthScale, heightScale)
        };
    }
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
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

    #region LifeCycle

    private void Awake()
    {
        dice.OnDiceRolled += RegisterRoll;
    }

    private void Start()
    {
        InitializePlayers();
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

    #region Initialization

    private void InitializePlayers()
{
    for (int i = 0; i < players.Count; i++)
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
}

    #endregion

    #region Turn Logic

    private void RegisterRoll(int result)
    {
        lastRoll = result;
        Debug.Log($"🎲 Dice Result: {lastRoll}");
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

        isMoving = false;
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        UpdatePlayersVisuals();
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
        FindPathsRecursive(start, moves, new List<string>(), result);
        return result;
    }

    private void FindPathsRecursive(BoardNode current, int movesLeft, List<string> visitedEdges, List<BoardNode> result)
    {
        if (movesLeft == 0)
        {
            if (!result.Contains(current)) result.Add(current);
            return;
        }

        foreach (var next in current.neighbors)
        {
            string edgeId = GetEdgeId(current, next);
            if (!visitedEdges.Contains(edgeId))
            {
                List<string> nextVisited = new List<string>(visitedEdges) { edgeId };
                FindPathsRecursive(next, movesLeft - 1, nextVisited, result);
            }
        }
    }

    private List<BoardNode> GetPathToTarget(BoardNode start, BoardNode target, int maxSteps)
    {
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
                    if (!visitedEdges.Contains(edgeId))
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

    #endregion

    #region Visuals & Offsets

    private void UpdatePlayersVisuals()
    {
        for (int i = 0; i < players.Count; i++)
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
        foreach (var p in players)
        {
            if (p.currentNode == node && p != players[currentPlayerIndex])
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
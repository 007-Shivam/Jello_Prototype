using System.Collections.Generic;
using UnityEngine;

public class JellyConnectivityManager : MonoBehaviour
{
    [Header("Bridge Visuals")]
    [SerializeField] private float bridgeLength = 0.3f;
    [SerializeField] private float bridgeThickness = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logConnectionChanges = true;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
    };

    private JellyBlock[] allJelly;

    private readonly Dictionary<(JellyBlock, JellyBlock), GameObject> bridges = new Dictionary<(JellyBlock, JellyBlock), GameObject>();

    private void Start()
    {
        allJelly = FindObjectsByType<JellyBlock>(FindObjectsSortMode.None);
        RefreshConnections();
    }

    public bool AreConnected(JellyBlock a, JellyBlock b)
    {
        Vector2Int offset = a.GridPosition - b.GridPosition;
        return Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1;
    }

    public List<JellyBlock> GetConnectedNeighbors(JellyBlock block)
    {
        List<JellyBlock> neighbors = new List<JellyBlock>();
        foreach (Vector2Int direction in Directions)
        {
            if (GridManager.Instance.GetOccupant(block.GridPosition + direction) is JellyBlock neighbor)
                neighbors.Add(neighbor);
        }
        return neighbors;
    }

    public List<JellyBlock> GetGroup(JellyBlock start)
    {
        List<JellyBlock> group = new List<JellyBlock> { start };
        HashSet<JellyBlock> visited = new HashSet<JellyBlock> { start };
        Queue<JellyBlock> queue = new Queue<JellyBlock>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            JellyBlock current = queue.Dequeue();
            foreach (JellyBlock neighbor in GetConnectedNeighbors(current))
            {
                if (visited.Add(neighbor))
                {
                    group.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return group;
    }

    public void RefreshConnections()
    {
        if (allJelly == null) return;

        HashSet<(JellyBlock, JellyBlock)> current = new HashSet<(JellyBlock, JellyBlock)>();
        foreach (JellyBlock block in allJelly)
        {
            if (GridManager.Instance.GetOccupant(block.GridPosition + Vector2Int.right) is JellyBlock right)
                current.Add((block, right));
            if (GridManager.Instance.GetOccupant(block.GridPosition + Vector2Int.up) is JellyBlock up)
                current.Add((block, up));
        }

        List<(JellyBlock, JellyBlock)> removed = new List<(JellyBlock, JellyBlock)>();
        foreach ((JellyBlock, JellyBlock) edge in bridges.Keys)
            if (!current.Contains(edge)) removed.Add(edge);

        foreach ((JellyBlock a, JellyBlock b) edge in removed)
        {
            Destroy(bridges[edge]);
            bridges.Remove(edge);
            if (logConnectionChanges) Debug.Log($"[Jelly] SEPARATED {edge.a.name} <-> {edge.b.name}");
        }

        foreach ((JellyBlock a, JellyBlock b) edge in current)
        {
            if (bridges.ContainsKey(edge)) continue;
            bridges[edge] = CreateBridge(edge.a, edge.b);
            if (logConnectionChanges) Debug.Log($"[Jelly] CONNECTED {edge.a.name} {edge.a.GridPosition} <-> {edge.b.name} {edge.b.GridPosition}");
        }
    }

    private GameObject CreateBridge(JellyBlock a, JellyBlock b)
    {
        Vector2Int offset = b.GridPosition - a.GridPosition;

        GameObject bridge = new GameObject($"Bridge to {b.name}");
        bridge.transform.SetParent(a.transform, false);
        bridge.transform.localPosition = new Vector3(offset.x * 0.5f, offset.y * 0.5f, 0f);
        bridge.transform.localScale = offset.x != 0
            ? new Vector3(bridgeLength, bridgeThickness, 1f)
            : new Vector3(bridgeThickness, bridgeLength, 1f);

        SpriteRenderer body = a.Body;
        if (body != null)
        {
            SpriteRenderer sr = bridge.AddComponent<SpriteRenderer>();
            sr.sprite = body.sprite;
            sr.color = body.color;
            sr.sortingOrder = body.sortingOrder;
        }
        return bridge;
    }
}

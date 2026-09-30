using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Grid Size (cells)")]
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 6;

    [Tooltip("Void cells: outside the level's shape. No floor is drawn and nothing can enter them. " +
             "Lets a level have any outline, not just a rectangle.")]
    [SerializeField] private List<Vector2Int> voidCells = new List<Vector2Int>();

    [Header("Floor Visuals")]
    [SerializeField] private Sprite cellSprite;
    [SerializeField] private Color floorColorA = new Color(0.86f, 0.86f, 0.82f);
    [SerializeField] private Color floorColorB = new Color(0.79f, 0.79f, 0.75f);

    [Header("Camera")]
    [SerializeField] private bool fitCameraToGrid = true;
    [SerializeField] private float cameraPadding = 1f;

    [Header("Debug")]
    [SerializeField] private bool logRegistrations = true;

    public int Width => width;
    public int Height => height;

    private HashSet<Vector2Int> voidSet;

    private readonly Dictionary<Vector2Int, GridObject> occupants = new Dictionary<Vector2Int, GridObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[Grid] More than one GridManager in the scene. Destroying the extra one.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        voidSet = new HashSet<Vector2Int>(voidCells);
        BuildFloor();
        if (fitCameraToGrid) FitCamera();
    }

    private void Start()
    {
        PrintGrid();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Vector3 GridToWorld(Vector2Int cell)
    {
        return transform.position + new Vector3(cell.x, cell.y, 0f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - transform.position;
        return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
    }

    public bool IsInside(Vector2Int cell)
    {
        bool inBounds = cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
        return inBounds && (voidSet == null || !voidSet.Contains(cell));
    }

    public GridObject GetOccupant(Vector2Int cell)
    {
        occupants.TryGetValue(cell, out GridObject occupant);
        return occupant;
    }

    public bool Register(GridObject obj, Vector2Int cell)
    {
        if (!IsInside(cell))
        {
            Debug.LogError($"[Grid] {obj.name} is outside the grid at {cell}. Grid is {width}x{height} (x 0..{width - 1}, y 0..{height - 1}).", obj);
            return false;
        }

        if (occupants.TryGetValue(cell, out GridObject existing))
        {
            Debug.LogError($"[Grid] DUPLICATE OCCUPANCY at {cell}: {obj.name} tried to register but {existing.name} is already there.", obj);
            return false;
        }

        occupants[cell] = obj;
        if (logRegistrations) Debug.Log($"[Grid] Registered {obj.name} at {cell}");
        return true;
    }

    public void Unregister(GridObject obj)
    {
        if (occupants.TryGetValue(obj.GridPosition, out GridObject existing) && existing == obj)
        {
            occupants.Remove(obj.GridPosition);
        }
    }

    public void ApplyMoves(IReadOnlyList<BlockMove> moves)
    {
        foreach (BlockMove move in moves)
        {
            if (GetOccupant(move.from) == move.block)
                occupants.Remove(move.from);
            else
                Debug.LogError($"[Grid] ApplyMoves: {move.block.name} is not registered at {move.from}.", move.block);
        }

        foreach (BlockMove move in moves)
        {
            if (occupants.TryGetValue(move.to, out GridObject blocker))
                Debug.LogError($"[Grid] ApplyMoves: {move.block.name} is overwriting {blocker.name} at {move.to}.", move.block);
            occupants[move.to] = move.block;
        }
    }

    [ContextMenu("Validate Occupancy")]
    public bool ValidateOccupancy()
    {
        bool ok = true;
        foreach (KeyValuePair<Vector2Int, GridObject> pair in occupants)
        {
            if (pair.Value == null)
            {
                Debug.LogError($"[Grid] Cell {pair.Key} points to a destroyed object.");
                ok = false;
            }
            else if (pair.Value.GridPosition != pair.Key)
            {
                Debug.LogError($"[Grid] MISMATCH: cell {pair.Key} holds {pair.Value.name}, but it thinks it is at {pair.Value.GridPosition}.", pair.Value);
                ok = false;
            }
        }
        return ok;
    }

    [ContextMenu("Print Grid")]
    public void PrintGrid()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[Grid] {width}x{height}, {occupants.Count} occupied cells   (. = empty)");
        for (int y = height - 1; y >= 0; y--)
        {
            sb.Append(y.ToString().PadLeft(2)).Append("  ");
            for (int x = 0; x < width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                GridObject occupant = GetOccupant(cell);
                char symbol = !IsInside(cell) ? ' ' : occupant != null ? occupant.DebugSymbol : '.';
                sb.Append(symbol).Append(' ');
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    private void BuildFloor()
    {
        if (cellSprite == null)
        {
            Debug.LogWarning("[Grid] No Cell Sprite assigned on GridManager, so the floor will not be drawn.", this);
            return;
        }

        Transform floorParent = new GameObject("Floor").transform;
        floorParent.SetParent(transform, false);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!IsInside(new Vector2Int(x, y))) continue;

                GameObject tile = new GameObject($"Floor {x},{y}");
                tile.transform.SetParent(floorParent, false);
                tile.transform.position = GridToWorld(new Vector2Int(x, y));

                SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                sr.sprite = cellSprite;
                sr.color = (x + y) % 2 == 0 ? floorColorA : floorColorB;
                sr.sortingOrder = -10;
            }
        }
    }

    private void FitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Grid] No camera tagged MainCamera found; cannot fit camera to grid.", this);
            return;
        }

        cam.orthographic = true;
        Vector3 center = transform.position + new Vector3((width - 1) * 0.5f, (height - 1) * 0.5f, 0f);
        cam.transform.position = new Vector3(center.x, center.y, -10f);

        float sizeForHeight = height * 0.5f + cameraPadding;
        float sizeForWidth = (width * 0.5f + cameraPadding) / cam.aspect;
        cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Vector3 bottomLeft = transform.position - new Vector3(0.5f, 0.5f, 0f);

        for (int x = 0; x <= width; x++)
            Gizmos.DrawLine(bottomLeft + new Vector3(x, 0f, 0f), bottomLeft + new Vector3(x, height, 0f));

        for (int y = 0; y <= height; y++)
            Gizmos.DrawLine(bottomLeft + new Vector3(0f, y, 0f), bottomLeft + new Vector3(width, y, 0f));
    }
}

using UnityEngine;

public abstract class GridObject : MonoBehaviour
{
    [Tooltip("Shown for debugging only. Set at runtime from the object's position; do not edit by hand.")]
    [SerializeField] private Vector2Int gridPosition;

    public Vector2Int GridPosition
    {
        get => gridPosition;
        protected set => gridPosition = value;
    }

    public abstract char DebugSymbol { get; }

    protected virtual void Awake()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null)
        {
            Debug.LogError($"[Grid] {name}: no GridManager found in the scene.", this);
            return;
        }

        Vector2Int cell = grid.WorldToGrid(transform.position);
        Vector3 snapped = grid.GridToWorld(cell);
        if ((transform.position - snapped).sqrMagnitude > 0.0001f)
        {
            Debug.LogWarning($"[Grid] {name} was off-grid at {transform.position}; snapped to cell {cell}.", this);
        }
        transform.position = snapped;

        GridPosition = cell;
        grid.Register(this, cell);
    }

    protected virtual void OnDestroy()
    {
        if (GridManager.Instance != null) GridManager.Instance.Unregister(this);
    }
}

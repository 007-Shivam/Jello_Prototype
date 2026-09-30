using UnityEngine;

public class GoalCell : MonoBehaviour
{
    public Vector2Int Cell { get; private set; }
    public bool IsSatisfied { get; private set; }

    private void Awake()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null)
        {
            Debug.LogError($"[Goal] {name}: no GridManager found in the scene.", this);
            return;
        }

        Cell = grid.WorldToGrid(transform.position);
        transform.position = grid.GridToWorld(Cell);
        if (!grid.IsInside(Cell)) Debug.LogError($"[Goal] {name} is outside the level at {Cell}.", this);
    }

    public bool Evaluate()
    {
        IsSatisfied = GridManager.Instance.GetOccupant(Cell) is JellyBlock;
        return IsSatisfied;
    }
}

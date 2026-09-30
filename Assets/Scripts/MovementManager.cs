using System.Collections.Generic;
using System.Text;
using UnityEngine;

public struct BlockMove
{
    public BlockBase block;
    public Vector2Int from;
    public Vector2Int to;

    public BlockMove(BlockBase block, Vector2Int direction)
    {
        this.block = block;
        from = block.GridPosition;
        to = from + direction;
    }

    public BlockMove(BlockBase block, Vector2Int from, Vector2Int to)
    {
        this.block = block;
        this.from = from;
        this.to = to;
    }

    public BlockMove Reversed() => new BlockMove(block, to, from);
}

public class MoveTransaction
{
    public BlockBase source;
    public Vector2Int direction;
    public readonly List<BlockMove> moves = new List<BlockMove>();

    public readonly List<BlockBase> leftBehind = new List<BlockBase>();
    public readonly Dictionary<BlockBase, string> blockReasons = new Dictionary<BlockBase, string>();

    public bool isValid;
    public string failReason;
    public BlockBase blockedBlock;

    public bool Contains(BlockBase block)
    {
        foreach (BlockMove move in moves)
            if (move.block == block) return true;
        return false;
    }
}

public class MovementManager : MonoBehaviour
{
    [SerializeField] private JellyConnectivityManager connectivity;
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private float moveDuration = 0.12f;
    [SerializeField] private float bumpDuration = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool logMoves = true;
    [SerializeField] private bool validateAfterEveryMove = true;
    [SerializeField] private bool printGridAfterEveryMove = false;

    private float busyUntil;

    private readonly Stack<List<BlockMove>> history = new Stack<List<BlockMove>>();

    public bool IsBusy => Time.time < busyUntil;

    private void Awake()
    {
        if (connectivity == null) connectivity = FindAnyObjectByType<JellyConnectivityManager>();
        if (connectivity == null) Debug.LogError("[Move] No JellyConnectivityManager in the scene.", this);
        if (levelManager == null) levelManager = FindAnyObjectByType<LevelManager>();
    }

    public bool TryMove(BlockBase source, Vector2Int direction)
    {
        if (source == null)
        {
            Debug.LogWarning("[Move] No source block assigned.");
            return false;
        }

        MoveTransaction transaction = BuildTransaction(source, direction);
        if (transaction.isValid) Validate(transaction);

        if (!transaction.isValid)
        {
            Reject(transaction);
            return false;
        }

        Execute(transaction);
        return true;
    }

    private MoveTransaction BuildTransaction(BlockBase source, Vector2Int direction)
    {
        MoveTransaction transaction = new MoveTransaction { source = source, direction = direction };

        List<BlockBase> group = GetSourceGroup(source);
        HashSet<BlockBase> moving = ResolveMovingBlocks(source, group, direction, transaction);

        foreach (BlockBase member in group)
            if (moving.Contains(member)) transaction.moves.Add(new BlockMove(member, direction));

        foreach (BlockBase member in group)
            if (!moving.Contains(member)) transaction.leftBehind.Add(member);

        if (transaction.blockedBlock == null) transaction.isValid = true;
        return transaction;
    }

    private List<BlockBase> GetSourceGroup(BlockBase source)
    {
        List<BlockBase> group = new List<BlockBase>();
        if (source is JellyBlock jellySource && connectivity != null)
            group.AddRange(connectivity.GetGroup(jellySource));
        else
            group.Add(source);
        return group;
    }

    private HashSet<BlockBase> ResolveMovingBlocks(BlockBase source, List<BlockBase> group, Vector2Int direction, MoveTransaction transaction)
    {
        HashSet<BlockBase> moving = new HashSet<BlockBase>(group);

        while (true)
        {
            List<BlockBase> blocked = FindBlockedBlocks(moving, direction, transaction);
            if (blocked.Count == 0) return moving;

            if (blocked.Contains(source))
            {
                Fail(transaction, source, transaction.blockReasons[source]);
                return moving;
            }

            foreach (BlockBase block in blocked) moving.Remove(block);
            moving = KeepConnectedToSource(source, moving);
        }
    }

    private List<BlockBase> FindBlockedBlocks(HashSet<BlockBase> moving, Vector2Int direction, MoveTransaction transaction)
    {
        List<BlockBase> blocked = new List<BlockBase>();
        foreach (BlockBase block in moving)
        {
            string reason = GetBlockingReason(block, direction, moving);
            if (reason != null)
            {
                blocked.Add(block);
                transaction.blockReasons[block] = reason;
            }
        }
        return blocked;
    }

    private string GetBlockingReason(BlockBase block, Vector2Int direction, HashSet<BlockBase> moving)
    {
        GridManager grid = GridManager.Instance;
        Vector2Int cell = block.GridPosition + direction;

        if (!grid.IsInside(cell)) return $"{cell} is outside the level";

        GridObject occupant = grid.GetOccupant(cell);
        if (occupant == null) return null;
        if (occupant is BlockBase other && moving.Contains(other)) return null;

        if (occupant is Wall) return $"{cell} is {occupant.name}";
        return $"{cell} is {occupant.name}, which is not moving";
    }

    private HashSet<BlockBase> KeepConnectedToSource(BlockBase source, HashSet<BlockBase> moving)
    {
        HashSet<BlockBase> connected = new HashSet<BlockBase> { source };
        if (!(source is JellyBlock jellySource) || connectivity == null) return connected;

        Queue<JellyBlock> queue = new Queue<JellyBlock>();
        queue.Enqueue(jellySource);
        while (queue.Count > 0)
        {
            foreach (JellyBlock neighbor in connectivity.GetConnectedNeighbors(queue.Dequeue()))
            {
                if (moving.Contains(neighbor) && connected.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }
        return connected;
    }

    private void Validate(MoveTransaction transaction)
    {
        GridManager grid = GridManager.Instance;
        HashSet<Vector2Int> destinations = new HashSet<Vector2Int>();

        foreach (BlockMove move in transaction.moves)
        {
            if (!grid.IsInside(move.to))
            {
                Fail(transaction, move.block, $"{move.to} is outside the level");
                return;
            }

            GridObject occupant = grid.GetOccupant(move.to);
            bool occupantIsLeaving = occupant is BlockBase occupantBlock && transaction.Contains(occupantBlock);
            if (occupant != null && !occupantIsLeaving)
            {
                string what = occupant is Wall ? "a wall" : "a block that is not moving";
                Fail(transaction, move.block, $"{move.to} is occupied by {occupant.name} ({what})");
                return;
            }

            if (!destinations.Add(move.to))
            {
                Fail(transaction, move.block, $"two blocks would end up in {move.to}");
                return;
            }
        }

        transaction.isValid = true;
    }

    private void Fail(MoveTransaction transaction, BlockBase block, string reason)
    {
        transaction.isValid = false;
        transaction.blockedBlock = block;
        transaction.failReason = reason;
    }

    private void Execute(MoveTransaction transaction)
    {
        ApplyAndAnimate(transaction.moves);
        history.Push(new List<BlockMove>(transaction.moves));

        foreach (BlockBase block in transaction.leftBehind)
            block.PlayBump(transaction.direction, bumpDuration);

        if (logMoves)
        {
            Debug.Log(Describe(transaction));
            foreach (BlockBase block in transaction.leftBehind)
            {
                string why = transaction.blockReasons.TryGetValue(block, out string reason)
                    ? reason
                    : "only attached to the source through blocked blocks";
                Debug.Log($"[Jelly] {block.name} STAYS BEHIND at {block.GridPosition}: {why}");
            }
        }
        AfterMoveChecks();
    }

    public bool Undo()
    {
        if (history.Count == 0)
        {
            if (logMoves) Debug.Log("[Undo] Nothing to undo.");
            return false;
        }

        List<BlockMove> lastMoves = history.Pop();
        List<BlockMove> reversed = new List<BlockMove>();
        foreach (BlockMove move in lastMoves)
            reversed.Add(move.Reversed());

        ApplyAndAnimate(reversed);

        if (logMoves) Debug.Log($"[Undo] Reverted {reversed.Count} block move(s). {history.Count} move(s) left in history.");
        AfterMoveChecks();
        return true;
    }

    private void ApplyAndAnimate(List<BlockMove> moves)
    {
        GridManager.Instance.ApplyMoves(moves);
        foreach (BlockMove move in moves)
            move.block.CommitMove(move.to, moveDuration);

        if (connectivity != null) connectivity.RefreshConnections();
        if (levelManager != null) levelManager.CheckWin();

        busyUntil = Time.time + moveDuration;
    }

    private void AfterMoveChecks()
    {
        if (validateAfterEveryMove) GridManager.Instance.ValidateOccupancy();
        if (printGridAfterEveryMove) GridManager.Instance.PrintGrid();
    }

    private void Reject(MoveTransaction transaction)
    {
        foreach (BlockMove move in transaction.moves)
            move.block.PlayBump(transaction.direction, bumpDuration);
        foreach (BlockBase block in transaction.leftBehind)
            block.PlayBump(transaction.direction, bumpDuration);
        busyUntil = Time.time + bumpDuration;

        if (logMoves)
        {
            Debug.Log($"[Move] REJECTED {transaction.source.name} {DirectionName(transaction.direction)} " +
                      $"from {transaction.source.GridPosition}: {transaction.failReason} " +
                      $"(blocked block: {transaction.blockedBlock.name})");
        }
    }

    private string Describe(MoveTransaction transaction)
    {
        StringBuilder sb = new StringBuilder($"[Move] {DirectionName(transaction.direction)}:");
        foreach (BlockMove move in transaction.moves)
            sb.Append($" {move.block.name} {move.from}->{move.to};");
        return sb.ToString();
    }

    public static string DirectionName(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return "Up";
        if (direction == Vector2Int.down) return "Down";
        if (direction == Vector2Int.left) return "Left";
        if (direction == Vector2Int.right) return "Right";
        return direction.ToString();
    }
}

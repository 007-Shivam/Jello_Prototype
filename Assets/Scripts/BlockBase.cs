using UnityEngine;
using UnityEngine.Serialization;

public enum OutlineState
{
    Normal,
    OnGoal,
    Complete
}

public abstract class BlockBase : GridObject
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer body;
    [Tooltip("Full-cell border behind the body. Its colour shows source / on-goal / level-complete.")]
    [FormerlySerializedAs("selectionHighlight")]
    [FormerlySerializedAs("sourceHighlight")]
    [SerializeField] private SpriteRenderer outline;
    [SerializeField] private float bumpDistance = 0.15f;

    [Header("Outline Colours")]
    [SerializeField] private Color sourceColor = new Color(1f, 0.82f, 0.25f);
    [SerializeField] private Color onGoalColor = new Color(0.35f, 0.75f, 1f);
    [SerializeField] private Color completeColor = new Color(0.3f, 0.9f, 0.45f);

    private enum AnimMode { None, Slide, Bump }

    private AnimMode animMode = AnimMode.None;
    private Vector3 animStart;
    private Vector3 animEnd;
    private float animTime;
    private float animDuration;

    private OutlineState outlineState = OutlineState.Normal;

    public bool IsAnimating => animMode != AnimMode.None;
    public SpriteRenderer Body => body;
    public bool IsSource { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        RefreshOutline();
    }

    public void CommitMove(Vector2Int newCell, float duration)
    {
        GridPosition = newCell;
        StartAnimation(AnimMode.Slide, transform.position, GridManager.Instance.GridToWorld(newCell), duration);
    }

    public void PlayBump(Vector2Int direction, float duration)
    {
        Vector3 home = GridManager.Instance.GridToWorld(GridPosition);
        Vector3 nudge = new Vector3(direction.x, direction.y, 0f) * bumpDistance;
        StartAnimation(AnimMode.Bump, home, home + nudge, duration);
    }

    private void StartAnimation(AnimMode mode, Vector3 start, Vector3 end, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = mode == AnimMode.Slide ? end : start;
            animMode = AnimMode.None;
            return;
        }

        animMode = mode;
        animStart = start;
        animEnd = end;
        animTime = 0f;
        animDuration = duration;
    }

    private void Update()
    {
        if (animMode == AnimMode.None) return;

        animTime += Time.deltaTime;
        float t = Mathf.Clamp01(animTime / animDuration);

        float blend = animMode == AnimMode.Slide ? Mathf.SmoothStep(0f, 1f, t) : Mathf.Sin(t * Mathf.PI);
        transform.position = Vector3.LerpUnclamped(animStart, animEnd, blend);

        if (t >= 1f)
        {
            transform.position = animMode == AnimMode.Slide ? animEnd : animStart;
            animMode = AnimMode.None;
        }
    }

    public void MarkAsSource()
    {
        IsSource = true;
        if (sourceEyes == null) sourceEyes = CreateEyes();
        RefreshOutline();
    }

    public void SetOutlineState(OutlineState state)
    {
        if (state == outlineState) return;
        outlineState = state;
        RefreshOutline();
    }

    private void RefreshOutline()
    {
        if (outline == null) return;

        switch (outlineState)
        {
            case OutlineState.OnGoal:
                outline.enabled = true;
                outline.color = onGoalColor;
                break;
            case OutlineState.Complete:
                outline.enabled = true;
                outline.color = completeColor;
                break;
            default:
                outline.enabled = IsSource;
                outline.color = sourceColor;
                break;
        }
    }

    private GameObject sourceEyes;

    private GameObject CreateEyes()
    {
        GameObject eyes = new GameObject("SourceEyes");
        eyes.transform.SetParent(transform, false);
        if (body == null) return eyes;

        foreach (float x in new[] { -0.16f, 0.16f })
        {
            SpriteRenderer white = CreateEyePart(eyes.transform, "Eye", new Vector3(x, 0.08f, 0f), 0.2f, Color.white, body.sortingOrder + 1);
            CreateEyePart(white.transform.parent, "Pupil", new Vector3(x, 0.06f, 0f), 0.1f, new Color(0.12f, 0.05f, 0.18f), body.sortingOrder + 2);
        }
        return eyes;
    }

    private SpriteRenderer CreateEyePart(Transform parent, string partName, Vector3 localPosition, float size, Color color, int sortingOrder)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = new Vector3(size, size, 1f);
        SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
        sr.sprite = body.sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}

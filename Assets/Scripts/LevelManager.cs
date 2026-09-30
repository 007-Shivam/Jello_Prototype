using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    [Header("Level Info (shown in the HUD)")]
    [SerializeField] private string levelTitle = "Level";
    [SerializeField] private string hint = "";

    [SerializeField] private float winMessageDelay = 0.15f;

    private GoalCell[] goals;
    private BlockBase[] blocks;
    private int satisfiedCount = -1;
    private float completedAt;

    public bool IsComplete { get; private set; }

    private bool IsLastLevel => SceneManager.GetActiveScene().buildIndex >= SceneManager.sceneCountInBuildSettings - 1;

    private void Start()
    {
        goals = FindObjectsByType<GoalCell>(FindObjectsSortMode.None);
        blocks = FindObjectsByType<BlockBase>(FindObjectsSortMode.None);
        if (goals.Length == 0) Debug.LogWarning("[Level] This level has no GoalCells, so it can never be completed.");
        CheckWin();
    }

    public void CheckWin()
    {
        if (IsComplete || goals == null) return;

        int count = 0;
        foreach (GoalCell goal in goals)
            if (goal.Evaluate()) count++;

        if (count != satisfiedCount)
        {
            satisfiedCount = count;
            Debug.Log($"[Level] Goals satisfied: {satisfiedCount}/{goals.Length}");
        }

        if (goals.Length > 0 && count == goals.Length)
        {
            IsComplete = true;
            completedAt = Time.time;
            Debug.Log($"[Level] LEVEL COMPLETE: {SceneManager.GetActiveScene().name}");
        }

        UpdateBlockOutlines();
    }

    private void UpdateBlockOutlines()
    {
        foreach (BlockBase block in blocks)
        {
            bool onGoal = false;
            foreach (GoalCell goal in goals)
                if (goal.IsSatisfied && goal.Cell == block.GridPosition) onGoal = true;

            OutlineState state = !onGoal ? OutlineState.Normal
                               : IsComplete ? OutlineState.Complete
                               : OutlineState.OnGoal;
            block.SetOutlineState(state);
        }
    }

    public void Restart()
    {
        Debug.Log("[Level] Restarting level.");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadNextLevel()
    {
        int next = IsLastLevel ? 0 : SceneManager.GetActiveScene().buildIndex + 1;
        Debug.Log($"[Level] Loading build index {next}.");
        SceneManager.LoadScene(next);
    }

    private void OnGUI()
    {
        int fontSize = Mathf.Max(14, Screen.height / 30);

        GUIStyle hud = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
        hud.normal.textColor = Color.white;
        int total = goals != null ? goals.Length : 0;
        float line = fontSize * 1.4f;
        GUI.Label(new Rect(12, 8, Screen.width, line), $"{levelTitle}    Goals: {Mathf.Max(satisfiedCount, 0)} / {total}", hud);
        GUI.Label(new Rect(12, 8 + line, Screen.width, line), hint, hud);

        GUIStyle controls = new GUIStyle(hud) { alignment = TextAnchor.LowerLeft };
        GUI.Label(new Rect(12, Screen.height - line - 8, Screen.width, line), "WASD / Arrows: move    Z: undo    R: restart", controls);

        if (!IsComplete || Time.time < completedAt + winMessageDelay) return;

        GUIStyle box = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize * 2,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        box.normal.textColor = Color.white;

        float width = Screen.width * 0.6f;
        float height = fontSize * 6f;
        Rect rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        string message = IsLastLevel
            ? "ALL LEVELS COMPLETE!\n<size=" + fontSize + ">Enter: play again from Level 1    R: restart</size>"
            : "LEVEL COMPLETE!\n<size=" + fontSize + ">Enter: next level    R: restart</size>";
        GUI.Box(rect, message, box);
    }
}

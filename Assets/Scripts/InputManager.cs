using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [SerializeField] private MovementManager movementManager;
    [SerializeField] private LevelManager levelManager;

    [Tooltip("The block the player controls. For the prototype this is a JellyBlock.")]
    [SerializeField] private BlockBase sourceBlock;

    private void Start()
    {
        if (movementManager == null) movementManager = FindAnyObjectByType<MovementManager>();
        if (levelManager == null) levelManager = FindAnyObjectByType<LevelManager>();

        if (sourceBlock == null)
        {
            Debug.LogError("[Input] No Source Block assigned on InputManager. Drag the player's block into the field.", this);
            return;
        }

        sourceBlock.MarkAsSource();
        Debug.Log($"[Input] Source block is {sourceBlock.name} at {sourceBlock.GridPosition}");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || sourceBlock == null) return;

        if (keyboard.rKey.wasPressedThisFrame && levelManager != null)
        {
            levelManager.Restart();
            return;
        }

        if (levelManager != null && levelManager.IsComplete)
        {
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                levelManager.LoadNextLevel();
            return;
        }

        if (movementManager.IsBusy) return;

        if (keyboard.zKey.wasPressedThisFrame)
        {
            movementManager.Undo();
            return;
        }

        Vector2Int direction = ReadDirection(keyboard);
        if (direction == Vector2Int.zero) return;

        movementManager.TryMove(sourceBlock, direction);
    }

    private Vector2Int ReadDirection(Keyboard keyboard)
    {
        if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) return Vector2Int.up;
        if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) return Vector2Int.down;
        if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) return Vector2Int.left;
        if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) return Vector2Int.right;
        return Vector2Int.zero;
    }
}

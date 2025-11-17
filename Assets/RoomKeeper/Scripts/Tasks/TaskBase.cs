using UnityEngine;

public abstract class TaskBase : MonoBehaviour
{
    [Header("Task Setup")]
    public GameObject panel;
    public int expReward = 10;

    private PlayerController player;

    public bool IsOpen => panel != null && panel.activeSelf;
    public bool IsCompleted { get; private set; }

    public RoomData OwningRoom { get; private set; }

    protected virtual void Start()
    {
        player = PlayerController.PlayerInstance;
        if (player == null)
        {
            Debug.LogError("TaskBase: PlayerController.playerInstance หายไป!");
        }
    }

    public virtual void Open()
    {
        if (IsCompleted || panel == null) return;
        panel.SetActive(true);
        if (player != null)
        {
            player.SetMovement(false);
        }
    }

    public virtual void Close()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
        if (player != null)
        {
            player.SetMovement(true);
        }
    }

    public void SetOwner(RoomData owner)
    {
        OwningRoom = owner;
    }

    protected void CompleteTask()
    {
        if (IsCompleted) return;

        IsCompleted = true;

        var playerProgress = PlayerProgress.Instance;
        if (playerProgress != null)
        {
            playerProgress.AddEXP(expReward);
        }
        else
        {
            Debug.LogError("PlayerProgress Instance หายไป! ไม่สามารถให้ EXP ได้");
        }

        Close();

        if (OwningRoom != null)
        {
            OwningRoom.CheckForCompletion();
        }
    }
}
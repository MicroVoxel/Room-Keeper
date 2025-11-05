using UnityEngine;

public abstract class TaskBase : MonoBehaviour
{
    [Header("Task Setup")]
    public GameObject panel;
    public int expReward = 10;

    [Header("Task Completion")]
    [Tooltip("สัญลักษณ์ภารกิจที่ควรซ่อนเมื่อภารกิจเสร็จสมบูรณ์ (กำหนดใน Inspector)")]
    public SpriteRenderer taskMarkerRenderer;

    private PlayerController player;

    public bool IsOpen => panel && panel.activeSelf;
    public bool IsCompleted { get; private set; }

    public virtual void Awake()
    {
        player = PlayerController.PlayerInstance;
        if (player == null)
        {
            Debug.LogError("TaskBase: PlayerController.playerInstance หายไป! การควบคุมการเคลื่อนไหวของผู้เล่นอาจผิดพลาด");
        }
    }

    public virtual void Open()
    {
        if (IsCompleted) return; // ถ้าจบแล้ว ไม่ต้องเปิดอีก
        if (panel) panel.SetActive(true);

        if (player != null)
        {
            player.SetMovement(false); // ปิดการเคลื่อนไหวของผู้เล่น
            //Debug.Log("OpenTask: ผู้เล่นถูกหยุดชั่วคราว");
        }
    }

    public virtual void Close()
    {
        if (panel) panel.SetActive(false);

        if (player != null)
        {
            player.SetMovement(true); // เปิดการเคลื่อนไหวของผู้เล่น
            //Debug.Log("CloseTask: ผู้เล่นกลับมาควบคุมได้");
        }
    }

    protected void CompleteTask()
    {
        if (IsCompleted) return;

        IsCompleted = true;

        if (PlayerProgress.Instance != null)
        {
            PlayerProgress.Instance.AddEXP(expReward);
        }
        else
        {
            Debug.LogError("PlayerProgress Instance หายไป! ไม่สามารถให้ EXP ได้");
        }

        Close();

        if (taskMarkerRenderer)
        {
            taskMarkerRenderer.enabled = false;
        }
    }
}

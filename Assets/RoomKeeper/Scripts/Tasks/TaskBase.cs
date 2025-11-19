using System;
using UnityEngine;

public abstract class TaskBase : MonoBehaviour
{
    [Header("Task Setup")]
    [Tooltip("UI Panel ที่จะแสดงเมื่อ Task นี้ถูกเปิด")]
    public GameObject panel;
    public int expReward = 10;

    // Event สำหรับบอกคนอื่นว่า "ทำเสร็จแล้วนะ"
    public event Action OnTaskCompleted;

    protected PlayerController player;

    // เช็ค IsOpen จากตัว gameObject ของ Task นี้ด้วย
    public bool IsOpen => gameObject.activeSelf && (panel != null && panel.activeSelf);
    public bool IsCompleted { get; private set; }

    public RoomData OwningRoom { get; private set; }

    protected virtual void Start()
    {
        player = PlayerController.PlayerInstance;

        // เนื่องจากเราจะ Disable Object นี้ตั้งแต่เกิด Start อาจจะยังไม่ทำงานจนกว่าจะ Open ครั้งแรก
        // การหา PlayerController ตรงนี้จึงเป็นการกันเหนียวเฉยๆ
    }

    public virtual void Open()
    {
        if (IsCompleted) return;

        // Re-check reference (จำเป็นมากเพราะ Start อาจยังไม่เคยรันถ้า Object ถูกปิดมาตลอด)
        if (player == null) player = PlayerController.PlayerInstance;

        // --- ส่วนสำคัญสำหรับการใช้ Logic แบบเดิม ---
        // เราต้องเปิดตัว GameObject หลัก (ตัวที่ถือ Script นี้) ก่อน ไม่งั้น Panel ที่เป็นลูกอาจจะไม่แสดง
        gameObject.SetActive(true);

        if (panel != null)
        {
            panel.SetActive(true);
        }

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

        // ปิดตัว GameObject หลักกลับไปเมื่อ Close เพื่อให้ตรงกับ Logic แบบเดิมที่ซ่อนทั้งก้อน
        gameObject.SetActive(false);
    }

    public void SetOwner(RoomData owner)
    {
        OwningRoom = owner;
    }

    protected void CompleteTask()
    {
        if (IsCompleted) return;

        IsCompleted = true;
        OnTaskCompleted?.Invoke();

        var playerProgress = PlayerProgress.Instance;
        if (playerProgress != null)
        {
            playerProgress.AddEXP(expReward);
        }

        Close();

        if (OwningRoom != null)
        {
            OwningRoom.CheckForCompletion();
        }
    }
}
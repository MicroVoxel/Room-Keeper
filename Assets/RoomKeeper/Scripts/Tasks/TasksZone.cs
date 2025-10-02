using Unity.VisualScripting;
using UnityEngine;

public class TasksZone : MonoBehaviour
{
    public TaskBase task; // อ้างไปยังคอมโพเนนต์มินิเกมตัวจริง (ซ่อนไว้ใน Canvas)
    bool inZone;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.GetComponent<PlayerController>()) return;
        if (task == null || task.IsCompleted) return;
        task.Open();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>()) { inZone = false; task.Close(); }
    }

}

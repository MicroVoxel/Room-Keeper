using UnityEngine;

public class SceneMusicController : MonoBehaviour
{
    [Header("Music Settings")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Config")]
    [Tooltip("ถ้าติ๊กถูก เพลงจะเล่นทันทีที่เข้า Scene นี้")]
    [SerializeField] private bool playOnStart = true;

    private void Start()
    {
        if (playOnStart)
        {
            PlayMusic();
        }
    }

    public void PlayMusic()
    {
        // เรียกใช้ AudioManager ที่เราทำไว้
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(musicClip, volume);
        }
        else
        {
            Debug.LogWarning("ไม่พบ AudioManager ใน Scene! อย่าลืมวาง AudioManager ไว้ที่ Scene แรกสุด");
        }
    }

    // ฟังก์ชันเสริมสำหรับปุ่ม (เช่น ปุ่ม Start Game)
    public void PlayMusicWithDelay(float delay)
    {
        Invoke(nameof(PlayMusic), delay);
    }
}
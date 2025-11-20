using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("BGM Settings")]
    [SerializeField] private AudioSource bgmSource1; // ใช้ 2 ตัวเพื่อทำ Crossfade
    [SerializeField] private AudioSource bgmSource2;
    [SerializeField] private float crossFadeDuration = 1.5f;
    private bool _isPlayingSource1 = true;

    [Header("SFX Pooling")]
    [SerializeField] private GameObject sfxSourcePrefab;
    [SerializeField] private int sfxPoolSize = 10;
    private List<AudioSource> _sfxPool;

    // Parameter Names ใน AudioMixer
    private const string MIXER_MASTER = "MasterVolume";
    private const string MIXER_MUSIC = "MusicVolume";
    private const string MIXER_SFX = "SFXVolume";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null); // ป้องกันการเป็น Child ของใครตอน DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);
            InitializeSFXPool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSFXPool()
    {
        // Safe Check: ถ้าไม่มี Prefab ให้จบฟังก์ชันเลย กัน Error
        if (sfxSourcePrefab == null)
        {
            Debug.LogWarning("[AudioManager] SFX Prefab is missing!");
            _sfxPool = new List<AudioSource>(); // กัน Null Ref เวลาเรียก GetFreeSFXSource
            return;
        }

        _sfxPool = new List<AudioSource>();
        GameObject poolHolder = new GameObject("SFX_Pool");
        poolHolder.transform.SetParent(transform);

        for (int i = 0; i < sfxPoolSize; i++)
        {
            CreateSFXSource(poolHolder.transform);
        }
    }

    private void CreateSFXSource(Transform parent)
    {
        if (sfxSourcePrefab == null) return;

        GameObject obj = Instantiate(sfxSourcePrefab, parent);
        AudioSource source = obj.GetComponent<AudioSource>();

        // Safe Check: ถ้า Prefab ไม่มี AudioSource ให้เติมเข้าไป
        if (source == null) source = obj.AddComponent<AudioSource>();

        // Safe Check: ถ้าลืมลาก Group มาใส่ ก็ปล่อยว่างไว้ (เสียงจะออก Master)
        if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;

        source.playOnAwake = false;
        obj.SetActive(false);
        _sfxPool.Add(source);
    }

    // ---------------- BGM System (Crossfade) ----------------

    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        // Safe Check: ถ้าไม่มี Source เลย ให้จบงาน
        if (bgmSource1 == null || bgmSource2 == null) return;

        AudioSource activeSource = _isPlayingSource1 ? bgmSource1 : bgmSource2;
        AudioSource newSource = _isPlayingSource1 ? bgmSource2 : bgmSource1;

        // ถ้าเพลงเดิมเล่นอยู่แล้ว ไม่ต้องทำอะไร
        if (activeSource.clip == clip && activeSource.isPlaying) return;

        // เริ่มกระบวนการ Crossfade
        StopAllCoroutines(); // Reset การ Crossfade เก่าถ้ามีการกดรัวๆ
        StartCoroutine(CrossFadeBGM(activeSource, newSource, clip, volume));
        _isPlayingSource1 = !_isPlayingSource1;
    }

    private IEnumerator CrossFadeBGM(AudioSource oldSource, AudioSource newSource, AudioClip newClip, float targetVolume)
    {
        newSource.clip = newClip;
        newSource.volume = 0;
        if (musicGroup != null) newSource.outputAudioMixerGroup = musicGroup;
        newSource.Play();

        float timer = 0f;
        float startVolume = oldSource.volume;

        while (timer < crossFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = timer / crossFadeDuration;

            newSource.volume = Mathf.Lerp(0f, targetVolume, t);
            if (oldSource.isPlaying)
                oldSource.volume = Mathf.Lerp(startVolume, 0f, t);

            yield return null;
        }

        newSource.volume = targetVolume;
        oldSource.Stop();
        oldSource.clip = null;
        oldSource.volume = 0; // Reset volume ตัวเก่าให้แน่ใจ
    }

    // ---------------- SFX System (Pooling) ----------------

    public void PlaySFX(AudioClip clip, float volume = 1f, float pitchVariance = 0f)
    {
        if (clip == null) return;

        AudioSource source = GetFreeSFXSource();
        if (source != null)
        {
            source.transform.SetParent(transform); // Reset parent just in case
            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            source.gameObject.SetActive(true);
            source.Play();
            StartCoroutine(DisableSFXSource(source, clip.length));
        }
    }

    private AudioSource GetFreeSFXSource()
    {
        if (_sfxPool == null) return null;

        foreach (var source in _sfxPool)
        {
            if (!source.gameObject.activeInHierarchy)
            {
                return source;
            }
        }

        // Optional: Expand Pool on the fly ถ้าเต็ม (สำหรับเกมเล็กๆ OK)
        // แต่ถ้าเกม Mobile แนะนำให้ return null หรือใช้ตัวที่เล่นจบนานสุดแทน เพื่อคุม Memory
        return null;
    }

    private IEnumerator DisableSFXSource(AudioSource source, float delay)
    {
        yield return new WaitForSecondsRealtime(delay + 0.1f);
        if (source != null) // เช็คว่า object ยังไม่ถูก destroy
        {
            source.Stop();
            source.gameObject.SetActive(false);
        }
    }

    // ---------------- Mixer Control ----------------

    /// <summary>
    /// รับค่าเป็น Decibel (-80 ถึง 20) โดยตรง
    /// หมายเหตุ: การแปลง Linear->Log ควรทำที่ UI เพื่อความถูกต้อง
    /// </summary>
    public void SetMixerVolume(string paramName, float dbValue)
    {
        if (audioMixer == null) return;

        // แก้ไข: รับค่า dB ตรงๆ เลย ไม่ต้องแปลง Log ซ้ำซ้อน
        // เพราะ SoundSettingsUI แปลงมาให้แล้ว
        audioMixer.SetFloat(paramName, dbValue);
    }

    public void ToggleMuteGroup(string paramName, bool isMuted)
    {
        if (audioMixer == null) return;

        float db = isMuted ? -80f : 0f;
        // ถ้าจะให้ดีควรเก็บค่า Volume ล่าสุดไว้แล้ว restore กลับมา
        // แต่ถ้าระบบ UI เราส่งค่า Realtime ตลอดอยู่แล้ว (Slider Value Change)
        // การ Mute แบบ Hard Set -80f ก็ใช้ได้ครับ
        audioMixer.SetFloat(paramName, db);
    }
}
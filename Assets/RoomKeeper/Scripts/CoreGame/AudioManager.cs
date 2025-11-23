using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer Settings")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    // [NEW] เปิดให้คนอื่นดึง Mixer Group ไปใช้ได้ (เช่น SweepingTask ที่ต้องการคุม AudioSource เอง)
    public AudioMixerGroup SFXGroup => sfxGroup;

    [Header("BGM Settings (Crossfade)")]
    [SerializeField] private AudioSource bgmSource1;
    [SerializeField] private AudioSource bgmSource2;
    [SerializeField] private float crossFadeDuration = 1.5f;
    private bool _isPlayingSource1 = true;

    [Header("SFX Pooling (Performance Optimized)")]
    [SerializeField] private GameObject sfxSourcePrefab;
    [SerializeField] private int defaultPoolSize = 10;
    [SerializeField] private int maxPoolSize = 20;

    private IObjectPool<AudioSource> _sfxPool;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
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
        if (sfxSourcePrefab == null)
        {
            Debug.LogError("[AudioManager] SFX Prefab is missing! Create an empty GameObject with AudioSource and assign it.");
            return;
        }

        _sfxPool = new ObjectPool<AudioSource>(
            createFunc: CreateSFXSource,
            actionOnGet: OnGetSFXSource,
            actionOnRelease: OnReleaseSFXSource,
            actionOnDestroy: OnDestroySFXSource,
            collectionCheck: false,
            defaultCapacity: defaultPoolSize,
            maxSize: maxPoolSize
        );
    }

    // ---------------- Pool Actions ----------------

    private AudioSource CreateSFXSource()
    {
        GameObject obj = Instantiate(sfxSourcePrefab, transform);
        AudioSource source = obj.GetComponent<AudioSource>();

        if (source == null) source = obj.AddComponent<AudioSource>();
        if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;

        source.playOnAwake = false;
        return source;
    }

    private void OnGetSFXSource(AudioSource source)
    {
        source.gameObject.SetActive(true);
    }

    private void OnReleaseSFXSource(AudioSource source)
    {
        if (source.gameObject.activeInHierarchy)
        {
            source.Stop();
        }
        source.gameObject.SetActive(false);
    }

    private void OnDestroySFXSource(AudioSource source)
    {
        Destroy(source.gameObject);
    }

    // ---------------- BGM System ----------------

    public void PlayBGM(AudioClip clip, float volume = 1f)
    {
        if (bgmSource1 == null || bgmSource2 == null) return;

        AudioSource activeSource = _isPlayingSource1 ? bgmSource1 : bgmSource2;
        AudioSource newSource = _isPlayingSource1 ? bgmSource2 : bgmSource1;

        if (activeSource.clip == clip && activeSource.isPlaying) return;

        StopAllCoroutines();
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
        oldSource.volume = 0;
    }

    // ---------------- SFX System ----------------

    public void PlaySFX(AudioClip clip, float volume = 1f, float pitchVariance = 0f)
    {
        if (clip == null || _sfxPool == null) return;

        AudioSource source = _sfxPool.Get();
        source.transform.SetParent(transform);

        source.clip = clip;
        source.volume = volume;
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);

        source.Play();

        StartCoroutine(ReturnToPoolAfterDelay(source, clip.length));
    }

    private IEnumerator ReturnToPoolAfterDelay(AudioSource source, float clipLength)
    {
        float duration = clipLength + 0.1f;
        float startTime = Time.unscaledTime;

        while (Time.unscaledTime < startTime + duration)
        {
            if (source == null) yield break;
            yield return null;
        }

        if (source != null && source.gameObject.activeInHierarchy)
        {
            _sfxPool.Release(source);
        }
    }

    public void SetMixerVolume(string paramName, float dbValue)
    {
        if (audioMixer == null) return;
        audioMixer.SetFloat(paramName, dbValue);
    }
}
using System.Collections;
using UnityEngine;

/// <summary>
/// BGMManager
/// - Singleton
/// - 두 개의 AudioSource로 크로스페이드(부드러운 전환) 구현
/// - 재생/정지/일시정지/재개, 볼륨 제어, 플레이리스트(다음/이전) 지원
/// - DontDestroyOnLoad 로 씬간 유지
/// </summary>
[DisallowMultipleComponent]
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("Initial / Playlist (optional)")]
    [Tooltip("Inspector에서 초기 재생할 트랙")]
    public AudioClip initialTrack;
    [Tooltip("플레이리스트(Inspector에 여러 트랙 등록 가능)")]
    public AudioClip[] playlist;
    [Tooltip("플레이리스트 자동재생(씬 시작 시)")]
    public bool playPlaylistOnStart = false;
    [Tooltip("플레이리스트 반복 재생 여부")]
    public bool loopPlaylist = true;

    [Header("Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Tooltip("트랙 전환/재생시 기본 페이드 시간(초)")]
    public float defaultFadeTime = 0.6f;
    [Tooltip("새 트랙을 재생할 때 자동으로 루프(Clip.loop) 설정 여부")]
    public bool defaultLoopClip = true;

    // 내부: 2개의 AudioSource를 번갈아 사용하여 crossfade
    private AudioSource[] audioSources = new AudioSource[2];
    private int activeIndex = 0; // 현재 소리가 나고 있는 소스 인덱스
    private Coroutine fadeCoroutine = null;

    // 플레이리스트 상태
    private int playlistIndex = 0;
    private bool isPaused = false;

    void Awake()
    {
        // 싱글톤 보장
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupAudioSources();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 초기 트랙 또는 플레이리스트 자동 재생
        if (playPlaylistOnStart && playlist != null && playlist.Length > 0)
        {
            playlistIndex = 0;
            PlayPlaylistTrack(playlistIndex, defaultFadeTime);
        }
        else if (initialTrack != null)
        {
            PlayBGM(initialTrack, defaultFadeTime, defaultLoopClip);
        }
    }

    void Update()
    {
        // 일시정지 중이 아니고, 플레이리스트가 설정되어 있으며, 현재 노래가 끝났을 때
        if (!isPaused && playlist != null && playlist.Length > 0)
        {
            if (!audioSources[activeIndex].isPlaying && fadeCoroutine == null)
            {
                // 루프 설정이 되어있지 않은 곡이 끝났다면 다음 곡으로
                PlayNextInPlaylist(defaultFadeTime);
            }
        }
    }

    void OnValidate()
    {
        // 인스펙터 변경 시 오디오 볼륨 동기화
        if (audioSources != null)
        {
            foreach (var a in audioSources)
            {
                if (a != null) a.volume = masterVolume;
            }
        }
    }

    private void SetupAudioSources()
    {
        for (int i = 0; i < 2; ++i)
        {
            // 자식 오브젝트로 생성하여 관리 (하이어라키가 깔끔해짐)
            GameObject child = new GameObject($"BGMSource_{i}");
            child.transform.SetParent(transform);

            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.volume = 0f;
            src.spatialBlend = 0f;
            audioSources[i] = src;
        }
    }

    /// <summary>Play a clip with optional fade and loop flag (crossfades with current).</summary>
    public void PlayBGM(AudioClip clip, float fadeTime = -1f, bool loopClip = true)
    {
        if (clip == null) return;
        if (fadeTime < 0f) fadeTime = defaultFadeTime;

        // target audio source is the inactive one
        int nextIndex = 1 - activeIndex;
        var next = audioSources[nextIndex];
        var cur = audioSources[activeIndex];

        // stop any existing fade coroutine
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        // prepare next
        next.clip = clip;
        next.loop = loopClip;
        next.volume = 0f;
        next.Play();

        fadeCoroutine = StartCoroutine(CrossfadeCoroutine(activeIndex, nextIndex, fadeTime));
        activeIndex = nextIndex;
        isPaused = false;
    }

    /// <summary>Stop playback with optional fade out. If fadeTime <= 0 -> immediate stop.</summary>
    public void StopBGM(float fadeTime = -1f)
    {
        if (fadeTime < 0f) fadeTime = defaultFadeTime;
        if (fadeTime <= 0f)
        {
            foreach (var a in audioSources) { a.Stop(); a.clip = null; }
            if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
            return;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutAllAndStopCoroutine(fadeTime));
        isPaused = false;
    }

    /// <summary>Pause current BGM (no fade).</summary>
    public void PauseBGM()
    {
        foreach (var a in audioSources) if (a.isPlaying) a.Pause();
        isPaused = true;
    }

    /// <summary>Resume if paused.</summary>
    public void ResumeBGM()
    {
        foreach (var a in audioSources) if (a.clip != null) a.UnPause();
        isPaused = false;
    }

    /// <summary>Set master volume (0..1).</summary>
    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        foreach (var a in audioSources) a.volume = masterVolume;
    }

    /// <summary>Play playlist entry by index (crossfade).</summary>
    public void PlayPlaylistTrack(int index, float fadeTime = -1f)
    {
        if (playlist == null || playlist.Length == 0) return;
        index = Mathf.Clamp(index, 0, playlist.Length - 1);
        playlistIndex = index;
        PlayBGM(playlist[playlistIndex], fadeTime, defaultLoopClip);
    }

    /// <summary>Play next track in playlist (wraps if loopPlaylist true).</summary>
    public void PlayNextInPlaylist(float fadeTime = -1f)
    {
        if (playlist == null || playlist.Length == 0) return;
        playlistIndex++;
        if (playlistIndex >= playlist.Length)
        {
            if (loopPlaylist) playlistIndex = 0;
            else { playlistIndex = playlist.Length - 1; return; }
        }
        PlayPlaylistTrack(playlistIndex, fadeTime);
    }

    /// <summary>Play previous track in playlist.</summary>
    public void PlayPrevInPlaylist(float fadeTime = -1f)
    {
        if (playlist == null || playlist.Length == 0) return;
        playlistIndex--;
        if (playlistIndex < 0)
        {
            if (loopPlaylist) playlistIndex = playlist.Length - 1;
            else { playlistIndex = 0; return; }
        }
        PlayPlaylistTrack(playlistIndex, fadeTime);
    }

    // --- Coroutines for crossfade / fades ---
    private IEnumerator CrossfadeCoroutine(int fromIndex, int toIndex, float duration)
    {
        AudioSource from = audioSources[fromIndex];
        AudioSource to = audioSources[toIndex];

        float t = 0f;
        float startFromVol = from.isPlaying ? from.volume : 0f;
        float startToVol = to.volume;

        // target final volumes scaled by masterVolume
        float targetVol = Mathf.Clamp01(masterVolume);

        // ensure to starts from zero
        to.volume = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            // fade out from, fade in to
            if (from.isPlaying) from.volume = Mathf.Lerp(startFromVol, 0f, a);
            to.volume = Mathf.Lerp(startToVol, targetVol, a);
            yield return null;
        }

        // finalize
        if (from.isPlaying) { from.Stop(); from.clip = null; from.volume = 0f; }
        to.volume = targetVol;

        fadeCoroutine = null;
        yield break;
    }

    private IEnumerator FadeOutAllAndStopCoroutine(float duration)
    {
        float t = 0f;
        float start0 = audioSources[0].isPlaying ? audioSources[0].volume : 0f;
        float start1 = audioSources[1].isPlaying ? audioSources[1].volume : 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, duration));
            audioSources[0].volume = Mathf.Lerp(start0, 0f, a);
            audioSources[1].volume = Mathf.Lerp(start1, 0f, a);
            yield return null;
        }
        foreach (var aS in audioSources) { aS.Stop(); aS.clip = null; aS.volume = 0f; }
        fadeCoroutine = null;
    }

    // Optional: convenience to set playlist at runtime
    public void SetPlaylist(AudioClip[] newList, bool autoPlayFirst = false, bool loop = true)
    {
        playlist = newList;
        loopPlaylist = loop;
        playlistIndex = 0;
        if (autoPlayFirst && playlist != null && playlist.Length > 0)
            PlayPlaylistTrack(0, defaultFadeTime);
    }
}

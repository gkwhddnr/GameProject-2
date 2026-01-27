using System.Collections;
using UnityEngine;
using Unity.AI.Assistant.Data;

/// <summary>
/// SoundManager
/// - 싱글톤으로 동작
/// - 아이템(별) 수집음, 목적지 도착음 재생 함수 제공
/// - AudioSource 자동 생성 및 PlayOneShot 사용으로 오버랩 재생 지원
/// </summary>
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Clips")]
    [Tooltip("캐릭터가 별(아이템)을 먹었을 때 재생할 음원")]
    public AudioClip collectClip;
    [Tooltip("DestinationPoint(목적지)에 도착했을 때 재생할 음원")]
    public AudioClip destinationClip;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Tooltip("PlayOneShot 시에 적용하는 기본 볼륨 스케일. 최종볼륨 = masterVolume * volumeScale")]
    [Range(0f, 1f)]
    public float baseVolumeScale = 1f;
    [Tooltip("랜덤 피치 적용 여부 (사운드가 반복될 때 변주를 주고 싶으면 켜기)")]
    public bool enableRandomPitch = true;
    [Tooltip("피치 랜덤 범위 (예: ±0.05)")]
    public float pitchVariance = 0.05f;

    [Header("AudioSource")]
    [Tooltip("SpatialBlend 0 = 2D(스테레오 UI용), 1 = 3D (월드사운드)")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;
    [Tooltip("이 오디오소스가 부가적으로 재생할 수 있는 최대 동시 사운드 수 (참고만)")]
    public int maxSimultaneous = 8;

    
    // 내부 AudioSource
    private AudioSource audioSource;

    void Awake()
    {
        // 싱글톤 처리 (간단)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // AudioSource 확보
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatialBlend;
        audioSource.volume = masterVolume;
        // 우리는 PlayOneShot을 사용하므로 loop 등은 필요 없음
    }

    void OnValidate()
    {
        // 인스펙터에서 spatialBlend/masterVolume 바꿨을 때 적용
        if (audioSource != null)
        {
            audioSource.spatialBlend = spatialBlend;
            audioSource.volume = masterVolume;
        }
    }

    /// <summary>
    /// 수집음 재생 (외부에서 호출)
    /// </summary>
    public void PlayCollect(float volumeScale = -1f)
    {
        if (collectClip == null) return;
        PlayClip(collectClip, (volumeScale < 0f) ? baseVolumeScale : volumeScale);
    }

    /// <summary>
    /// 목적지 도착음 재생 (외부에서 호출)
    /// </summary>
    public void PlayDestination(float volumeScale = -1f)
    {
        if (destinationClip == null) return;
        PlayClip(destinationClip, (volumeScale < 0f) ? baseVolumeScale : volumeScale);
    }

    /// <summary>
    /// 임의의 AudioClip을 재생 (외부 사용 가능)
    /// </summary>
    public void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || audioSource == null) return;

        float finalVol = Mathf.Clamp01(masterVolume * volumeScale);

        if (enableRandomPitch && pitchVariance > 0f)
        {
            float rand = Random.Range(-pitchVariance, pitchVariance);
            audioSource.pitch = 1f + rand; // 기본 피치(1) 기준 변주
            audioSource.PlayOneShot(clip, finalVol);
        }
        else
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(clip, finalVol);
        }
    }

    /// <summary>
    /// 외부에서 마스터 볼륨을 즉시 변경할 때 호출
    /// </summary>
    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        if (audioSource != null) audioSource.volume = masterVolume;
    }
}

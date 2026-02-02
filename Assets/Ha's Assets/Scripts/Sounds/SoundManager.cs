using UnityEngine;

[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip collectClip;
    public AudioClip destinationClip;
    public AudioClip keyClip;

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float baseVolumeScale = 1f;
    public bool enableRandomPitch = true;
    public float pitchVariance = 0.05f;

    [Header("AudioSource")]
    [Range(0f, 1f)] public float spatialBlend = 0f;
    private AudioSource audioSource;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else if (Instance != this) { Destroy(gameObject); return; }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = spatialBlend;
        audioSource.volume = masterVolume;
    }

    public void PlayKey(float volumeScale = -1f)
    {
        if (keyClip == null) return;
        PlayClip(keyClip, (volumeScale < 0f) ? baseVolumeScale : volumeScale);
    }

    public void PlayCollect(float volumeScale = -1f)
    {
        if (collectClip == null) return;
        PlayClip(collectClip, (volumeScale < 0f) ? baseVolumeScale : volumeScale);
    }

    public void PlayDestination(float volumeScale = -1f)
    {
        if (destinationClip == null) return;
        PlayClip(destinationClip, (volumeScale < 0f) ? baseVolumeScale : volumeScale);
    }

    public void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || audioSource == null) return;
        float finalVol = Mathf.Clamp01(masterVolume * volumeScale);

        if (enableRandomPitch && pitchVariance > 0f) audioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        else audioSource.pitch = 1f;

        audioSource.PlayOneShot(clip, finalVol);
    }
}
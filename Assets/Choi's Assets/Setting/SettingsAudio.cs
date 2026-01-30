using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingsAudio : MonoBehaviour
{
    public Slider bgmSlider;
    public Slider sfxSlider;

    public AudioMixer mixer;
    public string bgmParam = "BGMVol";
    public string sfxParam = "SFXVol";

    const string KEY_BGM = "SET_BGM";
    const string KEY_SFX = "SET_SFX";

    void Start()
    {
        float bgm = PlayerPrefs.GetFloat(KEY_BGM, 1f);
        float sfx = PlayerPrefs.GetFloat(KEY_SFX, 1f);

        bgmSlider.SetValueWithoutNotify(bgm);
        sfxSlider.SetValueWithoutNotify(sfx);

        Apply(bgmParam, bgm);
        Apply(sfxParam, sfx);

        bgmSlider.onValueChanged.AddListener(v =>
        {
            Apply(bgmParam, v);
            PlayerPrefs.SetFloat(KEY_BGM, v);
            PlayerPrefs.Save();
        });

        sfxSlider.onValueChanged.AddListener(v =>
        {
            Apply(sfxParam, v);
            PlayerPrefs.SetFloat(KEY_SFX, v);
            PlayerPrefs.Save();
        });
    }

    void Apply(string param, float linear)
    {
        linear = Mathf.Clamp(linear, 0.0001f, 1f);
        float db = Mathf.Log10(linear) * 20f;
        mixer.SetFloat(param, db);
    }
}

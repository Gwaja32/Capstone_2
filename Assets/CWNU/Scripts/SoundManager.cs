using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    private AudioSource bgmSource;
    private List<AudioSource> sfxSources = new List<AudioSource>();
    public int sfxChannelCount = 15; // 병렬 재생 채널 수

    [Header("BGM")]
    public AudioClip mainBGM; // 1개

    [Header("Combat SFX")]
    public AudioClip[] clashSounds;   // 4개 (가드 성공)
    public AudioClip[] parrySounds;   // 3개 (패링 성공)
    public AudioClip[] hitSounds;     // 4개 (피격)
    public AudioClip[] attackSounds;  // 4개 (공격 휘두르기)

    [Header("Movement & Game State")]
    public AudioClip[] footstepSounds; // 6개 (발걸음)
    public AudioClip victorySound;     // 1개 (승리)
    public AudioClip[] defeatSounds;   // 3개 (패배)

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitSoundSystem();
        }
        else { Destroy(gameObject); }
    }

    private void InitSoundSystem()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;

        for (int i = 0; i < sfxChannelCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            sfxSources.Add(source);
        }
    }

    // --- 재생 핵심 함수 (랜덤 피치 및 랜덤 클립) ---
    public void PlayRandomSFX(AudioClip[] clips, float volume = 1.0f)
    {
        if (clips == null || clips.Length == 0) return;

        // 1. 배열 중 랜덤하게 하나 선택
        AudioClip clip = clips[Random.Range(0, clips.Length)];

        foreach (var source in sfxSources)
        {
            if (!source.isPlaying)
            {
                // 2. 미세한 피치 조절로 자연스러움 추가 (0.9 ~ 1.1)
                source.pitch = Random.Range(0.9f, 1.1f);
                source.clip = clip;
                source.volume = volume;
                source.Play();
                return;
            }
        }
    }

    // 단일 사운드용 (승리 등)
    public void PlaySingleSFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null) return;
        PlayRandomSFX(new AudioClip[] { clip }, volume);
    }

    public void PlayBGM(float volume = 0.5f)
    {
        if (mainBGM == null) return;
        bgmSource.clip = mainBGM;
        bgmSource.volume = volume;
        bgmSource.Play();
    }
}
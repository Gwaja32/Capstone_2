using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager _instance;

    private AudioSource bgmSource;
    private List<AudioSource> sfxSources = new List<AudioSource>();
    public int sfxChannelCount = 15; // 병렬 재생 채널 수

    [Header("BGM Clips")]
    public AudioClip lobbyBGM;   // "Lobby" 씬 BGM
    public AudioClip selectBGM;  // "Select" 씬 BGM
    public AudioClip battleBGM;  // "Test" 씬 BGM

    [Header("Combat SFX")]
    public AudioClip[] clashSounds;   // 4개 (가드 성공)
    public AudioClip[] parrySounds;   // 3개 (패링 성공)
    public AudioClip[] hitSounds;     // 4개 (피격)
    public AudioClip[] attackSounds;  // 4개 (공격 휘두르기)

    [Header("Movement & Game State")]
    public AudioClip[] footstepSounds; // 6개 (발걸음)
    public AudioClip victorySound;     // 1개 (승리)
    public AudioClip[] defeatSounds;   // 3개 (패배)

    // 볼륨 설정을 저장할 내부 배율 변수들 (50 기준 비례 연산 결과 대입)
    private float bgmVolumeMultiplier = 1.0f;
    private float sfxVolumeMultiplier = 1.0f;

    // 음소거 상태 변수들
    private bool isBGMMuted = false;
    private bool isSFXMuted = false;

    private bool isInitialized = false; // 🔴 초기화 완료 여부를 체크할 플래그

    public static SoundManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<SoundManager>();

            if (_instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>("SoundManager");
                if (prefab != null)
                {
                    GameObject go = Instantiate(prefab);
                    _instance = go.GetComponent<SoundManager>();
                    Debug.Log("[SoundManager] 프리팹을 자동으로 생성했습니다.");
                }
                else
                {
                    Debug.LogError("[SoundManager] Resources 폴더에서 'SoundManager' 프리팹을 찾을 수 없습니다!");
                }
            }

            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeSceneLoad()
    {
        var trigger = Instance;
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitSoundSystem();
            isInitialized = true;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 🔴 이미 유니티 시스템(OnSceneLoaded)에 의해 첫 BGM이 재생되었을 수 있으므로, 
        // 중복 재생을 막기 위해 bgmSource에 클립이 없을 때만 안전하게 한 번 틀어줍니다.
        if (bgmSource != null && bgmSource.clip == null)
        {
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 🔴 핵심 방어선: 아직 초기화가 안 끝난 상태(유니티 첫 진입 타이밍)라면 
        // BGM 로직을 실행하지 않고 과감히 스킵 (어차피 잠시 후 Start나 다음 로직이 커버함)
        if (!isInitialized) return;

        AudioClip targetBGM = null;

        switch (scene.name)
        {
            case "Lobby": targetBGM = lobbyBGM; break;
            case "Select": targetBGM = selectBGM; break;
            case "Test": targetBGM = battleBGM; break;
        }

        PlayBGM(targetBGM);
    }

    private void PlayBGM(AudioClip clip)
    {
        // 🔴 이 부분 추가: bgmSource가 아직 메모리에 안 올라왔으면 실행을 취소하고 탈출시킴
        if (bgmSource == null)
        {
            Debug.LogWarning("[SoundManager] bgmSource가 아직 초기화되지 않아 BGM 재생을 스킵합니다. (곧 초기화됨)");
            return;
        }

        if (clip == null)
        {
            bgmSource.Stop();
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        UpdateBGMVolumeState();
        bgmSource.Play();
    }

    // --- 🎛️ SettingsManager와 완벽 호환되는 실시간 동기화 함수들 ---

    /// <summary>
    /// 설정창의 BGM 볼륨(0~100)을 받아와 50 기준 비례 연산 후 실시간 적용하는 함수
    /// </summary>
    public void SetupBGMVolume(int settingValue)
    {
        // 50일 때 1.0f(기준점), 0일 때 0f, 100일 때 2.0f 배율
        bgmVolumeMultiplier = settingValue / 50f;
        UpdateBGMVolumeState();
    }

    /// <summary>
    /// 설정창의 SFX 볼륨(0~100)을 받아와 50 기준 비례 연산 후 실시간 적용하는 함수
    /// </summary>
    public void SetupSFXVolume(int settingValue)
    {
        // 50일 때 1.0f(기준점), 0일 때 0f, 100일 때 2.0f 배율
        sfxVolumeMultiplier = settingValue / 50f;
        UpdateSFXVolumeState();
    }

    public void SetBGMMuteState(bool mute)
    {
        isBGMMuted = mute;
        UpdateBGMVolumeState();
    }

    public void SetSFXMuteState(bool mute)
    {
        isSFXMuted = mute;
        UpdateSFXVolumeState();
    }

    /// <summary>
    /// BGM 오디오 소스의 볼륨 상태를 안전하게 일괄 계산하여 갱신하는 내부 함수
    /// </summary>
    private void UpdateBGMVolumeState()
    {
        if (bgmSource == null) return;

        // BGM 자체 Mute 상태면 0, 아니면 설정된 배율(0.0f ~ 2.0f) 적용
        bgmSource.volume = isBGMMuted ? 0f : bgmVolumeMultiplier;
    }

    /// <summary>
    /// 모든 SFX 오디오 소스들의 볼륨 상태를 안전하게 일괄 계산하여 갱신하는 내부 함수
    /// </summary>
    private void UpdateSFXVolumeState()
    {
        foreach (var source in sfxSources)
        {
            if (source != null)
            {
                // SFX 자체 Mute 상태면 0, 아니면 설정된 배율(0.0f ~ 2.0f) 적용
                source.volume = isSFXMuted ? 0f : sfxVolumeMultiplier;
            }
        }
    }

    // --- ⚔️ 재생 핵심 함수 (SFX) ---
    public void PlayRandomSFX(AudioClip[] clips, float volumeMultiplier = 1.0f)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        foreach (var source in sfxSources)
        {
            if (!source.isPlaying)
            {
                source.pitch = Random.Range(0.9f, 1.1f);
                source.clip = clip;

                // 🔴 전역 SFX 배율 * 오디오 호출 시 개별 가중치 배율 계산 후 Mute 필터링
                source.volume = isSFXMuted ? 0f : (sfxVolumeMultiplier * volumeMultiplier);
                source.Play();
                return;
            }
        }
    }

    public void PlaySingleSFX(AudioClip clip, float volumeMultiplier = 1.0f)
    {
        if (clip == null) return;
        PlayRandomSFX(new AudioClip[] { clip }, volumeMultiplier);
    }
}
using UnityEngine;
using UnityEngine.SceneManagement; // 🔴 씬 전환 감지를 위해 추가
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

    // 볼륨 설정을 저장할 내부 변수들 (0.0f ~ 1.0f)
    private float bgmVolume = 0.5f;
    private float sfxVolume = 1.0f;

    // 음소거 상태 변수들
    private bool isBGMMuted = false;
    private bool isSFXMuted = false;

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

    void Awake()
    {
        // --- Awake 로직도 안전장치로 유지 ---
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitSoundSystem();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // ⚠️ [주의] 자동으로 생성되는 경우, 최초 생성 시점에는 OnSceneLoaded가 이미 지나갔을 수 있습니다.
        // 이를 방지하기 위해 생성되자마자 현재 씬의 BGM을 체크하도록 강제 실행해 줍니다.
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnEnable()
    {
        // 🔴 씬이 로드될 때마다 OnSceneLoaded 함수가 자동으로 실행되도록 리스너 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // 오브젝트가 파괴되거나 꺼질 때 리스너 해제 (메모리 누수 방지)
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

    // 🔴 [핵심] 씬 전환을 자동으로 감지하여 BGM을 틀어주는 이벤트 함수
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioClip targetBGM = null;

        // 씬 이름에 따라 틀어줄 BGM 에셋 매핑
        switch (scene.name)
        {
            case "Lobby":
                targetBGM = lobbyBGM;
                break;
            case "Select":
                targetBGM = selectBGM;
                break;
            case "Test":
                targetBGM = battleBGM;
                break;
        }

        // 선택된 BGM을 재생시킵니다.
        PlayBGM(targetBGM);
    }

    // 🔴 내부 BGM 재생 로직 (기존 씬 BGM과 같으면 중복 재생 방지)
    private void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            bgmSource.Stop();
            return;
        }

        // 이미 같은 BGM이 재생 중이라면 처음부터 다시 틀지 않고 유지합니다. (씬 이동 시 뚝 끊김 방지)
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        // 음소거 상태면 0, 아니면 설정된 BGM 볼륨 적용
        bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
        bgmSource.Play();
    }

    // --- 🎛️ 설정창 연동용 함수화 (볼륨 및 음소거) ---

    /// <summary>
    /// UI 슬라이더 등과 연동하여 BGM 볼륨을 조절하는 함수 (0.0f ~ 1.0f)
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);

        // 음소거 상태가 아닐 때만 실제 오디오 소스 볼륨을 실시간 갱신합니다.
        if (!isBGMMuted)
        {
            bgmSource.volume = bgmVolume;
        }
    }

    /// <summary>
    /// UI 슬라이더 등과 연동하여 모든 SFX 볼륨을 조절하는 함수 (0.0f ~ 1.0f)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        // 현재 재생 중인 SFX 오디오 채널들의 볼륨도 실시간으로 반영합니다.
        if (!isSFXMuted)
        {
            foreach (var source in sfxSources)
            {
                source.volume = sfxVolume;
            }
        }
    }

    /// <summary>
    /// BGM 음소거 토글 함수 (켜져 있으면 끄고, 꺼져 있으면 켬)
    /// </summary>
    public void ToggleBGMMute()
    {
        isBGMMuted = !isBGMMuted;
        bgmSource.volume = isBGMMuted ? 0f : bgmVolume;
    }

    /// <summary>
    /// SFX 음소거 토글 함수
    /// </summary>
    public void ToggleSFXMute()
    {
        isSFXMuted = !isSFXMuted;

        // 음소거 상태가 되면 현재 재생 중인 이펙트 소리도 즉시 0으로 만듭니다.
        foreach (var source in sfxSources)
        {
            source.volume = isSFXMuted ? 0f : sfxVolume;
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

                // 🔴 핵심 교정: 음소거 상태면 0, 아니면 [설정창 전역 SFX 볼륨 * 개별 호출 볼륨] 세팅
                source.volume = isSFXMuted ? 0f : (sfxVolume * volumeMultiplier);
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
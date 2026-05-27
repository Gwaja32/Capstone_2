using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

public class SettingsManager : MonoBehaviour
{
    private static SettingsManager _instance;

    public static SettingsManager Instance
    {
        get
        {
            if (_instance != null) return _instance;

            // 씬에 이미 배치된 게 있는지 먼저 검색
            _instance = FindFirstObjectByType<SettingsManager>();

            // 없다면 Resources 폴더에서 프리팹을 찾아서 자동 생성
            if (_instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>("SettingsManager");
                if (prefab != null)
                {
                    GameObject go = Instantiate(prefab);
                    _instance = go.GetComponent<SettingsManager>();
                    Debug.Log("[SettingsManager] 프리팹을 자동으로 생성했습니다.");
                }
                else
                {
                    Debug.LogError("[SettingsManager] Resources 폴더에서 'SettingsManager' 프리팹을 찾을 수 없습니다!");
                }
            }

            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeSceneLoad()
    {
        // 게임 켜지자마자 인스턴스를 한 번 호출해서 하이어라키에 생성되도록 만듦
        var trigger = Instance;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllSettings();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    [Header("UI Prefab")]
    [SerializeField] private GameObject settingsUiPrefab;
    private GameObject activeSettingsUi;

    // 데이터 프로퍼티
    public int Brightness { get; private set; } = 50;
    public int MasterVolume { get; private set; } = 100;
    public int BgmVolume { get; private set; } = 50;
    public int SfxVolume { get; private set; } = 50;
    public int Sensitivity { get; private set; } = 50;

    // Mute 여부 (T/F 대신 PlayerPrefs 저장을 위해 int 성격 유지)
    public bool IsMasterMuted { get; private set; } = false;
    public bool IsBgmMuted { get; private set; } = false;
    public bool IsSfxMuted { get; private set; } = false;

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleSettingsWindow();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyBrightness();
        ApplyControls();
        ApplyMouseAndPauseState(activeSettingsUi != null && activeSettingsUi.activeSelf);
    }

    /// <summary>
    /// 설정창 토글 및 시간 정지, 마우스 커서 제어 전역 처리
    /// </summary>
    public void ToggleSettingsWindow()
    {
        bool shouldBeActive = false;

        // 1. 이미 씬에 생성되어 DontDestroy로 살아남은 UI가 있는지 먼저 확인
        if (activeSettingsUi == null)
        {
            // 런타임에 생성된 복사본 이름인 "Settings_Canvas(Clone)"으로 검색
            activeSettingsUi = GameObject.Find("Settings_Canvas(Clone)");
        }

        // 2. 씬에 없다면 자동으로 Resources 폴더에서 로드하여 생성
        if (activeSettingsUi == null)
        {
            // 만약 인스펙터에 직접 등록해둔 프리팹(settingsUiPrefab)이 없다면 Resources에서 자동 로드
            if (settingsUiPrefab == null)
            {
                settingsUiPrefab = Resources.Load<GameObject>("Settings_Canvas");
            }

            if (settingsUiPrefab != null)
            {
                activeSettingsUi = Instantiate(settingsUiPrefab);
                activeSettingsUi.name = "Settings_Canvas(Clone)"; // 이름 고정 (서치용)
                DontDestroyOnLoad(activeSettingsUi);
                shouldBeActive = true;
            }
            else
            {
                Debug.LogError("[SettingsManager] Resources 폴더에서 'Settings_Canvas' 프리팹을 찾을 수 없습니다!");
                return;
            }
        }
        else
        {
            // 3. 이미 존재한다면 껐다 켜기 토글 작동
            shouldBeActive = !activeSettingsUi.activeSelf;
            activeSettingsUi.SetActive(shouldBeActive);

            if (!shouldBeActive)
            {
                SaveAllSettings();
                ApplyBrightness();
                ApplyControls();
            }
        }

        // 설정창 상태에 따라 마우스 커서와 게임 일시정지 로직 처리
        ApplyMouseAndPauseState(shouldBeActive);
    }

    /// <summary>
    /// 설정창 활성화 여부 및 현재 씬 종류에 맞춰 커서 상태와 Time.timeScale 조절
    /// </summary>
    private void ApplyMouseAndPauseState(bool isSettingsOpen)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (isSettingsOpen)
        {
            // [설정창이 열렸을 때] - 어떤 씬이든 공통
            Time.timeScale = 0f; // 게임 시간 정지
            Cursor.lockState = CursorLockMode.None; // 마우스 락 해제 (클릭 권한 획득)
            Cursor.visible = true; // 마우스 보여주기
        }
        else
        {
            // [설정창이 닫혔을 때]
            Time.timeScale = 1f; // 게임 시간 정상 재개

            if (currentScene == "Test")
            {
                // Test 씬이라면 인게임 모드로 복귀 (FPS/TPS 스타일)
                Cursor.lockState = CursorLockMode.Locked; // 화면 중앙에 마우스 고정
                Cursor.visible = false; // 마우스 숨기기
            }
            else
            {
                // 로비 등 다른 씬이라면 마우스가 계속 보여야 함
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    // --- 그래픽 & 컨트롤 로직 (기존 유지) ---
    public void SetBrightness(int value)
    {
        Brightness = Mathf.Clamp(value, 0, 100);
        ApplyBrightness();
        SaveSetting("Brightness", Brightness);
    }
    public void ApplyBrightness()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "Test")
        {
            // 1. 씬에 배치된 "Global Volume" 이름의 오브젝트를 직접 찾아옵니다.
            GameObject volumeObj = GameObject.Find("Global Volume");
            Volume globalVolume = null;

            if (volumeObj != null)
            {
                globalVolume = volumeObj.GetComponent<Volume>();
            }
            else
            {
                // 혹시 이름을 바꿨을 상황을 대비한 2차 검색 (타입으로 찾기)
                globalVolume = FindFirstObjectByType<Volume>();
            }

            // 2. 볼륨 컴포넌트와 프로필이 정상적으로 존재하는지 확인
            if (globalVolume != null && globalVolume.profile != null)
            {
                // 볼륨 프로필에서 Color Adjustments 컴포넌트를 추출
                if (globalVolume.profile.TryGet<ColorAdjustments>(out var colorAdjustments))
                {
                    // 💡 슬라이더 50일 때 0f (원래 어두운 기본 분위기)
                    // 슬라이더 0일 때 -2f (더 어둡게), 100일 때 +2f (화면 전체 노출 업)
                    float targetExposure = (Brightness - 50f) / 25f;

                    colorAdjustments.postExposure.value = targetExposure;

                    Debug.Log($"[SettingsManager] 씬 Global Volume 노출도(밝기) 실시간 적용 완: {targetExposure} (설정값: {Brightness})");
                }
                else
                {
                    Debug.LogWarning("[SettingsManager] Global Volume 프로필 안에 'Color Adjustments' 오버라이드가 없습니다! 에디터에서 추가해 주세요.");
                }
            }
            else
            {
                Debug.LogWarning("[SettingsManager] 씬에서 Global Volume 오브젝트나 컴포넌트를 찾을 수 없습니다.");
            }
        }
    }

    public void SetSensitivity(int value)
    {
        Sensitivity = Mathf.Clamp(value, 1, 100);
        ApplyControls();
    }
    public void ApplyControls()
    {
        TPSFixedMovement[] movements = FindObjectsByType<TPSFixedMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (movements != null && movements.Length > 0)
        {
            float calculatedSpeed = (Sensitivity <= 50) ? (3f / 50f) * Sensitivity : 3f * (Sensitivity / 50f);

            foreach (var tpsMovement in movements)
            {
                if (tpsMovement != null)
                {
                    tpsMovement.mouseDecaySpeed = calculatedSpeed;
                }
            }
            Debug.Log($"[SettingsManager] 씬 내 모든({movements.Length}개) TPSFixedMovement에 마우스 감도 주입 완: {calculatedSpeed}");
        }
    }

    // --- 오디오 & 뮤트 로직 ---
    public void SetMasterVolume(int value)
    {
        MasterVolume = Mathf.Clamp(value, 0, 100);
        ApplyAudio();
        SaveSetting("MasterVolume", MasterVolume);
    }

    public void SetBgmVolume(int value)
    {
        BgmVolume = Mathf.Clamp(value, 0, 100);
        ApplyAudio();
        SaveSetting("BgmVolume", BgmVolume);
    }

    public void SetSfxVolume(int value)
    {
        SfxVolume = Mathf.Clamp(value, 0, 100);
        ApplyAudio();
        SaveSetting("SfxVolume", SfxVolume);
    }

    public static Action<bool> OnMasterMuteChanged;
    public static Action<bool> OnBgmMuteChanged;
    public static Action<bool> OnSfxMuteChanged;

    // --- 오디오 & 뮤트 로직 (이벤트 방송 추가) ---
    public void ToggleMasterMute()
    {
        IsMasterMuted = !IsMasterMuted;
        PlayerPrefs.SetInt("IsMasterMuted", IsMasterMuted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAudio();

        // 마스터 방송 송출
        OnMasterMuteChanged?.Invoke(IsMasterMuted);
    }

    public void ToggleBgmMute()
    {
        IsBgmMuted = !IsBgmMuted;
        PlayerPrefs.SetInt("IsBgmMuted", IsBgmMuted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAudio();

        // BGM 방송 송출
        OnBgmMuteChanged?.Invoke(IsBgmMuted);
    }

    public void ToggleSfxMute()
    {
        IsSfxMuted = !IsSfxMuted;
        PlayerPrefs.SetInt("IsSfxMuted", IsSfxMuted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyAudio();

        // SFX 방송 송출
        OnSfxMuteChanged?.Invoke(IsSfxMuted);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(); // 어플리케이션 종료
#endif
    }

    public void MoveToLobby()
    {
        // 1. 현재 활성화된 씬의 이름을 가져옴
        string currentSceneName = SceneManager.GetActiveScene().name;

        // 2. 현재 씬이 "Lobby"가 아닌지 체크
        if (currentSceneName != "Lobby")
        {
            Debug.Log($"[SceneController] 현재 씬이 '{currentSceneName}'이므로 'Lobby' 씬으로 이동을 시작합니다.");

            BattleManager.Instance.currentStage = 1;

            // 💡 만약 일시정지(Time.timeScale = 0) 상태에서 이동하는 거라면 
            // 원래 시간 속도로 복구해 주는 게 안전해! (안 그러면 로비도 멈춤)
            Time.timeScale = 1f;

            // 3. 로비 씬으로 즉시 로드
            SceneManager.LoadScene("Lobby");
        }
        else
        {
            Debug.Log("[SceneController] 현재 이미 'Lobby' 씬에 머물고 있습니다.");
        }
    }

    private void ApplyAudio()
    {
        // 1. 마스터 볼륨 및 마스터 뮤트 처리 (유니티 전역 리스너 조절 : 0~1)
        if (IsMasterMuted)
        {
            AudioListener.volume = 0f;
        }
        else
        {
            AudioListener.volume = MasterVolume / 100f;
        }

        // 2. SoundManager 인스턴스가 존재할 때, 0~100 데이터 및 개별 Mute 상태를 다이렉트로 연동
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetupBGMVolume(BgmVolume);
            SoundManager.Instance.SetupSFXVolume(SfxVolume);

            SoundManager.Instance.SetBGMMuteState(IsBgmMuted);
            SoundManager.Instance.SetSFXMuteState(IsSfxMuted);
        }

        Debug.Log($"[오디오 동기화] 마스터: {AudioListener.volume}, BGM정수: {BgmVolume}, SFX정수: {SfxVolume}");
    }

    // --- 저장 및 불러오기 ---
    private void SaveAllSettings()
    {
        PlayerPrefs.SetInt("Brightness", Brightness);
        PlayerPrefs.SetInt("MasterVolume", MasterVolume);
        PlayerPrefs.SetInt("BgmVolume", BgmVolume);
        PlayerPrefs.SetInt("SfxVolume", SfxVolume);
        PlayerPrefs.SetInt("Sensitivity", Sensitivity);

        // bool을 int(0 또는 1)로 변환하여 저장
        PlayerPrefs.SetInt("IsMasterMuted", IsMasterMuted ? 1 : 0);
        PlayerPrefs.SetInt("IsBgmMuted", IsBgmMuted ? 1 : 0);
        PlayerPrefs.SetInt("IsSfxMuted", IsSfxMuted ? 1 : 0);

        PlayerPrefs.Save();
    }

    private void LoadAllSettings()
    {
        Brightness = PlayerPrefs.GetInt("Brightness", 50);
        MasterVolume = PlayerPrefs.GetInt("MasterVolume", 100);
        BgmVolume = PlayerPrefs.GetInt("BgmVolume", 50);
        SfxVolume = PlayerPrefs.GetInt("SfxVolume", 50);
        Sensitivity = PlayerPrefs.GetInt("Sensitivity", 50);

        IsMasterMuted = PlayerPrefs.GetInt("IsMasterMuted", 0) == 1;
        IsBgmMuted = PlayerPrefs.GetInt("IsBgmMuted", 0) == 1;
        IsSfxMuted = PlayerPrefs.GetInt("IsSfxMuted", 0) == 1;

        ApplyAudio();
    }

    // --- 데이터 개별 저장용 내부 헬퍼 함수 ---
    private void SaveSetting(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
    }
}
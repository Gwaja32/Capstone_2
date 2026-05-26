using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUiBridge : MonoBehaviour
{
    [System.Serializable]
    public struct CategoryTab
    {
        public Button tabButton;
        public TMP_Text buttonText;
        public GameObject gridGroup;
    }

    [Header("Category Tabs")]
    [SerializeField] private CategoryTab[] tabs;

    [Header("Close Button")]
    [SerializeField] private Button closeButton; // 닫기 버튼 할당

    [Header("Graphic UI Elements")]
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private TMP_Text brightnessValueText;

    [Header("Audio UI Elements")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private Button masterMuteButton; // 마스터 뮤트 버튼

    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TMP_Text bgmVolumeValueText;
    [SerializeField] private Button bgmMuteButton; // BGM 뮤트 버튼

    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text sfxVolumeValueText;
    [SerializeField] private Button sfxMuteButton; // SFX 뮤트 버튼

    [Header("Control UI Elements")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValueText;

    private readonly Color activeColor = Color.white;
    private readonly Color inactiveColor = new Color(0.4f, 0.4f, 0.4f);

    private void Start()
    {
        // 1. 카테고리 버튼 리스너
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].tabButton.onClick.AddListener(() => ChangeCategory(index));
        }

        // 2. 패널 닫기 버튼 리스너 등록
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => {
                SettingsManager.Instance.ToggleSettingsWindow(); // 매니저를 통해 안전하게 닫기
            });
        }

        // 3. Mute 버튼 리스너 등록
        masterMuteButton.onClick.AddListener(() => SettingsManager.Instance.ToggleMasterMute());
        bgmMuteButton.onClick.AddListener(() => SettingsManager.Instance.ToggleBgmMute());
        sfxMuteButton.onClick.AddListener(() => SettingsManager.Instance.ToggleSfxMute());

        // 4. 슬라이더 초기화 및 이벤트 등록
        InitSliderEvents();
        ChangeCategory(0);
    }

    public void ChangeCategory(int activeIndex)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = (i == activeIndex);
            tabs[i].gridGroup.SetActive(isActive);
            tabs[i].buttonText.color = isActive ? activeColor : inactiveColor;
        }
    }

    private void InitSliderEvents()
    {
        brightnessSlider.value = SettingsManager.Instance.Brightness;
        masterVolumeSlider.value = SettingsManager.Instance.MasterVolume;
        bgmVolumeSlider.value = SettingsManager.Instance.BgmVolume;
        sfxVolumeSlider.value = SettingsManager.Instance.SfxVolume;
        sensitivitySlider.value = SettingsManager.Instance.Sensitivity;

        UpdateText(brightnessValueText, brightnessSlider.value);
        UpdateText(masterVolumeValueText, masterVolumeSlider.value);
        UpdateText(bgmVolumeValueText, bgmVolumeSlider.value);
        UpdateText(sfxVolumeValueText, sfxVolumeSlider.value);
        UpdateText(sensitivityValueText, sensitivitySlider.value);

        brightnessSlider.onValueChanged.AddListener((val) => {
            int intVal = Mathf.RoundToInt(val);
            UpdateText(brightnessValueText, intVal);
            SettingsManager.Instance.SetBrightness(intVal);
        });

        masterVolumeSlider.onValueChanged.AddListener((val) => {
            int intVal = Mathf.RoundToInt(val);
            UpdateText(masterVolumeValueText, intVal);
            SettingsManager.Instance.SetMasterVolume(intVal);
        });

        bgmVolumeSlider.onValueChanged.AddListener((val) => {
            int intVal = Mathf.RoundToInt(val);
            UpdateText(bgmVolumeValueText, intVal);
            SettingsManager.Instance.SetBgmVolume(intVal);
        });

        sfxVolumeSlider.onValueChanged.AddListener((val) => {
            int intVal = Mathf.RoundToInt(val);
            UpdateText(sfxVolumeValueText, intVal);
            SettingsManager.Instance.SetSfxVolume(intVal);
        });

        sensitivitySlider.onValueChanged.AddListener((val) => {
            int intVal = Mathf.RoundToInt(val);
            if (intVal < 1) intVal = 1;
            UpdateText(sensitivityValueText, intVal);
            SettingsManager.Instance.SetSensitivity(intVal);
        });
    }

    private void UpdateText(TMP_Text textElement, float value)
    {
        if (textElement != null) textElement.text = Mathf.RoundToInt(value).ToString();
    }
}
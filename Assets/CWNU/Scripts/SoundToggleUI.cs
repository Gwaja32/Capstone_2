using UnityEngine;
using UnityEngine.UI;

public class SoundToggleUI : MonoBehaviour
{
    // 🔴 인스펙터에서 이 버튼이 무슨 버튼인지 고를 수 있게 종류를 나눔
    public enum MuteType { Master, BGM, SFX }

    [Header("버튼 유형 설정")]
    [SerializeField] private MuteType muteType;

    [Header("스프라이트 설정")]
    [SerializeField] private Sprite muteOnSprite;  // 🔇 뮤트 On 이미지
    [SerializeField] private Sprite muteOffSprite; // 🔊 뮤트 Off 이미지

    [Header("대상 이미지")]
    [SerializeField] private Image buttonImage;

    void Awake()
    {
        if (buttonImage == null) buttonImage = GetComponent<Image>();
    }

    void OnEnable()
    {
        if (SettingsManager.Instance == null) return;

        // 🔴 유형에 맞게 각각 다른 방송국 주파수를 구독하고, 초기 이미지를 설정함
        switch (muteType)
        {
            case MuteType.Master:
                SettingsManager.OnMasterMuteChanged += UpdateButtonImage;
                UpdateButtonImage(SettingsManager.Instance.IsMasterMuted);
                break;
            case MuteType.BGM:
                SettingsManager.OnBgmMuteChanged += UpdateButtonImage;
                UpdateButtonImage(SettingsManager.Instance.IsBgmMuted);
                break;
            case MuteType.SFX:
                SettingsManager.OnSfxMuteChanged += UpdateButtonImage;
                UpdateButtonImage(SettingsManager.Instance.IsSfxMuted);
                break;
        }
    }

    void OnDisable()
    {
        // 🔴 꺼질 때도 유형에 맞는 방송 주파수에서 탈퇴
        switch (muteType)
        {
            case MuteType.Master:
                SettingsManager.OnMasterMuteChanged -= UpdateButtonImage;
                break;
            case MuteType.BGM:
                SettingsManager.OnBgmMuteChanged -= UpdateButtonImage;
                break;
            case MuteType.SFX:
                SettingsManager.OnSfxMuteChanged -= UpdateButtonImage;
                break;
        }
    }

    public void OnMuteButtonClicked()
    {
        if (SettingsManager.Instance == null) return;

        // 🔴 버튼을 누르면 유형에 맞는 매니저 함수를 호출
        switch (muteType)
        {
            case MuteType.Master:
                SettingsManager.Instance.ToggleMasterMute();
                break;
            case MuteType.BGM:
                SettingsManager.Instance.ToggleBgmMute();
                break;
            case MuteType.SFX:
                SettingsManager.Instance.ToggleSfxMute();
                break;
        }
    }

    private void UpdateButtonImage(bool isMuted)
    {
        if (buttonImage == null) return;

        if (isMuted)
        {
            if (muteOnSprite != null) buttonImage.sprite = muteOnSprite;
        }
        else
        {
            if (muteOffSprite != null) buttonImage.sprite = muteOffSprite;
        }
    }
}
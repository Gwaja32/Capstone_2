using UnityEngine;
using UnityEngine.UI;

public class SettingsTriggerButton : MonoBehaviour
{
    void Start()
    {
        // 내 오브젝트에 있는 Button 컴포넌트를 가져옴
        Button btn = GetComponent<Button>();

        if (btn != null)
        {
            // 🔴 기존에 에디터에서 마우스로 연결했던 이벤트를 싹 비우고,
            // 실시간으로 메모리에 '진짜' 살아남은 SettingsManager.Instance를 찾아서 연결함
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(TriggerSettings);
        }
    }

    private void TriggerSettings()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ToggleSettingsWindow();
        }
        else
        {
            Debug.LogError("[SettingsTriggerButton] 씬에 SettingsManager 인스턴스가 존재하지 않습니다!");
        }
    }
}
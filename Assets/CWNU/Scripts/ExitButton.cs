using UnityEngine;
using UnityEngine.UI;

public class ExitButton : MonoBehaviour
{
    void Start()
    {
        // 내 오브젝트에 있는 Button 컴포넌트를 가져옴
        Button btn = GetComponent<Button>();

        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(TriggerExit);
        }
    }

    private void TriggerExit()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ExitGame();
        }
        else
        {
            Debug.LogError("[ExitButton] 씬에 SettingsManager 인스턴스가 존재하지 않습니다!");
        }
    }
}

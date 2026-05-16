using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 필요

public class LobbyManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject howToPlay_Panel; // 게임 방법 패널
    [SerializeField] private GameObject credits_Panel;    // 크레딧 패널

    /// <summary>
    /// 게임 시작 버튼 클릭 시 호출: SampleScene으로 이동
    /// </summary>
    public void GameStart()
    {
        SceneManager.LoadScene("Select");
    }

    /// <summary>
    /// 게임 방법 버튼 클릭 시 호출: 패널 활성화
    /// </summary>
    public void HowToPlay()
    {
        if (howToPlay_Panel != null)
        {
            howToPlay_Panel.SetActive(true);
        }
    }

    /// <summary>
    /// 게임 방법 닫기 버튼 클릭 시 호출: 패널 비활성화
    /// </summary>
    public void HowToPlayExit()
    {
        if (howToPlay_Panel != null)
        {
            howToPlay_Panel.SetActive(false);
        }
    }

    /// <summary>
    /// 크레딧 버튼 클릭 시 호출: 패널 활성화
    /// </summary>
    public void Credits()
    {
        if (credits_Panel != null)
        {
            credits_Panel.SetActive(true);
        }
    }

    /// <summary>
    /// 크레딧 닫기 버튼 클릭 시 호출: 패널 비활성화
    /// </summary>
    public void CreditsExit()
    {
        if (credits_Panel != null)
        {
            credits_Panel.SetActive(false);
        }
    }
}
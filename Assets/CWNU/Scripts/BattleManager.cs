using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("Data Pass")]
    public string selectedCharacterName; // 선택창에서 "Alix", "Echo", "Gorr" 등이 저장됨

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 배틀 씬으로 넘어가도 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void BattleStage()
    {
        SceneManager.LoadScene("Test");
    }


    public void SelectAlix()
    {
        selectedCharacterName = "Alix";
        BattleStage();
    }

    public void SelectEcho()
    {
        selectedCharacterName = "Echo";
        BattleStage();
    }

    public void SelectGorr()
    {
        selectedCharacterName = "Gorr";
        BattleStage();
    }
}
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectManager : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip sceneStartSound;

    [Header("VS Match UI Settings")]
    public GameObject vsMatchPanel;      // VS 연출 전체 판넬
    public float illustrationDisplayTime = 5.0f; // 일러스트 감상 시간 (초)

    [Header("Stage UI")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("My_Illust")]
    [SerializeField] private GameObject my_alix;
    [SerializeField] private GameObject my_echo;
    [SerializeField] private GameObject my_gorr;
    [SerializeField] private Button btn_alix;
    [SerializeField] private Button btn_echo;
    [SerializeField] private Button btn_gorr;

    [Header("My_Name")]
    [SerializeField] private GameObject my_name;

    [Header("Enemy_Illust")]
    [SerializeField] private GameObject enemy_alix;
    [SerializeField] private GameObject enemy_echo;
    [SerializeField] private GameObject enemy_gorr;

    [Header("Enemy_Name")]
    [SerializeField] private GameObject enemy_name;

    private bool selected = false;
    private BattleManager bm; // 🔴 배틀 매니저 캐싱용 변수 추가

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SoundManager.Instance.PlaySingleSFX(sceneStartSound);
        bm = FindFirstObjectByType<BattleManager>();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        my_alix.SetActive(false);
        my_echo.SetActive(false);
        my_gorr.SetActive(false);
        my_name.SetActive(false);

        enemy_alix.SetActive(false);
        enemy_echo.SetActive(false);
        enemy_gorr.SetActive(false);
        enemy_name.SetActive(false);

        // 🔴 [핵심 추가] 씬이 켜지자마자 현재 스테이지 상태를 판단해서 UI 텍스트를 갱신합니다.
        UpdateStageText();

        btn_alix.onClick.AddListener(OnSelectAlix);
        btn_echo.onClick.AddListener(OnSelectEcho);
        btn_gorr.onClick.AddListener(OnSelectGorr);
    }

    // 🔴 [추가] BM의 데이터를 기반으로 스테이지 텍스트를 그려주는 함수
    private void UpdateStageText()
    {
        if (stageText == null) return;

        if (bm != null)
        {
            // BM이 마지막 스테이지라고 판단했거나, 남은 적이 1명일 때 결승전으로 취급
            // (BM의 OnEnemyDefeated에서 stage가 먼저 올라가므로 안전하게 두 조건 다 체크)
            if (bm.isFinalStage || (bm.enemyModelNames != null && bm.currentStage >= bm.enemyModelNames.Count))
            {
                stageText.text = "FINAL STAGE";
                stageText.color = Color.red; // 파이널 스테이지는 강렬하게 빨간색 폰트 추천! (선택사항)
            }
            else
            {
                stageText.text = $"STAGE {bm.currentStage}";
                stageText.color = Color.white;
            }
        }
        else
        {
            // 혹시 테스트용으로 Select 씬만 단독 실행해서 BM이 없을 때를 위한 방어 코드
            stageText.text = "STAGE 1";
        }
    }

    // 🔴 [추가] 각 버튼을 눌렀을 때 데이터만 먼저 세팅하고 연출을 시작하는 함수들
    private void OnSelectAlix() { if (selected) return; selected = true; if (bm != null) bm.selectedCharacterName = "Alix"; Selected(); }
    private void OnSelectEcho() { if (selected) return; selected = true; if (bm != null) bm.selectedCharacterName = "Echo"; Selected(); }
    private void OnSelectGorr() { if (selected) return; selected = true; if (bm != null) bm.selectedCharacterName = "Gorr"; Selected(); }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HoverAlix()
    {
        if (selected) return; // 이미 선택했다면 호버 무시
        my_echo.SetActive(false);
        my_gorr.SetActive(false);
        my_alix.SetActive(true);
        my_name.SetActive(true);
        my_name.GetComponent<TextMeshProUGUI>().text = "Alix";
    }

    public void HoverEcho()
    {
        if (selected) return; // 이미 선택했다면 호버 무시
        my_gorr.SetActive(false);
        my_alix.SetActive(false);
        my_echo.SetActive(true);
        my_name.SetActive(true);
        my_name.GetComponent<TextMeshProUGUI>().text = "Echo";
    }

    public void HoverGorr()
    {
        if (selected) return; // 이미 선택했다면 호버 무시
        my_echo.SetActive(false);
        my_alix.SetActive(false);
        my_gorr.SetActive(true);
        my_name.SetActive(true);
        my_name.GetComponent<TextMeshProUGUI>().text = "Gorr";
    }

    public void Selected()
    {
        btn_alix.gameObject.SetActive(false);
        btn_echo.gameObject.SetActive(false);
        btn_gorr.gameObject.SetActive(false);
        Invoke("VS", 2f);
        Invoke("EnemySelected", 4f);
        Invoke("BattleStart", 9f);
    }

    public void VS()
    {
        if (vsMatchPanel != null) vsMatchPanel.SetActive(true);
    }


    public void EnemySelected()
    {
        if (bm == null) return;

        string pickedEnemyName = "Echo"; // 리스트가 비었을 때를 대비한 방어 코드용 기본값

        // BM의 에디터 인스펙터에 등록해 둔 적 이름 풀(List<string> enemyModelNames)을 그대로 가져옵니다.
        List<string> pool = bm.enemyModelNames;

        if (pool != null && pool.Count > 0)
        {
            int randomIndex = Random.Range(0, pool.Count);
            pickedEnemyName = pool[randomIndex]; // 랜덤으로 적 확정!
        }

        // ⭐ 중요: BM이 이 적 이름을 기억하도록 강제로 심어줍니다. (방금 BM 수정한 변수 이름)
        bm.enemyCharacterName = pickedEnemyName;

        // 확정된 이름에 맞춰 SM에 드래그해 둔 적 일러스트 오브젝트들을 켜줍니다.
        enemy_alix.SetActive(pickedEnemyName.Equals("Alix"));
        enemy_echo.SetActive(pickedEnemyName.Equals("Echo"));
        enemy_gorr.SetActive(pickedEnemyName.Equals("Gorr"));

        if (enemy_name != null)
        {
            enemy_name.GetComponent<TextMeshProUGUI>().text = pickedEnemyName;
            enemy_name.SetActive(true);
        }
    }

    public void BattleStart()
    {
        // 🔴 [구조 수정] 9초간의 모든 연출(Invoke)이 끝난 시점에 배틀 매니저에게 씬 로드를 명령합니다.
        if (bm != null)
        {
            // 씬 이동 전 UI 판넬을 정리해줍니다.
            if (vsMatchPanel != null) vsMatchPanel.SetActive(false);

            SceneManager.LoadScene(bm.battleSceneName);
        }
    }
}

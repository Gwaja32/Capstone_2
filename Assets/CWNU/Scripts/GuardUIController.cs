using UnityEngine;
using UnityEngine.UI;
using static PlayerController;

public class GuardUIController : MonoBehaviour
{
    [Header("플레이어 스크립트 연결")]
    public TPSFixedMovement activeCharacter;

    [Header("UI 이미지 연결")]
    public Image topSwordUI;
    public Image leftSwordUI;
    public Image rightSwordUI;

    [Header("UI 추적 및 크기 설정")]
    public Transform targetToFollow; // UI가 따라다닐 타겟 (플레이어)
    public Vector3 offset = new Vector3(0f, 1.5f, 0f); // 타겟 중심으로 부터의 높이 오프셋

    // [새로 추가됨] 인스펙터에 0.1 ~ 3.0 사이의 슬라이더를 만들어줍니다.
    [Range(0.1f, 3f)]
    public float uiScale = 1f;

    private Camera mainCam;

    [Header("색상 설정")]
    public Color activeColor = new Color(1f, 1f, 1f, 1f);
    public Color inactiveColor = new Color(1f, 1f, 1f, 0f);

    private CombatStance lastStance;

    void Start()
    {
        mainCam = Camera.main;
        InitializeSelectedCharacter();
        if (activeCharacter != null)
        {
            lastStance = activeCharacter.currentStance;
            UpdateUI(lastStance);
        }
    }

    private void InitializeSelectedCharacter()
    {
        // 1. 씬에 존재하는 PlayerController를 먼저 찾습니다.
        PlayerController playerController = FindFirstObjectByType<PlayerController>();

        if (playerController != null)
        {
            // 2. 플레이어 컨트롤러가 이미 자동으로 찾아둔 활성화된 캐릭터를 그대로 가져옵니다.
            if (playerController.activeCharacter != null)
            {
                activeCharacter = playerController.activeCharacter;
                Debug.Log($"✅ [GuardUI] 플레이어의 activeCharacter({activeCharacter.gameObject.name}) 연결 성공!");
            }
            else
            {
                Debug.LogWarning("⚠ PlayerController는 찾았으나, activeCharacter가 아직 할당되지 않았습니다. (호출 순서 문제일 수 있음)");
            }
        }
        else
        {
            Debug.LogWarning("⚠ 씬에서 PlayerController를 찾을 수 없습니다.");
        }
    }

    void Update()
    {
        if (activeCharacter == null)
        {
            InitializeSelectedCharacter();
            if (activeCharacter == null) return; // 그래도 없으면 리턴
        }

        // 1. 캐릭터 따라가기
        if (targetToFollow != null && mainCam != null)
        {
            Vector3 screenPos = mainCam.WorldToScreenPoint(targetToFollow.position + offset);

            if (screenPos.z > 0)
            {
                transform.position = screenPos;
            }
        }

        // 2. UI 크기 실시간 적용 (새로 추가됨)
        // Inspector 창에서 uiScale 값을 바꾸면 즉시 크기가 변합니다.
        transform.localScale = new Vector3(uiScale, uiScale, 1f);

        // 3. 가드 방향 업데이트
        if (activeCharacter == null) return;

        if (activeCharacter.currentStance != lastStance)
        {
            lastStance = activeCharacter.currentStance;
            UpdateUI(lastStance);
        }
    }

    void UpdateUI(CombatStance stance)
    {
        topSwordUI.color = inactiveColor;
        leftSwordUI.color = inactiveColor;
        rightSwordUI.color = inactiveColor;

        switch (stance)
        {
            case CombatStance.Top:
                topSwordUI.color = activeColor;
                break;
            case CombatStance.Left:
                leftSwordUI.color = activeColor;
                break;
            case CombatStance.Right:
                rightSwordUI.color = activeColor;
                break;
        }
    }
}
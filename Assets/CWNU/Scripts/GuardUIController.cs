using UnityEngine;
using UnityEngine.UI;

public class GuardUIController : MonoBehaviour
{
    [Header("플레이어 스크립트 연결")]
    public TPSFixedMovement playerMovement;

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

    private TPSFixedMovement.CombatStance lastStance;

    void Start()
    {
        mainCam = Camera.main;

        if (playerMovement != null)
        {
            lastStance = playerMovement.currentStance;
            UpdateUI(lastStance);
        }
    }

    void Update()
    {
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
        if (playerMovement == null) return;

        if (playerMovement.currentStance != lastStance)
        {
            lastStance = playerMovement.currentStance;
            UpdateUI(lastStance);
        }
    }

    void UpdateUI(TPSFixedMovement.CombatStance stance)
    {
        topSwordUI.color = inactiveColor;
        leftSwordUI.color = inactiveColor;
        rightSwordUI.color = inactiveColor;

        switch (stance)
        {
            case TPSFixedMovement.CombatStance.Top:
                topSwordUI.color = activeColor;
                break;
            case TPSFixedMovement.CombatStance.Left:
                leftSwordUI.color = activeColor;
                break;
            case TPSFixedMovement.CombatStance.Right:
                rightSwordUI.color = activeColor;
                break;
        }
    }
}
using Unity.AppUI.Core;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Camera & Target Settings")]
    public CinemachineCamera thirdPersonCam;
    public Transform cameraHolder;

    // 중앙에서 관리하는 락온 타겟 (이제 외부에서 갱신해줍니다)
    public Transform currentLockOnTarget;

    [Header("Shoulder Camera Settings")]
    public float shoulderOffset = 0.35f;
    public float camTransitionSpeed = 15f;
    private float currentXOffset = 0f;

    [Header("Current Controlled Character (자동 할당)")]
    public TPSFixedMovement activeCharacter;

    void Start()
    {
        InitializeSelectedCharacter();
    }

    private void InitializeSelectedCharacter()
    {
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager == null) return;

        string targetCharacterName = battleManager.selectedCharacterName;
        TPSFixedMovement[] allChildCharacters = GetComponentsInChildren<TPSFixedMovement>(true);

        foreach (TPSFixedMovement character in allChildCharacters)
        {
            if (character.gameObject.name == targetCharacterName)
            {
                character.gameObject.SetActive(true);
                activeCharacter = character;

                if (character.controller == null) character.controller = character.GetComponent<CharacterController>();
                if (character.anim == null) character.anim = character.GetComponent<Animator>();

                // 🔴 [삭제] 기존의 무조건 첫 적을 찾던 로직은 지웁니다. 
                // 대신 BattleManager가 적을 켤 때 이 플레이어에게 타겟을 지정해줄 것입니다.

                if (thirdPersonCam != null && cameraHolder != null)
                {
                    thirdPersonCam.Follow = this.cameraHolder;
                    thirdPersonCam.LookAt = this.cameraHolder;

                    var tracking = thirdPersonCam.GetComponent<CinemachinePositionComposer>();
                    if (tracking != null)
                    {
                        tracking.CameraDistance = 0f;
                    }
                }
            }
            else
            {
                character.gameObject.SetActive(false);
            }
        }
    }

    // 🔴 [추가] BattleManager가 새로운 적을 내보낼 때 호출해줄 타겟 갱신 함수
    public void SetLockOnTarget(Transform newTarget)
    {
        currentLockOnTarget = newTarget;

        if (currentLockOnTarget != null)
        {
            Debug.Log($"🎯 플레이어 카메라 타겟 변경 완료: {currentLockOnTarget.parent.name}의 {currentLockOnTarget.name}");
        }
        else
        {
            Debug.Log("🎯 락온 타겟 해제");
        }
    }

    void LateUpdate()
    {
        if (activeCharacter == null) return;

        // 1. [위치 동기화] 캐릭터의 위치만 그대로 따라갑니다.
        transform.position = activeCharacter.transform.position;

        // 🔴 [핵심 교정] 2. 애니메이션 회전 노이즈 제거
        // 만약 락온 타겟(적)이 있다면, 애니메이션 뼈대 흔들림 무시하고 오직 '적'만 똑바로 바라보게 만듭니다.
        if (currentLockOnTarget != null)
        {
            EnemyAI enemyComp = currentLockOnTarget.GetComponentInParent<EnemyAI>();
            if (enemyComp != null && enemyComp.currentHealth <= 0)
            {
                currentLockOnTarget = null;
                return;
            }

            Vector3 dir = currentLockOnTarget.position - transform.position;
            if (dir.sqrMagnitude > 0.09f)
            {
                dir.y = 0; // 상하 꺾임 방지
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }
        else
        {
            // 락온 타겟이 없을 때는 모델링의 'Y축(수평) 정면' 대세 회전만 가져오고, 
            // 애니메이션 좌우 트위스트 잔떨림은 Quaternion.Euler로 필터링합니다.
            Vector3 forward = transform.forward;
            forward.y = 0;
            if (forward != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }

    void Update()
    {
        if (activeCharacter == null) return;

        // 3. 자식 캐릭터의 전투 스탠스를 감시해서 목표 X 좌표 설정
        float targetX = 0f;
        if (activeCharacter.currentStance == CombatStance.Left)
            targetX = -shoulderOffset;
        else if (activeCharacter.currentStance == CombatStance.Right)
            targetX = shoulderOffset;

        // 4. 부드럽게 좌우 어깨너머로 카메라 홀더 이동
        currentXOffset = Mathf.Lerp(currentXOffset, targetX, Time.deltaTime * camTransitionSpeed);
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);

        // 5. 카메라 홀더의 자체 회전은 부모(본체) 정면과 깔끔하게 일치시킵니다.
        if (transform.forward != Vector3.zero)
        {
            cameraHolder.rotation = Quaternion.LookRotation(transform.forward);
        }
    }
}
using UnityEngine;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [Header("Camera & Target Settings")]
    public CinemachineCamera thirdPersonCam;
    public Transform cameraHolder;

    // 중앙에서 관리하는 락온 타겟
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

                EnemyAI targetEnemy = FindFirstObjectByType<EnemyAI>();
                if (targetEnemy != null)
                {
                    currentLockOnTarget = targetEnemy.transform;
                }

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

    void Update()
    {
        if (activeCharacter == null) return;

        // 1. [위치 동기화] 부모 위치를 자식 캐릭터 위치로 밀착 (CCTV 버그 방지)
        transform.position = activeCharacter.transform.position;

        // 2. [★ 회전 버그 해결의 핵심] 
        // 자식 캐릭터는 LateUpdate에서 적을 보며 회전하므로, 
        // 부모도 자식 캐릭터의 회전 값을 그대로 실시간 복사해야 카메라가 캐릭터 등 뒤를 따라 같이 회전합니다.
        transform.rotation = activeCharacter.transform.rotation;

        // 3. 자식 캐릭터의 전투 스탠스를 감시해서 목표 X 좌표 설정
        float targetX = 0f;
        if (activeCharacter.currentStance == CombatStance.Left)
            targetX = -shoulderOffset;
        else if (activeCharacter.currentStance == CombatStance.Right)
            targetX = shoulderOffset;

        // 4. 부드럽게 좌우 어깨너머로 카메라 홀더 이동
        currentXOffset = Mathf.Lerp(currentXOffset, targetX, Time.deltaTime * camTransitionSpeed);
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);

        // 5. [수정] 굳어있던 정면 벡터가 아니라, 자식을 따라 회전한 부모의 정면 50m 앞을 바라보게 만듭니다.
        Vector3 lookPoint = transform.position + transform.forward * 50f;
        cameraHolder.LookAt(lookPoint);
    }
}
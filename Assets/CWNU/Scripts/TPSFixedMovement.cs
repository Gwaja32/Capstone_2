using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.EventSystems.EventTrigger;

public class TPSFixedMovement : MonoBehaviour
{
    [Header("Targeting & Camera")]
    public CinemachineCamera thirdPersonCam;
    public Transform cameraHolder;
    public Transform targetObject;

    [Header("Aim Step Settings")]
    public float shoulderOffset = 1.5f; // 캐릭터 키(0.8)에 맞춰 조정
    public float mouseStepThreshold = 1000f;
    public float mouseDecaySpeed = 3f;
    public float camTransitionSpeed = 15f;

    [Header("Combat Stance")]
    public CombatStance currentStance = CombatStance.Top;

    [Header("Stats Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaRegenRate = 20f;
    public float guardStaminaCost = 25f;
    public float minStaminaToGuard = 10f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float guardSpeedMultiplier = 0.4f;
    public float gravity = -9.81f;

    [Header("Combat & Hit System")]
    public float attackRange = 1.2f; // 캐릭터(0.8) + 칼(0.8) 최적화
    public LayerMask enemyLayer;
    public AnimationClip topHitClip;
    public AnimationClip sideHitClip;
    public float attackDuration = 1.0f;
    public float parryDuration = 0.8f;
    public float criticalAttackedDuration = 0.3f;
    public float parryDamage = 20f;
    public bool isHitState = false;
    public bool isGuarding = false;

    [Header("Foot IK Settings")]
    public bool useFootIK = true;
    public LayerMask groundLayer;
    public float footOffset = 0.05f; // 캐릭터 크기에 맞춰 조정
    public float ikWeight = 1f;
    private float currentIKWeight = 0f;

    [Header("References")]
    public CharacterController controller;
    public Animator anim;
    public bool isInteracting = true;

    // Internal Variables
    private float mouseAccumulatorX = 0f;
    private float mouseAccumulatorY = 0f;
    private float currentXOffset = 0f;
    private Vector3 velocity;
    private int actionLayerIndex;
    private Dictionary<string, float> hitDurationDict = new Dictionary<string, float>();

    private InputActionMap actionMap;
    private InputAction moveAction, lookAction, guardAction, parryAction;

    private bool isDead = false; 
    private bool isCriticalAttacking = false;
    private Vector3 externalForce;
    

    void Awake()
    {
        actionMap = new InputActionMap("PlayerControls");
        moveAction = actionMap.AddAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        lookAction = actionMap.AddAction("Look", binding: "<Mouse>/delta");
        guardAction = actionMap.AddAction("Guard", binding: "<Mouse>/rightButton");
        parryAction = actionMap.AddAction("Parry", binding: "<Keyboard>/leftCtrl");
        actionMap.Enable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        actionLayerIndex = anim.GetLayerIndex("Action Layer");

        if (topHitClip != null) hitDurationDict["TopHit"] = topHitClip.length;
        if (sideHitClip != null) hitDurationDict["SideHit"] = sideHitClip.length;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayBGM(0.2f);
    }

    void Update()
    {
        HandleStaminaRegen();

        // 1. 행동 가능 상태일 때만 입력 처리
        if (isInteracting && !isHitState)
        {
            HandleGuardInput();
            HandleAimStep();

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                ExecuteAttack();
            }

            if (Keyboard.current.jKey.wasPressedThisFrame) // 테스트용
            {
                //GetCriticalAttackedUpper();
                //GetCriticalAttackedUnder();

            }

            if (parryAction.triggered)
            {
                ExecuteParry();  
            }
        }

        // 2. 이동 및 애니메이션 업데이트
        ApplyMovement();
        UpdateAnimationParams();
        UpdateActionLayerWeight();
    }

    void LateUpdate()
    {
        if (targetObject == null || cameraHolder == null) return;

        if (!isCriticalAttacking)
        {
            Vector3 targetDir = targetObject.position - transform.position;
            targetDir.y = 0;
            if (targetDir != Vector3.zero) {
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
            }
        }

        float targetX = 0;
        if (currentStance == CombatStance.Left) targetX = shoulderOffset;
        else if (currentStance == CombatStance.Right) targetX = -shoulderOffset;

        currentXOffset = Mathf.Lerp(currentXOffset, targetX, Time.deltaTime * camTransitionSpeed);
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);

        Vector3 lookPoint = transform.position + transform.forward * 50f;
        cameraHolder.LookAt(lookPoint);
    }

    private void HandleGuardInput()
    {
        if (guardAction.IsPressed() && currentStamina > 0)
        {
            if (!isGuarding && currentStamina >= minStaminaToGuard)
                isGuarding = true;
        }
        else isGuarding = false;

        if (currentStamina <= 0) isGuarding = false;
    }

    private void HandleStaminaRegen()
    {
        if (!isGuarding && !isHitState && currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
    }

    private void ApplyMovement()
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        if (externalForce.magnitude > 0.1f)
        {
            controller.Move(externalForce * Time.deltaTime);
        }

        // 공격/패링/피격 중일 때는 중력만 적용하고 이동은 씹음
        if (!isInteracting || isHitState)
        {
            ApplyGravityOnly();
            return;
        }

        bool isGrounded = controller.isGrounded;
        anim.SetBool("IsGrounded", isGrounded);
        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        float speedMultiplier = isGuarding ? guardSpeedMultiplier : 1.0f;

        Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x);
        if (move.magnitude > 1f) move.Normalize();

        controller.Move(move * moveSpeed * speedMultiplier * Time.deltaTime);

        anim.SetFloat("InputX", moveInput.x, 0.1f, Time.deltaTime);
        anim.SetFloat("InputY", moveInput.y, 0.1f, Time.deltaTime);

        // 평상시에도 중력 적용
        ApplyGravityOnly();

        currentIKWeight = Mathf.MoveTowards(currentIKWeight, isGrounded ? ikWeight : 0f, Time.deltaTime * 5f);
    }

    // [SOUND] 애니메이션 이벤트용 발걸음 함수
    public void PlayFootstep()
    {
        // 이동 중이고 인터랙션 중일 때만 소리 재생
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput.magnitude > 0.1f && isInteracting && !isHitState)
        {
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.footstepSounds, 0.3f);
        }
    }

    // [중요] 모든 애니메이션 파라미터를 깨끗하게 밀어버리는 함수
    private void ResetAllActionBools()
    {
        anim.SetBool("IsTopAttack", false);
        anim.SetBool("IsLeftAttack", false);
        anim.SetBool("IsRightAttack", false);
        anim.SetBool("IsParry", false);
        anim.SetBool("IsParried", false);
        anim.SetBool("IsTopHit", false);
        anim.SetBool("IsLeftHit", false);
        anim.SetBool("IsRightHit", false); 
        anim.SetBool("IsCriAtckedUp", false);
        anim.SetBool("IsCriAtckedUnder", false);
        anim.SetBool("IsCriAtckUp", false);
        anim.SetBool("IsCriAtckUnder", false);
    }

    private void ExecuteAttack()
    {
        if (!isInteracting) return;

        Vector3 rayStart = transform.position + Vector3.up * 0.45f - transform.forward * 0.5f;

        if (Physics.SphereCast(rayStart, 0.3f, transform.forward, out RaycastHit hit, attackRange, enemyLayer))
        {
            EnemyAI target = hit.collider.GetComponent<EnemyAI>();

            if (target != null && target.isParried)
            {
                StartCoroutine(CriticalAttackRoutine(target));
                return;
            }
        }

        StartCoroutine(nameof(AttackRoutine));
    }

    private IEnumerator AttackRoutine()
    {
        isInteracting = false;
        isGuarding = false;

        // [핵심] 다른 공격/피격 Bool이 켜져 있으면 Any State가 헷갈려하므로 싹 다 꺼줍니다.
        ResetAllActionBools();

        // [SOUND] 공격 휘두르기 소리 재생
        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.6f);

        string boolName = currentStance == CombatStance.Top ? "IsTopAttack" :
                          currentStance == CombatStance.Left ? "IsLeftAttack" : "IsRightAttack";

        anim.SetBool(boolName, true); // Any State가 이걸 보고 즉시 애니메이션 실행

        CheckCombatHit(15f, false);

        yield return new WaitForSeconds(attackDuration);

        anim.SetBool(boolName, false); // 애니메이션이 끝날 때쯤 꺼줌
        isInteracting = true;
    }

    // 치명 공격 루틴
    private IEnumerator CriticalAttackRoutine(EnemyAI targetEnemy)
    {
        isCriticalAttacking = true;

        isInteracting = false;
        isGuarding = false;
        ResetAllActionBools();

        // 0. 상대 체력에 따른 공격 종류를 제일 먼저 판별합니다.
        bool isUpperAttack = targetEnemy.currentHealth > 50f;

        // 1. 위치 및 회전 보정 세팅 (적 앞으로 빨려 들어가는 연출)
        float elapsed = 0f;
        float alignDuration = 0.2f; // 보정에 걸리는 시간 (짧을수록 확 빨려들어감)

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 toEnemyDir = (targetEnemy.transform.position - transform.position).normalized;
        toEnemyDir.y = 0; // 평면 기준

        // 내가 적을 바라볼 때의 '오른쪽' 방향
        Vector3 myRightDir = Vector3.Cross(Vector3.up, toEnemyDir);

        float forwardOffset = 0.3f; // 적과 유지할 앞뒤 거리
        float myRightOffset = 0f;   // '내 기준' 좌우 이동량 (음수 = 내 왼쪽, 양수 = 내 오른쪽)

        if (isUpperAttack)
        {
            // Upper: 칼끝을 맞추기 위해 내 몸을 왼쪽으로 살짝 이동
            myRightOffset = 0f;
        }
        else
        {
            // Under: 모션에 맞게 수치 조절 (필요시 변경하세요)
            myRightOffset = -0.1f;
        }

        // 목표 위치: 적의 위치 
        // - (적에서 나를 향해 forwardOffset 만큼 떨어짐) 
        // + (내 오른쪽 방향 벡터 * myRightOffset 만큼 이동)
        Vector3 targetPos = targetEnemy.transform.position
                          - (toEnemyDir * forwardOffset)
                          + (myRightDir * myRightOffset);

        targetPos.y = transform.position.y;

        // 목표 회전: 적을 마주보기
        Quaternion targetRot = Quaternion.LookRotation(toEnemyDir);

        // 이동 중 물리 충돌로 덜덜거리는 것을 방지하기 위해 잠시 컨트롤러 끔
        controller.enabled = false;

        // 2. 부드럽게 위치/회전 보정
        while (elapsed < alignDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / alignDuration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / alignDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 오차 방지를 위해 최종 스냅 후 컨트롤러 원상복구
        transform.position = targetPos;
        transform.rotation = targetRot;
        controller.enabled = true;

        // 3. 애니메이션 실행
        float kickImpactTime; //발차기까지 걸리는 시간
        float totalAnimDuration; //전체 모션 시간

        // 애니메이션 실행
        if (isUpperAttack) {
            anim.Play("CriticalAttackUpper", 0, 0f); 
            anim.Play("CriticalAttackUpper", 1, 0f);

            kickImpactTime = 2.25f;     
            totalAnimDuration = 4.35f;
        }
        else
        {
            anim.Play("CriticalAttackUnder", 0, 0f);
            anim.Play("CriticalAttackUnder", 1, 0f); 
            
            kickImpactTime = 2.45f;    
            totalAnimDuration = 6.17f;
        }

        targetEnemy.GetParried(kickImpactTime);
        yield return new WaitForSeconds(kickImpactTime);

        // 적에게 50 데미지 전달
        targetEnemy.GetCriticalAttacked(50f);

        // 박제해둔 변수를 기준으로 대기 시간 설정 (꼬임 방지)
        yield return new WaitForSeconds(totalAnimDuration - kickImpactTime);

        anim.SetBool("IsCriAtckUp", false);
        anim.SetBool("IsCriAtckUnder", false);
        isInteracting = true;

        isCriticalAttacking = false;
    }

    private void ExecuteParry()
    {
        if (!isInteracting) return;
        StartCoroutine(nameof(ParryRoutine));
    }
    private IEnumerator ParryRoutine()
    {
        isInteracting = false;
        isGuarding = false;

        // [SOUND] 패링 시도 소리 (공격 소리와 공유하거나 별도 할당 가능)
        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.5f);

        anim.SetBool("IsParry", true);
        CheckCombatHit(parryDamage, true);

        yield return new WaitForSeconds(parryDuration);

        anim.SetBool("IsParry", false);
        isInteracting = true;
    }
    public void GetParried()
    {
        // 이미 죽었거나 피격 중이면 무시 (상황에 따라 isHitState 조건은 빼셔도 됩니다)
        if (isDead) return;

        // [SOUND] 패링 당했을 때 (역경직 시작 시 둔탁한 소리)
        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.parrySounds, 1.0f);

        // 현재 진행 중인 공격 코루틴 등을 강제 중단
        StopAllCoroutines();

        // Any State에서 꼬이지 않도록 모든 애니메이션 파라미터 초기화
        ResetAllActionBools();
        isCriticalAttacking = false;

        // 역경직(패링 당함) 루틴 시작
        StartCoroutine(GetParriedRoutine());
    }
    private IEnumerator GetParriedRoutine()
    {
        UpdateCameraPositionInHit();

        isHitState = true;
        isInteracting = false;
        isGuarding = false;

        // 만약을 대비해 애니메이션 속도를 정상화
        anim.speed = 1f;

        // 1. 패링 당하는 애니메이션 시작
        anim.SetBool("IsParried", true);

        // 2. 애니메이션이 최고점에 도달할 중간 포즈까지 도달할 짧은 시간 대기
        yield return new WaitForSeconds(0.3f);

        // 3. 애니메이션 재생 느리게
        anim.speed = 0.2f;
        yield return new WaitForSeconds(1.7375f);

        // 4. 애니메이션 재생 속도 정상 복구
        anim.speed = 1f;

        // 5. 파라미터 초기화
        anim.SetBool("IsParried", false);

        // 6. 남은 분량만큼 대기
        yield return new WaitForSeconds(0.7625f);

        isHitState = false;
        isInteracting = true;
    }

    private void GetCriticalAttackedUpper()
    {
        // 이미 다른 행동 중이라면 무시
        if (!isInteracting) return;

        StartCoroutine(nameof(GetCriticalAttackedUpperRoutine));
    }

    private IEnumerator GetCriticalAttackedUpperRoutine()
    {
        isHitState = true; 
        isInteracting = false;
        isGuarding = false;
        ResetAllActionBools();

        // 1. 애니메이션 실행
        anim.CrossFadeInFixedTime("CriticalAttackedUpper", 0.1f, anim.GetLayerIndex("Base Layer"));
        anim.CrossFadeInFixedTime("CriticalAttackedUpper", 0.1f, anim.GetLayerIndex("Action Layer"));

        // 2. 물리적으로 날려보낼 방향과 힘 설정 (예: 뒤로 10의 힘으로)
        // 앞으로 날아가고 싶다면 transform.forward를 사용하세요.
        externalForce = -transform.forward * 2.5f;

        float elapsed = 0f;
        float duration = 3.1f; // 애니메이션 지속 시간

        while (elapsed < duration)
        {
            // 시간에 따라 힘을 서서히 줄여줌 (마찰력 효과)
            externalForce = Vector3.Lerp(externalForce, Vector3.zero, Time.deltaTime * 2f);

            // CharacterController로 실제 이동 적용
            // 여기서도 컨트롤러가 켜져있는지 확인하면 더 안전합니다.
            if (controller.enabled)
                controller.Move(externalForce * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        externalForce = Vector3.zero;
        isInteracting = true;
    }
    
    private void GetCriticalAttackedUnder()
    {
        // 이미 다른 행동 중이라면 무시
        if (!isInteracting) return;

        StartCoroutine(nameof(GetCriticalAttackedUnderRoutine));
    }

    private IEnumerator GetCriticalAttackedUnderRoutine()
    {
        isInteracting = false;
        isGuarding = false;
        ResetAllActionBools();

        // 1. 애니메이션 실행
        anim.CrossFadeInFixedTime("CriticalAttackedUnder", 0.1f, anim.GetLayerIndex("Base Layer"));
        anim.CrossFadeInFixedTime("CriticalAttackedUnder", 0.1f, anim.GetLayerIndex("Action Layer"));

        // 2. 물리적으로 날려보낼 방향과 힘 설정 (예: 뒤로 10의 힘으로)
        // 앞으로 날아가고 싶다면 transform.forward를 사용하세요.
        externalForce = Vector3.zero;

        float elapsed = 0f;
        float duration = 3.1f;
        float forceStartTime = 2.6f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 2.4초가 되는 "순간"에 딱 한 번만 힘을 충전함
            if (elapsed >= forceStartTime)
            {
                // 아직 힘이 충전되지 않았다면 (처음 forceStartTime 초 달성 시)
                if (externalForce == Vector3.zero)
                {
                    externalForce = -transform.forward * 1.0f;
                }
                // 힘을 서서히 줄임
                externalForce = Vector3.Lerp(externalForce, Vector3.zero, Time.deltaTime * 2f);
                // 실제 이동 적용
                controller.Move(externalForce * Time.deltaTime);
            }
            else
            {
                // 2.4초 전에는 중력만 적용해서 바닥에 붙여둠
                ApplyGravityOnly();
            }
            yield return null;
        }
        externalForce = Vector3.zero;
        isInteracting = true;
    }

    // [수정] 공격 시 적에게 자기 자신(this)을 넘겨주도록 변경
    private void CheckCombatHit(float damage, bool isParry)
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.45f;
        RaycastHit hit;
        if (Physics.SphereCast(rayStart, 0.2f, transform.forward, out hit, attackRange, enemyLayer))
        {
            EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                if (isParry)
                {
                    if (enemy.isAttacking)
                    {
                        enemy.GetParried();
                        return;
                    }
                }
                else
                {
                    enemy.TakeDamage(currentStance, this);
                }
            }
        }
    }

    private IEnumerator RecoilRoutine(float duration)
    {
        isInteracting = false;
        yield return new WaitForSeconds(duration);
        isInteracting = true;
    }

    // [수정] 피격 및 가드 판정 로직
    public void TakeDamage(CombatStance attackerStance, EnemyAI attacker)
    {
        if (isDead || isHitState) return;

        // 1. 가드 성공 판정
        if (isGuarding && CanBlock(attackerStance))
        {
            // [SOUND] 가드 성공 (Clash) 소리 재생
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.clashSounds, 0.8f);

            currentStamina -= guardStaminaCost; // 스테미나 많이 깎음

            // [메리트] 나를 때린 적에게 역경직 부여
            if (attacker != null)
            {
                attacker.StartCoroutine("RecoilRoutine", 0.8f); // 적 0.8초간 멍 때림
            }

            // 스테미나 오링 시 가드 강제 해제
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                isGuarding = false;
            }
            return;
        }

        // 2. 가드 실패 시 (생짜 피격)
        // [SOUND] 피격(Hit) 소리 재생
        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.hitSounds, 0.9f);

        currentHealth -= 20f;
        if (currentHealth <= 0) { Die(); return; }

        StopAllCoroutines();
        ResetAllActionBools();
        isCriticalAttacking = false;

        string hitBool = (attackerStance == CombatStance.Top) ? "IsTopHit" : (attackerStance == CombatStance.Left) ? "IsRightHit" : "IsLeftHit";
        float duration = (attackerStance == CombatStance.Top) ? hitDurationDict.GetValueOrDefault("TopHit", 0.5f) : hitDurationDict.GetValueOrDefault("SideHit", 0.5f);

        duration = duration * 0.5f;

        StartCoroutine(PlayerHitRoutine(hitBool, duration));
    }

    private void Die()
    {
        isDead = true;
        anim.SetBool("IsDead", true); // Any State에서 사망 모션 실행

        // [SOUND] 사망/패배 소리 재생
        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.defeatSounds, 1.0f);

        // 조작 및 인터랙션 영구 정지
        isInteracting = false;
        StopAllCoroutines();
        actionMap.Disable();

        // 사망 후 처리 루틴 시작
        StartCoroutine(DeadSequence());
    }

    private IEnumerator DeadSequence()
    {
        // 1. 쓰러지는 모션을 감상할 시간 (2~3초)
        yield return new WaitForSeconds(3.0f);

        // 2. 여기에 UI를 띄우는 코드를 넣으세요.
        // UIManager.Instance.ShowGameOverUI(); 

        // 3. 만약 즉시 재시작하고 싶다면 (씬 다시 로드)
        // UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        // 4. 메인 메뉴로 보내고 싶다면
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private bool CanBlock(CombatStance attackerStance)
    {
        if (attackerStance == CombatStance.Top && currentStance == CombatStance.Top) return true;
        if (attackerStance == CombatStance.Left && currentStance == CombatStance.Right) return true;
        if (attackerStance == CombatStance.Right && currentStance == CombatStance.Left) return true;
        return false;
    }

    private IEnumerator PlayerHitRoutine(string boolName, float duration)
    {
        UpdateCameraPositionInHit();

        isHitState = true;
        isInteracting = false;
        isGuarding = false;

        anim.SetBool(boolName, true); // Any State가 이 Bool을 보고 즉시 피격 모션 재생
        yield return new WaitForSeconds(duration);

        anim.SetBool(boolName, false);
        isHitState = false;
        isInteracting = true;
    }

    private void HandleAimStep()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        if (lookInput.sqrMagnitude > 0.001f)
        {
            mouseAccumulatorX += lookInput.x;
            mouseAccumulatorY += lookInput.y;
            if (Mathf.Abs(mouseAccumulatorX) > mouseStepThreshold || Mathf.Abs(mouseAccumulatorY) > mouseStepThreshold)
            {
                DetermineStance();
                mouseAccumulatorX = 0; mouseAccumulatorY = 0;
            }
        }
        else
        {
            mouseAccumulatorX = Mathf.Lerp(mouseAccumulatorX, 0, Time.deltaTime * mouseDecaySpeed);
            mouseAccumulatorY = Mathf.Lerp(mouseAccumulatorY, 0, Time.deltaTime * mouseDecaySpeed);
        }
    }

    private void DetermineStance()
    {
        if (mouseAccumulatorY > mouseStepThreshold && mouseAccumulatorY > Mathf.Abs(mouseAccumulatorX))
            currentStance = CombatStance.Top;
        else if (Mathf.Abs(mouseAccumulatorX) > mouseStepThreshold)
            currentStance = (mouseAccumulatorX < 0) ? CombatStance.Left : CombatStance.Right;
    }

    private void UpdateAnimationParams()
    {
        anim.SetBool("IsGuarding", isGuarding);
        anim.SetFloat("Stance", (float)currentStance);
    }

    private void UpdateActionLayerWeight()
    {
        // [수정] 평상시에도 Action Layer의 애니메이션(Idle 등)이 보여야 하므로 
        // 특수한 상황이 아니더라도 기본적으로 1을 유지하도록 변경하거나, 
        // 하위 레이어(Base)가 보이길 원한다면 조건을 세밀하게 조정해야 합니다.

        // 만약 Action Layer가 상시 노출되어야 하는 레이어라면 그냥 1f로 고정해도 됩니다.
        float targetWeight = 1f;

        // 만약 특정 상황에서만 Action Layer가 덮어씌워야 한다면 기존 로직을 유지하되,
        // Idle이 Base Layer에 있다면 현재 상태가 정상입니다.
        // 하지만 Idle이 Action Layer에 있다면 아래처럼 항상 1로 유지하세요.
        anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(anim.GetLayerWeight(actionLayerIndex), targetWeight, Time.deltaTime * 20f));
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useFootIK || anim == null) return;
        anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, currentIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, currentIKWeight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, currentIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, currentIKWeight);
        if (currentIKWeight > 0.01f) { ApplyFootIK(AvatarIKGoal.LeftFoot); ApplyFootIK(AvatarIKGoal.RightFoot); }
    }

    private void ApplyFootIK(AvatarIKGoal goal)
    {
        Vector3 anklePos = anim.GetIKPosition(goal);
        if (Physics.Raycast(new Ray(anklePos + Vector3.up * 1f, Vector3.down), out RaycastHit hit, 2f, groundLayer))
        {
            Vector3 targetPos = hit.point; targetPos.y += footOffset;
            if (anklePos.y > targetPos.y) targetPos.y = anklePos.y;
            anim.SetIKPosition(goal, targetPos);
            anim.SetIKRotation(goal, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal));
        }
    }

    private void ApplyGravityOnly()
    {
        if (!controller.enabled) return;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateCameraPositionInHit()
    {
        if (cameraHolder == null) return;

        float targetX = 0;
        if (currentStance == CombatStance.Left) targetX = shoulderOffset;
        else if (currentStance == CombatStance.Right) targetX = -shoulderOffset;

        currentXOffset = targetX;
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);

        // 캐릭터 정면 50m 앞을 바라보게 설정
        cameraHolder.LookAt(transform.position + transform.forward * 50f);
    }

    void OnDisable() => actionMap.Disable();
}
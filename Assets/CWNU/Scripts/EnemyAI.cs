using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Chase, Attack, Hit, Dead }
    public enum MovementState { Idle, StrafeLeft, StrafeRight }

    [Header("Character Profile")]
    public CharacterData characterData;

    [HideInInspector] public float maxHealth;
    [HideInInspector] public float currentHealth;
    [HideInInspector] public float maxStamina;
    [HideInInspector] public float currentStamina;
    [HideInInspector] public float staminaRegenRate;
    [HideInInspector] public float guardStaminaCost;
    [HideInInspector] public float minStaminaToGuard;
    [HideInInspector] public float moveSpeed;
    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float attackRange;
    [HideInInspector] public AnimationClip topHitClip;
    [HideInInspector] public AnimationClip sideHitClip;
    [HideInInspector] public float attackDuration;

    [Header("State Settings")]
    public AIState currentState = AIState.Idle;
    public CombatStance currentStance = CombatStance.Top;

    [Header("Detection & Layer Mask")]
    public Transform playerTransform;
    public LayerMask playerLayer;
    public float chaseRange = 10f;

    [Header("Movement Settings")]
    public float gravity = -9.81f;

    [Header("Strafing Settings")]
    public float optimalRange = 2.5f;
    private MovementState currentMoveState = MovementState.Idle;
    private float lastMoveStateChangeTime;
    private float moveStateChangeInterval = 2f;

    [Header("Psychological Combat Settings")]
    public float stanceHoldDuration = 0.5f;

    [Header("Combat Settings")]
    public float attackCooldown = 2.5f;
    public float criticalAttackDamage = 33.0f;
    private float lastAttackTime;
    public bool isGuarding = false;
    public bool isParried = false;
    public bool isAttacking = false;
    public bool isCriticalAttacking = false;
    public bool canExecuteCritical = false;

    [Header("Layer Settings")]
    public string actionLayerName = "Action Layer";
    private int actionLayerIndex;
    public float weightLerpSpeed = 20f;

    [Header("References")]
    public CharacterController controller;
    [HideInInspector] public Animator anim;
    [HideInInspector] public Transform targetEnemyTransform;

    private Dictionary<string, float> hitDurationDict = new Dictionary<string, float>();
    private Vector3 velocity;
    private bool isDead = false;
    private bool isInteracting = false;

    void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
            if (controller == null) controller = GetComponentInParent<CharacterController>();
        }

        velocity = Vector3.zero;
        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        Transform rootTransform = transform.root;
        Transform targetObj = rootTransform.FindDeepChild("Target_Enemy");
        if (targetObj != null) targetEnemyTransform = targetObj;
    }

    void Start()
    {
        if (characterData != null)
        {
            moveSpeed = characterData.moveSpeed;
            maxHealth = characterData.maxHealth;
            currentHealth = maxHealth;
            maxStamina = characterData.maxStamina;
            currentStamina = maxStamina;
            staminaRegenRate = characterData.staminaRegenRate;
            attackRange = characterData.attackRange;
            attackDuration = characterData.attackDuration;

            guardStaminaCost = 20f;
            minStaminaToGuard = 10f;
            rotationSpeed = 100f;

            if (characterData.topHitClip != null) hitDurationDict["TopHit"] = characterData.topHitClip.length;
            if (characterData.sideHitClip != null) hitDurationDict["SideHit"] = characterData.sideHitClip.length;
        }

        if (BattleManager.Instance != null)
        {
            int stage = BattleManager.Instance.currentStage;
            float difficultyMultiplier = 1f + ((stage - 1) * 0.15f);

            maxHealth *= difficultyMultiplier;
            moveSpeed *= (1f + ((stage - 1) * 0.05f));
            staminaRegenRate *= difficultyMultiplier;

            attackCooldown = Mathf.Max(1.0f, attackCooldown - ((stage - 1) * 0.3f));
            stanceHoldDuration = Mathf.Max(0.3f, stanceHoldDuration - ((stage - 1) * 0.05f));

            Debug.Log($"⚔️ [{stage}스테이지 난이도 보정] 체력:{maxHealth:F0} / 공격쿨타임:{attackCooldown:F1}초 / 자세유지:{stanceHoldDuration:F2}초 / 패링 확률 : {100 * (((stage - 1) * 0.05f) + 0.10)}%");
        }

        currentHealth = maxHealth;
        currentStamina = maxStamina;

        if (anim != null)
            actionLayerIndex = anim.GetLayerIndex(actionLayerName);
    }

    public void PlayFootstep()
    {
        if (currentState == AIState.Chase && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.footstepSounds, 0.2f);
        }
    }

    void Update()
    {
        if (isDead || anim == null || isCriticalAttacking) return;

        if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
        {
            ApplyGravity();
        }
        HandleStaminaRegen();

        if (playerTransform == null)
        {
            GameObject pObj = GameObject.FindWithTag("Player");
            if (pObj != null)
            {
                Transform pTarget = pObj.transform.FindDeepChild("Camera_Target");
                playerTransform = (pTarget != null && pTarget.gameObject.activeInHierarchy) ? pTarget : pObj.transform;
            }
        }

        if (playerTransform == null) return;

        if (currentState == AIState.Hit || isInteracting)
        {
            UpdateLayerWeights();
            if (currentState == AIState.Hit || isAttacking) isGuarding = false;

            HandleRotation(playerTransform.position);
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case AIState.Idle:
                if (distance <= chaseRange) currentState = AIState.Chase;
                break;

            case AIState.Chase:
                HandleRotation(playerTransform.position);
                if (distance < 3f && currentStamina > minStaminaToGuard && Random.value < 0.01f) isGuarding = true;

                if (distance <= attackRange) currentState = AIState.Attack;
                else if (distance > chaseRange) currentState = AIState.Idle;
                else MoveTowardsPlayer();
                break;

            case AIState.Attack:
                HandleRotation(playerTransform.position);
                if (distance > attackRange + 0.5f && !isInteracting)
                {
                    currentState = AIState.Chase;
                }
                else
                {
                    if (!isInteracting)
                    {
                        MoveTowardsPlayer();
                        HandleAttackPattern();
                    }
                }
                break;
        }

        UpdateAnimationParams();
        UpdateLayerWeights();
    }

    private void HandleStaminaRegen()
    {
        if (!isGuarding && currentState != AIState.Hit && currentStamina < maxStamina) currentStamina += staminaRegenRate * Time.deltaTime;
    }

    private void MoveTowardsPlayer()
    {
        if (controller == null || playerTransform == null || !controller.enabled || !controller.gameObject.activeInHierarchy) return;

        Vector3 directionToPlayer = (playerTransform.position - controller.transform.position);
        directionToPlayer.y = 0;
        float distance = directionToPlayer.magnitude;
        Vector3 forwardDir = directionToPlayer.normalized;

        float closeCombatRange = attackRange + 0.5f;

        if (distance > closeCombatRange)
        {
            currentMoveState = MovementState.Idle;
            controller.Move(forwardDir * moveSpeed * Time.deltaTime);
            return;
        }

        if (Time.time >= lastMoveStateChangeTime + moveStateChangeInterval)
        {
            float rand = Random.value;
            if (rand < 0.40f) currentMoveState = MovementState.Idle;
            else if (rand < 0.70f) currentMoveState = MovementState.StrafeLeft;
            else currentMoveState = MovementState.StrafeRight;

            moveStateChangeInterval = Random.Range(1.0f, 2.0f);
            lastMoveStateChangeTime = Time.time;
        }

        Vector3 finalMoveDirection = Vector3.zero;
        Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;

        float approachWeight = 0.1f;
        if (distance < attackRange - 0.2f) approachWeight = -0.4f;

        switch (currentMoveState)
        {
            case MovementState.Idle:
                finalMoveDirection = forwardDir * approachWeight;
                break;
            case MovementState.StrafeLeft:
                finalMoveDirection = (forwardDir * approachWeight) + (rightDir * -1f);
                break;
            case MovementState.StrafeRight:
                finalMoveDirection = (forwardDir * approachWeight) + (rightDir * 1f);
                break;
        }

        finalMoveDirection.Normalize();

        if (finalMoveDirection != Vector3.zero)
        {
            float currentSpeed = (currentMoveState == MovementState.Idle) ? moveSpeed : moveSpeed * 0.7f;
            controller.Move(finalMoveDirection * currentSpeed * Time.deltaTime);
        }
    }

    private void HandleRotation(Vector3 targetPos)
    {
        if (controller == null || !controller.enabled) return;

        Transform rootBody = controller.transform;
        Vector3 dir = targetPos - rootBody.position;
        dir.y = 0;

        if (dir != Vector3.zero)
        {
            rootBody.rotation = Quaternion.Slerp(rootBody.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed);
        }
    }

    private void HandleAttackPattern()
    {
        if (!isInteracting && Time.time >= lastAttackTime + attackCooldown)
        {
            ExecuteAttack();
        }
    }

    private IEnumerator AttackRoutine()
    {
        currentStance = (CombatStance)Random.Range(0, 3);
        UpdateAnimationParams();

        float elapsed = 0f;
        while (elapsed < stanceHoldDuration)
        {
            elapsed += Time.deltaTime;
            HandleRotation(playerTransform.position);

            Vector3 dirToPlayer = (playerTransform.position - transform.position);
            dirToPlayer.y = 0;
            float currentDistance = dirToPlayer.magnitude;

            if (currentDistance > attackRange && controller != null && controller.enabled)
            {
                anim.SetFloat("InputY", 1.0f);
                anim.SetFloat("InputX", 0f);
                controller.Move(dirToPlayer.normalized * moveSpeed * Time.deltaTime);
            }

            UpdateAnimationParams();
            yield return null;
        }

        isInteracting = true; isGuarding = false; isAttacking = true; 
        currentStance = (CombatStance)Random.Range(0, 3);
        currentStamina -= 15f;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.6f);

        string triggerName = currentStance == CombatStance.Top ? "IsTopAttack" : currentStance == CombatStance.Left ? "IsLeftAttack" : "IsRightAttack";
        anim.SetTrigger(triggerName);

        float windUpTime = 0.4f;
        yield return new WaitForSeconds(windUpTime);

        CheckCombatHit();

        yield return new WaitForSeconds(attackDuration - windUpTime);

        isInteracting = false; isAttacking = false;
    }

    private void ExecuteAttack()
    {
        if (isInteracting || currentStamina < 20f) return;

        lastAttackTime = Time.time;
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator CriticalAttackRoutine(TPSFixedMovement targetPlayer)
    {
        // 1. AI 측 상시 차단 플래그 즉시 확정
        isCriticalAttacking = true;
        currentState = AIState.Attack;
        isInteracting = true;
        isAttacking = true;
        isGuarding = false;

        targetPlayer.isHitState = false;

        bool isUpperAttack = targetPlayer.getCurrentHealth() > criticalAttackDamage;

        // [개선] CharacterController를 확실하게 끄고 물리 속도 초기화
        if (controller != null)
        {
            controller.enabled = false;
        }
        velocity = Vector3.zero;

        float elapsed = 0f;
        float alignDuration = 0.2f;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // 0.2초 정렬 이동 (보정된 실시간 위치 추적 방식)
        while (elapsed < alignDuration)
        {
            if (targetPlayer == null) break;

            // 매 프레임 플레이어의 위치를 기반으로 방향을 다시 계산하여 정확도 극대화
            Vector3 toPlayerDir = (targetPlayer.transform.position - transform.position).normalized;
            toPlayerDir.y = 0;

            // 🟢 [수정] 올바른 우측 벡터 연산 (앞 -> 위 = 오른쪽)
            Vector3 myRightDir = Vector3.Cross(toPlayerDir, Vector3.up).normalized;

            float forwardOffset = 0.3f;
            // 필요에 따라 오프셋 조정 (오른손잡이 무기 궤적 맞춤용)
            float myRightOffset = isUpperAttack ? 0f : 0.1f;

            // 목표 위치 실시간 갱신
            Vector3 currentTargetPos = targetPlayer.transform.position
                                      - (toPlayerDir * forwardOffset)
                                      + (myRightDir * myRightOffset);
            currentTargetPos.y = transform.position.y; // 땅 높이 유지

            Quaternion currentTargetRot = Quaternion.LookRotation(toPlayerDir);

            // 부드럽게 보간 이동 및 회전
            transform.position = Vector3.Lerp(startPos, currentTargetPos, elapsed / alignDuration);
            transform.rotation = Quaternion.Slerp(startRot, currentTargetRot, elapsed / alignDuration);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 최종 위치 고정 및 완벽히 타겟 바라보기
        if (targetPlayer != null)
        {
            Vector3 finalDir = (targetPlayer.transform.position - transform.position).normalized;
            finalDir.y = 0;
            if (finalDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(finalDir);
        }

        float originalActionWeight = anim.GetLayerWeight(actionLayerIndex);
        anim.SetLayerWeight(actionLayerIndex, 0f);

        float kickImpactTime;
        float totalAnimDuration;

        if (isUpperAttack)
        {
            anim.SetTrigger("IsCriAtckUp");
            kickImpactTime = 2.25f;
            totalAnimDuration = 4.35f;
        }
        else
        {
            anim.SetTrigger("IsCriAtckUnder");
            kickImpactTime = 2.45f;
            totalAnimDuration = 6.17f;
        }

        targetPlayer.GetParried(kickImpactTime);
        yield return new WaitForSeconds(kickImpactTime);

        if (targetPlayer != null)
        {
            targetPlayer.GetCriticalAttacked(criticalAttackDamage, this);
        }

        anim.SetLayerWeight(actionLayerIndex, originalActionWeight);

        // [개선] 애니메이션 처리가 끝난 후 안전하게 컨트롤러 재활성화
        if (controller != null) controller.enabled = true;

        yield return new WaitForSeconds(totalAnimDuration - kickImpactTime);

        if (targetPlayer != null) targetPlayer.isHitState = false;
        canExecuteCritical = false;

        isInteracting = false;
        isAttacking = false;
        isCriticalAttacking = false;
        currentState = AIState.Chase;
    }

    private string GetAttackTriggerName(CombatStance stance)
    {
        switch (stance)
        {
            case CombatStance.Top: return "IsTopAttack";
            case CombatStance.Left: return "IsLeftAttack";
            case CombatStance.Right: return "IsRightAttack";
            default: return "IsTopAttack";
        }
    }

    private void CheckCombatHit()
    {
        Vector3 start = transform.position + Vector3.up * 0.45f;
        RaycastHit hit;
        if (Physics.SphereCast(start, 0.2f, transform.forward, out hit, attackRange, playerLayer))
        {
            TPSFixedMovement p = hit.collider.GetComponent<TPSFixedMovement>();
            if (p != null) p.TakeDamage(currentStance, this);
        }
    }

    public void TakeDamage(CombatStance attackerStance, TPSFixedMovement attacker)
    {
        if (isDead || isCriticalAttacking || currentState == AIState.Dead) return;

        int stage = BattleManager.Instance != null ? BattleManager.Instance.currentStage : 1;
        float parryChance = ((stage - 1) * 0.05f) + 0.10f;

        if (Random.value < parryChance && attacker != null)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.parrySounds, 1.0f);

            attacker.GetParried();
            lastAttackTime = 0f;

            canExecuteCritical = true;
            currentState = AIState.Attack;

            isInteracting = true;
            isAttacking = false;
            isGuarding = false;

            // 🟢 [수정] 즉시 처형하는 대신, 시간차를 두는 안전한 코루틴으로 위임합니다.
            StopAllCoroutines();
            StartCoroutine(DelayedCriticalFromParryRoutine(attacker));
            return;
        }

        if (isGuarding && CanBlock(attackerStance))
        {
            currentStamina -= guardStaminaCost;

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.clashSounds, 0.7f);

            if (attacker != null) attacker.StartCoroutine("RecoilRoutine", 1.0f);
            if (currentStamina <= 0) { currentStamina = 0; isGuarding = false; }
            return;
        }

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.hitSounds, 0.8f);

        currentHealth -= 20f;
        if (currentHealth <= 0) { Die(); return; }

        StopAllCoroutines();
        isInteracting = false;

        string hitBool = (attackerStance == CombatStance.Top) ? "IsTopHit" : (attackerStance == CombatStance.Left) ? "IsRightHit" : "IsLeftHit";
        float duration = (attackerStance == CombatStance.Top) ? hitDurationDict.GetValueOrDefault("TopHit", 0.5f) : hitDurationDict.GetValueOrDefault("SideHit", 0.5f);

        StartCoroutine(HitRoutine(hitBool, duration));
    }

    private IEnumerator DelayedCriticalFromParryRoutine(TPSFixedMovement targetPlayer)
    {
        anim.SetTrigger("IsParry");

        yield return new WaitForSeconds(0.8f);

        if (targetPlayer != null && !isDead)
        {
            // 대기가 끝났으니 잠금을 일시 해제하고 정식 처형 함수로 진입시킵니다.
            isInteracting = false;
            ExecuteCriticalFromParry(targetPlayer);
        }
        else
        {
            // 만에 하나 플레이어가 사라졌거나 처리 도중 사망했다면 상태 해제
            isInteracting = false;
            currentState = AIState.Chase;
        }
    }

    private void ExecuteCriticalFromParry(TPSFixedMovement targetPlayer)
    {
        if (targetPlayer == null || isCriticalAttacking) return;

        // 안전하게 모든 문을 걸어잠그고 처형 돌입
        isCriticalAttacking = true;
        currentState = AIState.Attack;
        isInteracting = true;
        isAttacking = true;
        canExecuteCritical = false;


        StopAllCoroutines();
        StartCoroutine(CriticalAttackRoutine(targetPlayer));
    }

    private IEnumerator HitRoutine(string b, float d)
    {
        currentState = AIState.Hit;
        anim.SetBool("IsGuarding", false);
        anim.SetFloat("InputY", 0);
        anim.SetTrigger(b);

        float elapsed = 0f;
        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            UpdateLayerWeights();

            if (playerTransform != null)
            {
                HandleRotation(playerTransform.position);
            }

            yield return null;
        }

        if (playerTransform != null && !isDead)
        {
            Transform targetRotationRoot = controller != null ? controller.transform : transform;
            Vector3 dir = playerTransform.position - targetRotationRoot.position;
            dir.y = 0;

            if (dir != Vector3.zero)
            {
                targetRotationRoot.rotation = Quaternion.LookRotation(dir);
            }
        }

        if (!isDead) currentState = AIState.Chase;
    }

    public void GetParried(float kickImpactTime = 2.5f)
    {
        if (currentState == AIState.Dead) return;

        StopAllCoroutines();

        currentState = AIState.Hit;
        isAttacking = false;
        isInteracting = false;

        StartCoroutine(GetParriedRoutine(kickImpactTime));
    }

    private IEnumerator GetParriedRoutine(float duration)
    {
        isParried = true;
        anim.SetTrigger("IsParried");
        anim.Play("Parried", actionLayerIndex, 0f);

        float myParryAnimLength = 1.45f;
        anim.speed = myParryAnimLength / duration;
        yield return new WaitForSeconds(duration + 0.1f);

        if (isParried)
        {
            isParried = false;
            anim.speed = 1f;
            currentState = AIState.Idle;
        }
    }

    public void GetCriticalAttacked(float damage)
    {
        if (currentState == AIState.Dead) return;

        anim.speed = 1f;
        StopAllCoroutines();
        isInteracting = false;

        if (currentHealth > criticalAttackDamage)
        {
            StartCoroutine(GetCriticalAttackedUpperRoutine(damage));
        }
        else
        {
            StartCoroutine(GetCriticalAttackedUnderRoutine(damage));
        }
    }

    private IEnumerator GetCriticalAttackedUpperRoutine(float damage)
    {
        currentState = AIState.Hit;
        isAttacking = false;
        isParried = false;

        anim.SetTrigger("IsCriAtckedUp");

        Vector3 pushForce = -transform.forward * 2.5f;
        float elapsed = 0f;
        float duration = 3.1f;

        bool isDamageApplied = false;
        float damageTiming = 0.38f;

        while (elapsed < duration)
        {
            pushForce = Vector3.Lerp(pushForce, Vector3.zero, Time.deltaTime * 2f);
            if (controller != null && controller.enabled)
            {
                controller.Move(pushForce * Time.deltaTime);
            }
            if (!isDamageApplied && elapsed >= damageTiming)
            {
                ApplyDamage(damage);
                isDamageApplied = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

    }

    private IEnumerator GetCriticalAttackedUnderRoutine(float damage)
    {
        currentState = AIState.Hit;
        isAttacking = false;
        isParried = false;

        anim.SetTrigger("IsCriAtckedUnder");

        Vector3 pushForce = Vector3.zero;
        float elapsed = 0f;

        float originalAnimSpeed = 2f;
        float duration = 3.35f / originalAnimSpeed;
        float forceStartTime = 2.6f / originalAnimSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= forceStartTime)
            {
                if (pushForce == Vector3.zero) pushForce = -transform.forward * 1.0f;
                pushForce = Vector3.Lerp(pushForce, Vector3.zero, Time.deltaTime * 2f);
                if (controller != null && controller.enabled) controller.Move(pushForce * Time.deltaTime);
            }
            else
            {
                if (controller != null && controller.enabled)
                    controller.Move(new Vector3(0, -9.81f, 0) * Time.deltaTime);
            }
            yield return null;
        }

        anim.speed = 0f;
        if (controller != null) controller.enabled = false;

        transform.position += Vector3.down * 0.15f;

        ApplyDamage(damage);
        isDead = true;
        currentState = AIState.Dead;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (BattleManager.Instance != null) BattleManager.Instance.OnEnemyDefeated();

        StopAllCoroutines();
    }

    private void ApplyDamage(float damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            currentState = AIState.Dead;
        }
        else
        {
            currentState = AIState.Idle;
        }
    }

    private bool CanBlock(CombatStance s)
    {
        return (s == CombatStance.Top && currentStance == CombatStance.Top) ||
               (s == CombatStance.Left && currentStance == CombatStance.Right) ||
               (s == CombatStance.Right && currentStance == CombatStance.Left);
    }

    private void Die()
    {
        isDead = true;
        currentState = AIState.Dead;
        anim.SetTrigger("IsDead");
        if (controller != null) controller.enabled = false;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (BattleManager.Instance != null) BattleManager.Instance.OnEnemyDefeated();

        StopAllCoroutines();
    }

    private void UpdateLayerWeights()
    {
        if (isDead || anim == null) return;
        float target = 0f;
        if (isGuarding || (!isAttacking && currentState != AIState.Hit && currentState != AIState.Dead))
        {
            target = 1f;
        }
        float currentWeight = anim.GetLayerWeight(actionLayerIndex);
        anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(currentWeight, target, Time.deltaTime * weightLerpSpeed));
    }

    private void ApplyGravity()
    {
        if (controller == null) return;

        if (controller.isGrounded)
        {
            if (velocity.y < 0) velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateAnimationParams()
    {
        anim.SetFloat("InputY", (currentState == AIState.Chase) ? 1f : 0f, 0.1f, Time.deltaTime);

        float targetInputX = 0f;
        if (currentState == AIState.Chase || isInteracting)
        {
            if (currentMoveState == MovementState.StrafeLeft) targetInputX = -1f;
            else if (currentMoveState == MovementState.StrafeRight) targetInputX = 1f;
        }
        anim.SetFloat("InputX", targetInputX, 0.1f, Time.deltaTime);

        anim.SetFloat("Stance", (float)currentStance);
        anim.SetBool("IsGuarding", isGuarding);
    }

    public IEnumerator RecoilRoutine(float duration)
    {
        isInteracting = true;
        currentState = AIState.Idle;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.parrySounds, 0.6f);


        yield return new WaitForSeconds(duration);

        isInteracting = false;
        currentState = AIState.Chase;
    }
}

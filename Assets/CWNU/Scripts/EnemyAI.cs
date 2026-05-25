using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Chase, Attack, Hit, Dead }

    [Header("Character Profile")]
    public CharacterData characterData; // 플레이어와 동일한 데이터 에셋 연결

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

    [Header("Combat Settings")]
    public float attackCooldown = 2.5f;
    private float lastAttackTime;
    public bool isGuarding = false;
    public bool isParried = false;
    public bool isAttacking = false;

    [Header("Layer Settings")]
    public string actionLayerName = "Action Layer";
    private int actionLayerIndex;
    public float weightLerpSpeed = 20f;

    [Header("References")]
    public CharacterController controller; // 최상위 부모의 컨트롤러를 담을 변수
    [HideInInspector] public Animator anim;
    [HideInInspector] public Transform targetEnemyTransform; // Target_Enemy 자동 참조용

    private Dictionary<string, float> hitDurationDict = new Dictionary<string, float>();
    private Vector3 velocity;
    private bool isDead = false;
    private bool isInteracting = false;

    //안쓰는거 같아서 임시 주석 처리 (안쓸거 같으면 지우죠)
    //[Header("Combat Stance Telegraphing")]
    //private CombatStance nextStance; // 선출된 다음 공격 방향
    //private bool isPreviewing = false; // 전조 Idle 대기 중인지 체크하는 플래그

    void Awake()
    {
        // 최상위 부모나 본인에게서 CharacterController를 확실하게 가져옵니다.
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
        // 스탯 데이터 적용
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

            // 기존 고정값 보존용 
            guardStaminaCost = 20f;
            minStaminaToGuard = 10f;
            rotationSpeed = 100f;

            if (characterData.topHitClip != null) hitDurationDict["TopHit"] = characterData.topHitClip.length;
            if (characterData.sideHitClip != null) hitDurationDict["SideHit"] = characterData.sideHitClip.length;
        }

        currentHealth = maxHealth;
        currentStamina = maxStamina;

        if (anim != null)
            actionLayerIndex = anim.GetLayerIndex(actionLayerName);
    }

    // [SOUND] 애니메이션 이벤트 수신기: 발걸음 소리
    public void PlayFootstep()
    {
        if (currentState == AIState.Chase && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.footstepSounds, 0.2f);
        }
    }
    void Update()
    {
        if (isDead || anim == null) return;

        // 1. 중력 및 스태미나 (항상 실행)
        if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
        {
            ApplyGravity();
        }
        HandleStaminaRegen();

        // 2. 플레이어 타겟 확보 (실시간 탐색으로 교정 🔴)
        // 매 프레임 활성화된 플레이어의 최신 조준점을 실시간으로 새로 탐색하여 추적합니다.
        GameObject pObj = GameObject.FindWithTag("Player");
        if (pObj != null)
        {
            Transform pTarget = pObj.transform.FindDeepChild("Camera_Target");
            // Camera_Target이 찾아졌다면 그걸 쓰고, 없거나 비활성화 상태면 플레이어 본체를 꼽습니다.
            playerTransform = (pTarget != null && pTarget.gameObject.activeInHierarchy) ? pTarget : pObj.transform;
        }
        else
        {
            playerTransform = null;
        }

        // 플레이어 조준점이 없으면 리턴
        if (playerTransform == null) return;

        // 🔴 피격이나 공격 상태일 때 예외 처리 순서
        if (currentState == AIState.Hit || isInteracting)
        {
            UpdateLayerWeights(); // 1. 피격/공격 애니메이션 출력을 위한 가중치 연산 정상화
            if (currentState == AIState.Hit || isAttacking) isGuarding = false;

            HandleRotation(playerTransform.position); // 2. 모션 실행 중에도 플레이어는 무조건 바라봄

            return; // 3. 모션 중에 이동(MoveTowardsPlayer)이 겹치지 않도록 여기서 차단
        }

        // 3. 평상시 AI 거리 계산 및 추적 시작
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
                if (distance > attackRange) currentState = AIState.Chase;
                else HandleAttackPattern();
                break;
        }
        UpdateAnimationParams();
        UpdateLayerWeights();
    }

    private void HandleStaminaRegen() { 
        if (!isGuarding && currentState != AIState.Hit && currentStamina < maxStamina) currentStamina += staminaRegenRate * Time.deltaTime; 
    }

    private void MoveTowardsPlayer()
    {
        if (controller == null || playerTransform == null) return;

        // 🔴 핵심 교정: 적의 '몸이 바라보는 정면'을 기준으로 앞으로 전진하게 만드는 게 아니라,
        // 플레이어가 있는 월드 좌표상의 실제 방향(dir)을 계산해서 물리 컨트롤러를 밀어줍니다.
        Vector3 dir = (playerTransform.position - controller.transform.position).normalized;
        dir.y = 0;

        controller.Move(dir * moveSpeed * Time.deltaTime);
    }

    private void HandleRotation(Vector3 targetPos)
    {
        // 🔴 핵심 교정: transform.parent 대신, Awake에서 안전하게 찾아둔 
        // 물리와 이동을 담당하는 최상위 변수인 'controller'의 transform을 직접 돌려버립니다.
        if (controller == null) return;

        Transform rootBody = controller.transform;

        // 적과 플레이어 사이의 방향 계산
        Vector3 dir = targetPos - rootBody.position;
        dir.y = 0; // 엎드러지거나 하늘을 보지 않도록 Y축 회전 고정

        if (dir != Vector3.zero)
        {
            // Slerp를 이용해 최상위 몸통(Controller를 가진 오브젝트)을 플레이어 쪽으로 부드럽고 빠르게 회전시킵니다.
            // rotationSpeed가 40f로 되어있는데, 격투 게임 특성상 시선이 휙휙 돌아가야 하므로 대폭 올리거나 즉시 회전하는 것이 좋습니다.
            rootBody.rotation = Quaternion.Slerp(rootBody.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed);
        }
    }

    private void HandleAttackPattern() { 
        if (Time.time >= lastAttackTime + attackCooldown) ExecuteAttack(); 
    }

    private void ExecuteAttack() { 
        if (isInteracting || currentStamina < 20f) return; 
        StartCoroutine(AttackRoutine()); 
        lastAttackTime = Time.time; 
    }

    private IEnumerator AttackRoutine()
    {
        isInteracting = true; isGuarding = false; isAttacking = true;
        currentStance = (CombatStance)Random.Range(0, 3);
        currentStamina -= 15f;
        ResetAllActionBools();

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.5f);

        string bName = currentStance == CombatStance.Top ? "IsTopAttack" : currentStance == CombatStance.Left ? "IsLeftAttack" : "IsRightAttack";
        anim.SetBool(bName, true);

        // 타격 판정이 발생하기 전까지 기다립니다.
        // 이 0.4초 동안 적은 isAttacking = true 상태이므로 패링이 가능해집니다!
        float windUpTime = 0.4f;
        yield return new WaitForSeconds(windUpTime);

        CheckCombatHit();

        yield return new WaitForSeconds(attackDuration - windUpTime);

        anim.SetBool(bName, false);
        isInteracting = false; isAttacking = false;
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
        if (isDead || currentState == AIState.Hit) return;

        if (isGuarding && CanBlock(attackerStance))
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.clashSounds, 0.7f);

            currentStamina -= guardStaminaCost;

            if (attacker != null)
            {
                attacker.StartCoroutine("RecoilRoutine", 1.0f);
            }

            if (currentStamina <= 0) { currentStamina = 0; isGuarding = false; }
            return;
        }

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.hitSounds, 0.8f);

        currentHealth -= 20f;
        if (currentHealth <= 0) { Die(); return; }

        StopAllCoroutines();
        ResetAllActionBools();
        isInteracting = false;

        string hitBool = (attackerStance == CombatStance.Top) ? "IsTopHit" : (attackerStance == CombatStance.Left) ? "IsRightHit" : "IsLeftHit";
        float duration = (attackerStance == CombatStance.Top) ? hitDurationDict.GetValueOrDefault("TopHit", 0.5f) : hitDurationDict.GetValueOrDefault("SideHit", 0.5f);

        StartCoroutine(HitRoutine(hitBool, duration));
    }

    public void GetParried(float kickImpactTime = 2.5f)
    {
        if (currentState == AIState.Dead) return;

        StopAllCoroutines();

        ResetAllActionBools();

        currentState = AIState.Hit;
        isAttacking = false;
        isInteracting = false;

        StartCoroutine(GetParriedRoutine(kickImpactTime));
    }

    private IEnumerator GetParriedRoutine(float duration)
    {
        isParried = true;

        anim.SetBool("IsParried", true);

        anim.Play("Parried", actionLayerIndex, 0f);

        float myParryAnimLength = 1.45f;
        anim.speed = myParryAnimLength / duration;
        yield return new WaitForSeconds(duration + 0.1f);

        if (isParried)
        {
            isParried = false; 
            anim.speed = 1f;
            anim.SetBool("IsParried", false);
            currentState = AIState.Idle;
        }
    }

    public void GetCriticalAttacked(float damage)
    {
        if (currentState == AIState.Dead) return;

        anim.speed = 1f;
        StopAllCoroutines();
        anim.SetBool("IsParried", false);

        isInteracting = false;

        if (currentHealth > 50f)
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

        anim.CrossFadeInFixedTime("CriticalAttackedUpper", 0.1f);

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
                isDamageApplied = true; // 중복 실행 방지
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

        anim.CrossFadeInFixedTime("CriticalAttackedUnder", 0.1f);

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
        StopAllCoroutines();
        ResetAllActionBools();
        currentState = AIState.Dead;
        controller.enabled = false;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (BattleManager.Instance != null) BattleManager.Instance.OnEnemyDefeated();
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

    private bool CanBlock(CombatStance s) { return (s == CombatStance.Top && currentStance == CombatStance.Top) || (s == CombatStance.Left && currentStance == CombatStance.Right) || (s == CombatStance.Right && currentStance == CombatStance.Left); }

    private IEnumerator HitRoutine(string b, float d)
    {
        currentState = AIState.Hit;

        anim.SetBool("IsGuarding", false);
        anim.SetFloat("InputY", 0);

        // 1. 피격 애니메이션 시작
        anim.SetBool(b, true);

        // 🔴 교정: 맞아서 경직이 들어간 시간(d) 동안 이동은 안 하지만, 플레이어가 좌우로 무빙하면 시선은 실시간으로 따라가도록 보정합니다.
        float elapsed = 0f;
        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            UpdateLayerWeights();

            // 피격 경직 중에도 플레이어의 최신 위치를 실시간 검색하여 바라봅니다.
            GameObject pObj = GameObject.FindWithTag("Player");
            if (pObj != null)
            {
                Transform pTarget = pObj.transform.FindDeepChild("Camera_Target");
                Transform currentTarget = (pTarget != null && pTarget.gameObject.activeInHierarchy) ? pTarget : pObj.transform;
                HandleRotation(currentTarget.position);
            }

            yield return null;
        }

        // 2. 피격 애니메이션 종료
        anim.SetBool(b, false);

        // 🔴 교정: 프레임 지연 없이 즉시 정면 각도를 쳐다보도록 즉시 동기화 처리
        if (playerTransform != null && !isDead)
        {
            Transform targetRotationRoot = transform.parent != null ? transform.parent : transform;
            Vector3 dir = playerTransform.position - targetRotationRoot.position;
            dir.y = 0;

            if (dir != Vector3.zero)
            {
                targetRotationRoot.rotation = Quaternion.LookRotation(dir);
            }
        }

        // 3. 시선 동기화가 완벽히 끝난 상태에서 안전하게 추적/공격 상태로 복귀
        if (!isDead) currentState = AIState.Chase;
    }

    private void Die()
    {
        isDead = true;
        StopAllCoroutines();
        ResetAllActionBools();
        anim.SetBool("IsDead", true);
        currentState = AIState.Dead;
        controller.enabled = false;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);

        // 🔴 [핵심 추가] 일반 사망 시에도 마우스 커서 완전 해제
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (BattleManager.Instance != null) BattleManager.Instance.OnEnemyDefeated();
    }

    private void ResetAllActionBools() { 
        anim.SetBool("IsTopAttack", false); 
        anim.SetBool("IsLeftAttack", false); 
        anim.SetBool("IsRightAttack", false); 
        anim.SetBool("IsTopHit", false); 
        anim.SetBool("IsLeftHit", false); 
        anim.SetBool("IsRightHit", false); 
        anim.SetBool("IsParried", false); 
    }

    // 🔴 [수정 완료] 가드 중이거나, 공격·피격을 하지 않는 평상시 상태일 때 가중치를 1로 복구
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

        // 🔴 수정: 바닥 체크 조건과 무관하게 공중일 때 중력이 누적되도록 분리
        if (controller.isGrounded)
        {
            if (velocity.y < 0) velocity.y = -2f; // 접지 유지
        }
        else
        {
            velocity.y += gravity * Time.deltaTime; // 낙하 속도 누적
        }

        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateAnimationParams() { anim.SetFloat("InputY", (currentState == AIState.Chase) ? 1f : 0f, 0.1f, Time.deltaTime); anim.SetFloat("Stance", (float)currentStance); anim.SetBool("IsGuarding", isGuarding); }

    public IEnumerator RecoilRoutine(float duration)
    {
        isInteracting = true;
        currentState = AIState.Idle;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.parrySounds, 0.6f);

        ResetAllActionBools();

        yield return new WaitForSeconds(duration);

        isInteracting = false;
        currentState = AIState.Chase;
    }
}

// 자식 계층 구조 깊은 탐색을 위한 확장 메서드 서포트 클래스
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}
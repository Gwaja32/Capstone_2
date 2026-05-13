using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Chase, Attack, Hit, Dead }

    [Header("State Settings")]
    public AIState currentState = AIState.Idle;
    public CombatStance currentStance = CombatStance.Top;

    [Header("Stats Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaRegenRate = 15f;
    public float guardStaminaCost = 20f;
    public float minStaminaToGuard = 10f;

    [Header("Detection & Layer Mask")]
    public Transform playerTransform;
    public LayerMask playerLayer;
    public float chaseRange = 10f;
    public float attackRange = 1.2f;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Combat Settings")]
    public float attackCooldown = 2.5f;
    public float attackDuration = 1.0f;
    private float lastAttackTime;
    public bool isGuarding = false;

    [Header("Animation Clips")]
    public AnimationClip topHitClip;
    public AnimationClip sideHitClip;

    [Header("Layer Settings")]
    public string actionLayerName = "Action Layer";
    private int actionLayerIndex;
    public float weightLerpSpeed = 20f;

    [Header("References")]
    public CharacterController controller;
    public Animator anim;

    private Dictionary<string, float> hitDurationDict = new Dictionary<string, float>();
    private Vector3 velocity;
    private bool isDead = false;
    private bool isInteracting = false;

    void Start()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        actionLayerIndex = anim.GetLayerIndex(actionLayerName);
        if (topHitClip != null) hitDurationDict["TopHit"] = topHitClip.length;
        if (sideHitClip != null) hitDurationDict["SideHit"] = sideHitClip.length;
    }

    // [SOUND] 애니메이션 이벤트 수신기: 발걸음 소리
    public void PlayFootstep()
    {
        // 추격 상태일 때만 발걸음 소리 재생
        if (currentState == AIState.Chase && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.footstepSounds, 0.2f);
        }
    }

    void Update()
    {
        if (isDead) return;
        ApplyGravity();
        HandleStaminaRegen();

        if (currentState == AIState.Hit || isInteracting) { UpdateLayerWeights(); return; }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        switch (currentState)
        {
            case AIState.Idle: if (distance <= chaseRange) currentState = AIState.Chase; break;
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

    private void HandleStaminaRegen() { if (!isGuarding && currentState != AIState.Hit && currentStamina < maxStamina) currentStamina += staminaRegenRate * Time.deltaTime; }

    private void MoveTowardsPlayer() { Vector3 dir = (playerTransform.position - transform.position).normalized; dir.y = 0; controller.Move(dir * moveSpeed * Time.deltaTime); }

    private void HandleRotation(Vector3 targetPos) { Vector3 dir = targetPos - transform.position; dir.y = 0; if (dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed); }

    private void HandleAttackPattern() { if (Time.time >= lastAttackTime + attackCooldown) ExecuteAttack(); }

    private void ExecuteAttack() { if (isInteracting || currentStamina < 20f) return; StartCoroutine(AttackRoutine()); lastAttackTime = Time.time; }

    private IEnumerator AttackRoutine()
    {
        isInteracting = true; isGuarding = false; currentStance = (CombatStance)Random.Range(0, 3);
        currentStamina -= 15f; ResetAllActionBools();

        // [SOUND] 적 공격 휘두르는 소리
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.5f);

        string bName = currentStance == CombatStance.Top ? "IsTopAttack" : currentStance == CombatStance.Left ? "IsLeftAttack" : "IsRightAttack";
        anim.SetBool(bName, true); CheckCombatHit();
        yield return new WaitForSeconds(attackDuration);
        anim.SetBool(bName, false); isInteracting = false;
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

    // [수정] 피격 및 가드 판정 로직
    public void TakeDamage(CombatStance attackerStance, TPSFixedMovement attacker)
    {
        if (isDead || currentState == AIState.Hit) return;

        // 1. 가드 성공 판정
        if (isGuarding && CanBlock(attackerStance))
        {
            // [SOUND] 적이 가드 성공 (금속 충돌음)
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.clashSounds, 0.7f);

            currentStamina -= guardStaminaCost; // 스테미나 대량 소모

            // [메리트] 나를 때린 플레이어에게 역경직 부여
            if (attacker != null)
            {
                attacker.StartCoroutine("RecoilRoutine", 1.0f); // 플레이어 1초간 역경직
            }

            if (currentStamina <= 0) { currentStamina = 0; isGuarding = false; }
            return;
        }

        // 2. 가드 실패 시 (생짜 피격)
        // [SOUND] 적 피격 소리
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

    private bool CanBlock(CombatStance s) { return (s == CombatStance.Top && currentStance == CombatStance.Top) || (s == CombatStance.Left && currentStance == CombatStance.Right) || (s == CombatStance.Right && currentStance == CombatStance.Left); }

    private IEnumerator HitRoutine(string b, float d)
    {
        currentState = AIState.Hit;

        // [중요] 피격 시작 시점에 가드와 이동 관련 파라미터를 다시 한번 리셋
        anim.SetBool("IsGuarding", false);
        anim.SetFloat("InputY", 0);

        anim.SetBool(b, true); // 피격 애니메이션 시작

        yield return new WaitForSeconds(d);

        anim.SetBool(b, false);
        if (!isDead) currentState = AIState.Chase;
    }

    private void Die() { 
        isDead = true; StopAllCoroutines(); ResetAllActionBools(); anim.SetBool("IsDead", true); currentState = AIState.Dead; controller.enabled = false;

        // [SOUND] 적 사망 및 플레이어 승리 사운드
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);
        }
    }

    private void ResetAllActionBools() { anim.SetBool("IsTopAttack", false); anim.SetBool("IsLeftAttack", false); anim.SetBool("IsRightAttack", false); anim.SetBool("IsTopHit", false); anim.SetBool("IsLeftHit", false); anim.SetBool("IsRightHit", false); }

    private void UpdateLayerWeights() { float target = (isInteracting || currentState == AIState.Hit || isGuarding) ? 1f : 0f; anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(anim.GetLayerWeight(actionLayerIndex), target, Time.deltaTime * weightLerpSpeed)); }

    private void ApplyGravity() { if (controller.isGrounded && velocity.y < 0) velocity.y = -2f; velocity.y += gravity * Time.deltaTime; controller.Move(velocity * Time.deltaTime); }

    private void UpdateAnimationParams() { anim.SetFloat("InputY", (currentState == AIState.Chase) ? 1f : 0f, 0.1f, Time.deltaTime); anim.SetFloat("Stance", (float)currentStance); anim.SetBool("IsGuarding", isGuarding); }

    // [추가] 역경직 코루틴 (적도 공격이 막히면 멈춰야 함)
    public IEnumerator RecoilRoutine(float duration)
    {
        isInteracting = true; // 공격 중인 것처럼 처리해서 다음 행동 차단
        currentState = AIState.Idle; // AI 로직 일시 중지

        // [SOUND] 적이 공격을 막혔을 때의 둔탁한 소리 (가드 소리와 별개로 연출 가능)
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.parrySounds, 0.6f);

        // 공격 애니메이션 강제 종료 (Any State에서 리셋되도록)
        ResetAllActionBools();

        yield return new WaitForSeconds(duration);

        isInteracting = false;
        currentState = AIState.Chase;
    }
}
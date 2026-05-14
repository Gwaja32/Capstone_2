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
    public bool isParried = false;
    public bool isAttacking = false;

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

    private void HandleStaminaRegen() { 
        if (!isGuarding && currentState != AIState.Hit && currentStamina < maxStamina) currentStamina += staminaRegenRate * Time.deltaTime; 
    }

    private void MoveTowardsPlayer() { 
        Vector3 dir = (playerTransform.position - transform.position).normalized; 
        dir.y = 0; controller.Move(dir * moveSpeed * Time.deltaTime); 
    }

    private void HandleRotation(Vector3 targetPos) { 
        Vector3 dir = targetPos - transform.position; 
        dir.y = 0; 
        if (dir != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed); 
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

        while (elapsed < duration)
        {
            pushForce = Vector3.Lerp(pushForce, Vector3.zero, Time.deltaTime * 2f);
            if (controller != null && controller.enabled)
            {
                controller.Move(pushForce * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyDamage(damage);
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

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position += Vector3.down * 0.15f;

        ApplyDamage(damage);
        isDead = true;
        StopAllCoroutines();
        ResetAllActionBools();
        currentState = AIState.Dead;
        controller.enabled = false;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);
        }
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

        anim.SetBool(b, true);

        yield return new WaitForSeconds(d);

        anim.SetBool(b, false);
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
        {
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);
        }
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

    private void UpdateLayerWeights() { 
        float target = (isInteracting || currentState == AIState.Hit || isGuarding) ? 1f : 0f;
        anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(anim.GetLayerWeight(actionLayerIndex), target, Time.deltaTime * weightLerpSpeed)); 
    }

    private void ApplyGravity() { if (controller.isGrounded && velocity.y < 0) velocity.y = -2f; velocity.y += gravity * Time.deltaTime; controller.Move(velocity * Time.deltaTime); }

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
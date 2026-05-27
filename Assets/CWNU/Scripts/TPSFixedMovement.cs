using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static EnemyAI;

public class TPSFixedMovement : MonoBehaviour
{
    [Header("Character Profile")]
    public CharacterData characterData;

    private float moveSpeed;
    private float maxHealth;
    private float currentHealth;
    private float attackRange;
    private float attackDuration;
    private float parryDuration;
    private AnimationClip topHitClip;
    private AnimationClip sideHitClip;

    private float maxStamina;
    private float currentStamina;
    private float staminaRegenRate;
    private float guardStaminaCost;
    private float attackStaminaCost;
    private float parryStaminaCost;
    private float criticalAttackStaminaCost;

    [Header("Aim Step Settings")]
    public float mouseStepThreshold = 1000f;
    public float mouseDecaySpeed = 3f;

    [Header("Combat Stance")]
    public CombatStance currentStance = CombatStance.Top;

    [Header("Movement Settings")]
    public float guardSpeedMultiplier = 0.4f;
    public float gravity = -9.81f;

    [Header("Combat & Hit System")]
    public LayerMask enemyLayer;
    public float criticalAttackDamage = 33.0f;
    public float criticalAttackedDuration = 0.3f;
    public bool isHitState = false;
    public bool isGuarding = false;
    public bool isParried = false;

    [Header("References")]
    public CharacterController controller;
    public Animator anim;
    public bool isInteracting = true;

    // Internal Variables
    private float mouseAccumulatorX = 0f;
    private float mouseAccumulatorY = 0f;
    private Vector3 velocity;
    private int actionLayerIndex;
    private Dictionary<string, float> hitDurationDict = new Dictionary<string, float>();

    private InputActionMap actionMap;
    private InputAction moveAction, lookAction, guardAction, parryAction;

    private bool isDead = false;
    private bool isCriticalAttacking = false;
    private Vector3 externalForce;

    private Coroutine attackCoroutine;
    private Coroutine parryCoroutine;
    private Coroutine hitCoroutine;
    private Coroutine criticalAttackCoroutine;

    private PlayerController parentController;

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
        if (characterData != null)
        {
            moveSpeed = characterData.moveSpeed;
            maxHealth = characterData.maxHealth;
            currentHealth = maxHealth;
            attackRange = characterData.attackRange;
            attackDuration = characterData.attackDuration;
            parryDuration = characterData.parryDuration;
            maxStamina = characterData.maxStamina;
            currentStamina = characterData.maxStamina;
            staminaRegenRate = characterData.staminaRegenRate;
            guardStaminaCost = characterData.guardStaminaCost;
            attackStaminaCost = characterData.attackStaminaCost;
            parryStaminaCost = characterData.parryStaminaCost;
            criticalAttackStaminaCost = characterData.criticalAttackStaminaCost;

            if (characterData.topHitClip != null)
                hitDurationDict["TopHit"] = characterData.topHitClip.length;
            if (characterData.sideHitClip != null)
                hitDurationDict["SideHit"] = characterData.sideHitClip.length;
        }

        if (transform.parent != null)
        {
            parentController = transform.parent.GetComponent<PlayerController>();
        }

        Cursor.lockState = CursorLockMode.Locked;
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        actionLayerIndex = anim.GetLayerIndex("Action Layer");
    }

    void Update()
    {
        if (isDead) return;

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            return;
        }

        bool parryTriggered = parryAction.triggered;
        bool attackTriggered = Mouse.current.leftButton.wasPressedThisFrame;

        if (isInteracting && !isHitState)
        {
            HandleGuardInput();
            HandleAimStep();

            if (attackTriggered)
            {
                ExecuteAttack();
            }

            if (parryTriggered)
            {
                ExecuteParry();
            }
        }

        HandleStaminaRegen();
        ApplyMovement();
        UpdateActionLayerWeight(); // 🛠️ 고정: 특수 상황엔 내부에서 리턴됨
        UpdateAnimationParams();
    }

    void LateUpdate()
    {
        if (isDead || isCriticalAttacking || isHitState) return;
        PlayerController parentController = transform.parent.GetComponent<PlayerController>();
        if (parentController != null && parentController.currentLockOnTarget != null)
        {
            EnemyAI enemyComp = parentController.currentLockOnTarget.GetComponent<EnemyAI>();
            if (enemyComp != null && enemyComp.currentHealth <= 0)
            {
                parentController.currentLockOnTarget = null;
                return;
            }

            if (!IsValidVector(transform.position))
                return;

            Vector3 targetDir = parentController.currentLockOnTarget.position - transform.position;

            if (targetDir.sqrMagnitude > 0.09f)
            {
                targetDir.y = 0;
                if (targetDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
                }
            }
        }
    }

    public float getCurrentHealth() { return currentHealth; }
    public float getMaxHealth() { return maxHealth; }
    public float getCurrentStamina() { return currentStamina; }
    public float getMaxStamina() { return maxStamina; }

    private void HandleGuardInput()
    {
        if (guardAction.IsPressed() && currentStamina >= guardStaminaCost)
        {
            isGuarding = true;
        }
        else isGuarding = false;

        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            isGuarding = false;
        }
    }

    private void HandleStaminaRegen()
    {
        if (!isGuarding && !isHitState && currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Min(currentStamina, maxStamina);
        }
    }

    private void StopAndClear(ref Coroutine coroutine)
    {
        if (coroutine == null) return;
        StopCoroutine(coroutine);
        coroutine = null;
    }

    private void ResetAllTriggers()
    {
        anim.ResetTrigger("IsTopAttack");
        anim.ResetTrigger("IsLeftAttack");
        anim.ResetTrigger("IsRightAttack");
        anim.ResetTrigger("IsParry");
        anim.ResetTrigger("IsParried");
        anim.ResetTrigger("IsTopHit");
        anim.ResetTrigger("IsLeftHit");
        anim.ResetTrigger("IsRightHit");
        anim.ResetTrigger("IsCriAtckedUp");
        anim.ResetTrigger("IsCriAtckedUnder");
    }

    private void ApplyMovement()
    {
        if (controller == null || !controller.enabled) return;

        bool isGrounded = controller.isGrounded;
        anim.SetBool("IsGrounded", isGrounded);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        Vector3 finalMove = Vector3.zero;

        if (externalForce.magnitude > 0.1f)
        {
            finalMove += externalForce;
            externalForce = Vector3.Lerp(externalForce, Vector3.zero, Time.deltaTime * 2f);
        }

        if (isInteracting && !isHitState)
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            float speedMultiplier = isGuarding ? guardSpeedMultiplier : 1.0f;

            Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x);

            if (move.magnitude > 1f)
                move.Normalize();

            finalMove += move * moveSpeed * speedMultiplier;

            anim.SetFloat("InputX", moveInput.x, 0.1f, Time.deltaTime);
            anim.SetFloat("InputY", moveInput.y, 0.1f, Time.deltaTime);
        }
        else
        {
            anim.SetFloat("InputX", 0f, 0.1f, Time.deltaTime);
            anim.SetFloat("InputY", 0f, 0.1f, Time.deltaTime);
        }

        finalMove.y = velocity.y;

        if (!IsValidVector(finalMove))
        {
            Debug.LogError("Invalid finalMove");
            finalMove = Vector3.zero;
        }

        controller.Move(finalMove * Time.deltaTime);
    }

    private bool IsValidVector(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    private void UpdateActionLayerWeight()
    {
        if (isCriticalAttacking || isHitState || !isInteracting) return;

        float currentWeight = anim.GetLayerWeight(actionLayerIndex);
        anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(currentWeight, 1f, Time.deltaTime * 20f));
    }

    public void PlayFootstep()
    {
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput.magnitude > 0.1f && isInteracting && !isHitState)
        {
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.footstepSounds, 0.3f);
        }
    }

    private void ExecuteAttack()
    {
        if (!isInteracting || isHitState || attackCoroutine != null || parryCoroutine != null) return;

        Vector3 rayStart = transform.position + Vector3.up * 0.45f - transform.forward * 0.5f;

        if (Physics.SphereCast(rayStart, 0.3f, transform.forward, out RaycastHit hit, attackRange, enemyLayer))
        {
            EnemyAI target = hit.collider.GetComponent<EnemyAI>();
            if (target != null && target.isParried)
            {
                if (currentStamina >= criticalAttackStaminaCost)
                {
                    if (criticalAttackCoroutine != null) StopCoroutine(criticalAttackCoroutine);
                    criticalAttackCoroutine = StartCoroutine(CriticalAttackRoutine(target));
                    return;
                }
                else return;
            }
        }
        if (currentStamina >= attackStaminaCost)
        {
            attackCoroutine = StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        currentStamina -= attackStaminaCost;
        currentStamina = Mathf.Max(currentStamina, 0f);

        isInteracting = false;
        isGuarding = false;

        anim.SetLayerWeight(actionLayerIndex, 0f);

        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.6f);
        string triggerName = currentStance == CombatStance.Top ? "IsTopAttack" : currentStance == CombatStance.Left ? "IsLeftAttack" : "IsRightAttack";

        anim.SetTrigger(triggerName);
        CheckCombatHit(15f, false);

        yield return null;
        yield return new WaitForSeconds(attackDuration);

        anim.SetLayerWeight(actionLayerIndex, 1f);

        isInteracting = true;
        attackCoroutine = null;
    }

    private IEnumerator CriticalAttackRoutine(EnemyAI targetEnemy)
    {
        currentStamina -= criticalAttackStaminaCost;
        currentStamina = Mathf.Max(currentStamina, 0f);

        isCriticalAttacking = true;
        isInteracting = false;
        isGuarding = false;

        bool isUpperAttack = targetEnemy.currentHealth > criticalAttackDamage;

        if (controller != null) controller.enabled = false;
        velocity = Vector3.zero;

        float elapsed = 0f;
        float alignDuration = 0.15f;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 toEnemyDir = (targetEnemy.transform.position - transform.position).normalized;
        toEnemyDir.y = 0;

        if (toEnemyDir.sqrMagnitude < 0.0001f)
        {
            toEnemyDir = transform.forward;
        }
        else
        {
            toEnemyDir.Normalize();
        }

        Vector3 myRightDir = Vector3.Cross(Vector3.up, toEnemyDir);

        float forwardOffset = 0.3f;
        float myRightOffset = 0f;

        if (isUpperAttack)
        {
            myRightOffset = 0f;
        }
        else
        {
            myRightOffset = -0.1f;
        }

        Vector3 targetPos = targetEnemy.transform.position
                          - (toEnemyDir * forwardOffset)
                          + (myRightDir * myRightOffset);

        targetPos.y = transform.position.y;

        Quaternion targetRot = Quaternion.LookRotation(toEnemyDir);

        controller.enabled = false;

        // 정렬 보간 실행
        while (elapsed < alignDuration)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / alignDuration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / alignDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;
        transform.rotation = targetRot;
        controller.enabled = true;

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

        // 상대방 피격 동기화
        targetEnemy.GetParried(kickImpactTime);
        yield return new WaitForSeconds(kickImpactTime);

        // 데미지 적용
        targetEnemy.GetCriticalAttacked(criticalAttackDamage);
       
        PlayerController parentController = transform.parent.GetComponent<PlayerController>();
        if (parentController != null && targetEnemy.currentHealth <= 0)
        {
            parentController.currentLockOnTarget = null;
        }

        yield return new WaitForSeconds(totalAnimDuration - kickImpactTime);

        anim.SetLayerWeight(actionLayerIndex, originalActionWeight);

        anim.SetBool("IsCriAtckUp", false);
        anim.SetBool("IsCriAtckUnder", false);
        isInteracting = true;
        isCriticalAttacking = false;
    }

    private void ExecuteParry()
    {
        if (!isInteracting || isHitState || parryCoroutine != null || attackCoroutine != null) return;
        
        if (currentStamina >= parryStaminaCost)
        {
            parryCoroutine = StartCoroutine(ParryRoutine());
        }
    }

    private IEnumerator ParryRoutine()
    {
        currentStamina -= parryStaminaCost;
        currentStamina = Mathf.Max(currentStamina, 0f);

        anim.SetLayerWeight(actionLayerIndex, 0f);
        isInteracting = false;
        isGuarding = false;

        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.attackSounds, 0.5f);
        anim.SetTrigger("IsParry");
        CheckCombatHit(0, true);

        yield return new WaitForSeconds(parryDuration);

        anim.SetLayerWeight(actionLayerIndex, 1f);

        if (guardAction.IsPressed() && currentStamina >= guardStaminaCost)
        {
            isGuarding = true;
        }

        parryCoroutine = null;
        isInteracting = true;
    }

    public void GetParried(float kickImpactTime = 2.5f)
    {
        if (isDead) return;

        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.parrySounds, 1.0f);

        ResetAllTriggers();
        StopAllCoroutines();

        isCriticalAttacking = false;
        isInteracting = false;

        StartCoroutine(GetParriedRoutine(kickImpactTime));
    }

    private IEnumerator GetParriedRoutine(float duration)
    {
        float originalActionWeight = anim.GetLayerWeight(actionLayerIndex);
        anim.SetLayerWeight(actionLayerIndex, 0f); // 피격 전신 모션 보장

        isHitState = true;
        isInteracting = false;
        isGuarding = false;

        anim.speed = 1f;
        isParried = true;
        anim.SetTrigger("IsParried");

        yield return new WaitForSeconds(0.5f);

        float myParryAnimLength = 1.45f;
        anim.speed = myParryAnimLength / duration;
        yield return new WaitForSeconds(duration + 0.1f);

        if (isParried)
        {
            isParried = false;
            anim.speed = 1f;
            isInteracting = true;
            isHitState = false;
            anim.SetLayerWeight(actionLayerIndex, originalActionWeight);
        }
    }

    public void GetCriticalAttacked(float damage, EnemyAI attacker)
    {
        if (isDead) return;

        anim.speed = 1f;

        ResetAllTriggers();
        StopAllCoroutines();

        isInteracting = false;

        if (currentHealth > criticalAttackDamage)
        {
            StartCoroutine(GetCriticalAttackedUpperRoutine(damage, attacker));
        }
        else
        {
            StartCoroutine(GetCriticalAttackedUnderRoutine(damage));
        }
    }

    private IEnumerator GetCriticalAttackedUpperRoutine(float damage, EnemyAI attacker)
    {
        isParried = false;
        isHitState = true;
        isInteracting = false;

        anim.SetLayerWeight(actionLayerIndex, 0f); // 🛠️ 고정: 이제 Update에서 간섭안하므로 전신 피격 유지됨
        anim.SetTrigger("IsCriAtckedUp");

        Vector3 pushDirection = -transform.forward;
        if (attacker != null)
        {
            pushDirection = (transform.position - attacker.transform.position).normalized;
            pushDirection.y = 0;
        }

        Vector3 pushForce = pushDirection * 2.5f;

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
                currentHealth -= damage;
                isDamageApplied = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (controller != null) controller.enabled = true;

        ResetAllTriggers();
        isHitState = false;
        isInteracting = true;
        anim.SetLayerWeight(actionLayerIndex, 1f);
    }

    private IEnumerator GetCriticalAttackedUnderRoutine(float damage)
    {
        isParried = false;
        isHitState = true;
        anim.SetLayerWeight(actionLayerIndex, 0f);

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

        currentHealth -= damage;

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySingleSFX(SoundManager.Instance.victorySound, 1.0f);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (BattleManager.Instance != null)
        {
            Die();
        }
        else
        {
            ResetAllTriggers();
            StopAllCoroutines();
        }
    }

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

    public void TakeDamage(CombatStance attackerStance, EnemyAI attacker)
    {
        if (isDead || isCriticalAttacking) return;

        if (isGuarding && CanBlock(attackerStance))
        {
            SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.clashSounds, 0.8f);
            currentStamina -= guardStaminaCost;

            if (attacker != null)
            {
                attacker.StartCoroutine("RecoilRoutine", 0.8f);
            }

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                isGuarding = false;
            }
            return;
        }

        SoundManager.Instance.PlayRandomSFX(SoundManager.Instance.hitSounds, 0.9f);

        currentHealth -= 20f;
        if (currentHealth <= 0) { Die(); return; }

        ResetAllTriggers();
        StopAndClear(ref attackCoroutine);
        StopAndClear(ref parryCoroutine);
        StopAndClear(ref hitCoroutine);
        StopAndClear(ref criticalAttackCoroutine);

        isCriticalAttacking = false;
        isInteracting = true;

        string hitBool = (attackerStance == CombatStance.Top) ? "IsTopHit" : (attackerStance == CombatStance.Left) ? "IsRightHit" : "IsLeftHit";
        float duration = (attackerStance == CombatStance.Top) ? hitDurationDict.GetValueOrDefault("TopHit", 0.5f) : hitDurationDict.GetValueOrDefault("SideHit", 0.5f);

        duration = duration * 0.5f;
        hitCoroutine = StartCoroutine(PlayerHitRoutine(hitBool, duration));
    }

    private void Die()
    {
        isDead = true;

        velocity = Vector3.zero;
        externalForce = Vector3.zero;

        ResetAllTriggers();
        StopAllCoroutines();

        anim.applyRootMotion = false; // 중요
        anim.SetTrigger("IsDead");

        // controller 유지
        // controller.enabled = false; 제거

        actionMap.Disable();

        StartCoroutine(DeadSequence());
    }

    private IEnumerator DeadSequence()
    {
        yield return new WaitForSeconds(2.0f);

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnPlayerDefeated();
        }
    }

    private bool CanBlock(CombatStance attackerStance)
    {
        if (attackerStance == CombatStance.Top && currentStance == CombatStance.Top) return true;
        if (attackerStance == CombatStance.Left && currentStance == CombatStance.Right) return true;
        if (attackerStance == CombatStance.Right && currentStance == CombatStance.Left) return true;
        return false;
    }

    private IEnumerator PlayerHitRoutine(string triggerName, float duration)
    {
        anim.SetLayerWeight(actionLayerIndex, 0f); // 일반 피격도 전신 처리 유지

        isHitState = true;
        isInteracting = false;
        isGuarding = false;

        anim.SetTrigger(triggerName);
        yield return new WaitForSeconds(duration);

        ResetAllTriggers();
        isHitState = false;
        isInteracting = true;
        anim.SetLayerWeight(actionLayerIndex, 1f);

        hitCoroutine = null;
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

    private void ApplyGravityOnly()
    {
        if (!controller.enabled) return;

        velocity.y += gravity * Time.fixedDeltaTime;
        controller.Move(velocity * Time.fixedDeltaTime);
    }

    void OnDisable() => actionMap.Disable();
}
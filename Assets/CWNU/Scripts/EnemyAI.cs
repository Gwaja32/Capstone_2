using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyAI : MonoBehaviour
{
    public enum AIState { Idle, Chase, Attack, Hit, Dead }

    [Header("State Settings")]
    public AIState currentState = AIState.Idle;
    public CombatStance currentStance = CombatStance.Top;

    [Header("Detection & Layer Mask")]
    public Transform playerTransform;
    public LayerMask playerLayer;    // 인스펙터에서 'Player' 레이어를 꼭 선택해주세요!
    public float chaseRange = 10f;
    public float attackRange = 2.5f;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Combat Settings")]
    public float attackCooldown = 2f;
    private float lastAttackTime;

    [Header("Animation Clips (No Hardcoding)")]
    public AnimationClip topHitClip;
    public AnimationClip sideHitClip;

    [Header("Layer Settings")]
    public string actionLayerName = "Action Layer";
    private int actionLayerIndex;
    public float weightLerpSpeed = 15f;

    [Header("References")]
    public CharacterController controller;
    public Animator anim;

    private Dictionary<string, float> hitDurationDict = new Dictionary<string, float>();
    private Vector3 velocity;
    private bool isDead = false;

    void Start()
    {
        if (anim == null) anim = GetComponent<Animator>();
        if (controller == null) controller = GetComponent<CharacterController>();

        actionLayerIndex = anim.GetLayerIndex(actionLayerName);

        // 애니메이션 클립 길이를 딕셔너리에 동적으로 저장
        if (topHitClip != null) hitDurationDict["TopHit"] = topHitClip.length;
        if (sideHitClip != null) hitDurationDict["SideHit"] = sideHitClip.length;
    }

    void Update()
    {
        if (isDead) return;

        ApplyGravity();

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case AIState.Idle:
                if (distanceToPlayer <= chaseRange) currentState = AIState.Chase;
                break;

            case AIState.Chase:
                HandleRotation(playerTransform.position);
                if (distanceToPlayer <= attackRange)
                {
                    currentState = AIState.Attack;
                }
                else if (distanceToPlayer > chaseRange)
                {
                    currentState = AIState.Idle;
                }
                else
                {
                    MoveTowardsPlayer();
                }
                break;

            case AIState.Attack:
                HandleRotation(playerTransform.position);
                if (distanceToPlayer > attackRange)
                {
                    currentState = AIState.Chase;
                }
                else
                {
                    HandleAttackPattern();
                }
                break;

            case AIState.Hit:
                // 피격 코루틴 중에는 Update에서 별도 이동 안 함
                break;
        }

        UpdateAnimationParams();
        UpdateLayerWeights();
    }

    private void MoveTowardsPlayer()
    {
        Vector3 direction = (playerTransform.position - transform.position).normalized;
        direction.y = 0;
        controller.Move(direction * moveSpeed * Time.deltaTime);
    }

    private void HandleRotation(Vector3 targetPos)
    {
        Vector3 targetDir = targetPos - transform.position;
        targetDir.y = 0;
        if (targetDir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void HandleAttackPattern()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            currentStance = (CombatStance)Random.Range(0, 3);
            ExecuteAttack();
            lastAttackTime = Time.time;
        }
    }

    private void ExecuteAttack()
    {
        // 1. 스탠스 및 레이어 가중치 설정
        anim.SetFloat("Stance", (float)currentStance);
        if (actionLayerIndex != -1) anim.SetLayerWeight(actionLayerIndex, 1f);

        // 2. 공격 트리거
        switch (currentStance)
        {
            case CombatStance.Top: anim.SetTrigger("IsTopAttack"); break;
            case CombatStance.Left: anim.SetTrigger("IsLeftAttack"); break;
            case CombatStance.Right: anim.SetTrigger("IsRightAttack"); break;
        }

        // 3. 플레이어 피격 판정 (Raycast)
        RaycastHit hit;
        // 발바닥이 아닌 가슴 높이(약 0.5m)에서 레이 발사
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;

        if (Physics.Raycast(rayStart, transform.forward, out hit, attackRange, playerLayer))
        {
            TPSFixedMovement player = hit.collider.GetComponent<TPSFixedMovement>();
            if (player != null)
            {
                player.TakeDamage(this.currentStance);
            }
        }

        // Scene 뷰에서 공격 범위를 빨간 선으로 표시
        Debug.DrawRay(rayStart, transform.forward * attackRange, Color.red, 1.0f);
    }

    public void TakeDamage(CombatStance attackerStance)
    {
        if (isDead) return;

        StopAllCoroutines();

        string triggerName = (attackerStance == CombatStance.Top) ? "IsTopHit" : "IsSideHit";
        float duration = 0.5f;

        if (attackerStance == CombatStance.Top && hitDurationDict.ContainsKey("TopHit"))
            duration = hitDurationDict["TopHit"];
        else if (hitDurationDict.ContainsKey("SideHit"))
            duration = hitDurationDict["SideHit"];

        StartCoroutine(HitRoutine(triggerName, duration));
    }

    private IEnumerator HitRoutine(string triggerName, float duration)
    {
        currentState = AIState.Hit;
        anim.SetTrigger(triggerName);

        yield return new WaitForSeconds(duration);

        if (!isDead) currentState = AIState.Chase;
    }

    private void UpdateLayerWeights()
    {
        if (actionLayerIndex == -1) return;

        float targetWeight = (currentState == AIState.Attack || currentState == AIState.Hit) ? 1f : 0f;
        float currentW = anim.GetLayerWeight(actionLayerIndex);
        anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(currentW, targetWeight, Time.deltaTime * weightLerpSpeed));
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateAnimationParams()
    {
        float targetForward = (currentState == AIState.Chase) ? 1f : 0f;
        anim.SetFloat("InputY", targetForward, 0.1f, Time.deltaTime);
        anim.SetFloat("Stance", (float)currentStance);
        anim.SetBool("IsGrounded", controller.isGrounded);
    }
}
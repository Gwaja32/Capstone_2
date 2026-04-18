using GLTFast.Schema;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class TPSFixedMovement : MonoBehaviour
{
    [Header("Targeting & Camera")]
    public CinemachineCamera thirdPersonCam;
    public Transform cameraHolder;
    public Transform targetObject;

    [Header("Aim Step Settings")]
    public float shoulderOffset = 6f;
    public float mouseStepThreshold = 1000f;
    public float mouseDecaySpeed = 3f;
    public float camTransitionSpeed = 15f;

    // 기존 private int aimState = 0; 를 아래로 교체
    public enum CombatStance { Top, Left, Right }
    [Header("Combat Stance")]
    public CombatStance currentStance = CombatStance.Top;

    // 마우스 Y축 누적을 위한 변수 추가
    private float mouseAccumulatorX = 0f;
    private float mouseAccumulatorY = 0f;

    private float currentXOffset = 0f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Foot IK Settings (팔 IK는 제거됨)")]
    public bool useFootIK = true;
    public LayerMask groundLayer;
    public float footOffset = 0.12f;
    public float ikWeight = 1f;
    private float currentIKWeight = 0f;

    [Header("Layer Settings")]
    public string actionLayerName = "Action Layer";
    private int actionLayerIndex;

    [Header("References")]
    public CharacterController controller;
    public Animator anim;
    public bool isInteracting = false;

    private Vector3 velocity;
    private Vector2 moveInput;
    private InputAction moveAction, lookAction;
    private InputActionMap actionMap;

    void Awake()
    {
        actionMap = new InputActionMap("PlayerControls");
        moveAction = actionMap.AddAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        lookAction = actionMap.AddAction("Look", binding: "<Mouse>/delta");
        actionMap.Enable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        actionLayerIndex = anim.GetLayerIndex(actionLayerName);
    }

    void Update()
    {
        bool isGrounded = controller.isGrounded;
        anim.SetBool("IsGrounded", isGrounded);
        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        HandleAimStep();

        if (isInteracting)
        {
            moveInput = moveAction.ReadValue<Vector2>();
            Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x);
            if (move.magnitude > 1f) move.Normalize();

            controller.Move(move * moveSpeed * Time.deltaTime);

            anim.SetFloat("InputX", moveInput.x, 0.1f, Time.deltaTime);
            anim.SetFloat("InputY", moveInput.y, 0.1f, Time.deltaTime);
        }
        else
        {
            anim.SetFloat("InputX", 0, 0.1f, Time.deltaTime);
            anim.SetFloat("InputY", 0, 0.1f, Time.deltaTime);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // [수정됨] 억지로 0으로 끄는 로직 삭제. 평소처럼 자연스럽게 IK 가중치 유지
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, isGrounded ? ikWeight : 0f, Time.deltaTime * 5f);

        UpdateActionLayerWeight();

        // 공격 입력 처리
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isInteracting)
            {
                isInteracting = false;
                ExecuteAttack();
                //CancelInvoke("ResetActionLayer");
                //Invoke("ResetActionLayer", 1.5f);
            }
        }

        anim.SetFloat("Stance", (float)currentStance);
    }

    private void ExecuteAttack()
    {
        if (anim == null) return;

        switch (currentStance)
        {
            case CombatStance.Top:
                anim.SetTrigger("IsTopAttack");
                break;
            case CombatStance.Left:
                anim.SetTrigger("IsLeftAttack");
                break;
            case CombatStance.Right:
                anim.SetTrigger("IsRightAttack");
                break;
        }
    }

    void LateUpdate()
    {
        if (targetObject == null || cameraHolder == null) return;
        if (isInteracting)
        {
            Vector3 targetDir = targetObject.position - transform.position;
            targetDir.y = 0;
            if (targetDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(targetDir);
        }
        UpdateCameraTransform();
    }

    private void HandleAimStep()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        // 입력이 있을 때만 누적
        if (lookInput.sqrMagnitude > 0.001f)
        {
            mouseAccumulatorX += lookInput.x;
            mouseAccumulatorY += lookInput.y;

            // 임계값 도달 시
            if (Mathf.Abs(mouseAccumulatorX) > mouseStepThreshold || Mathf.Abs(mouseAccumulatorY) > mouseStepThreshold)
            {
                DetermineStance();
                // 상태 변화 후 누적치 초기화 대신 일정량만 감소시켜 연속적인 조작 유도 가능
                mouseAccumulatorX = 0;
                mouseAccumulatorY = 0;
            }
        }
        else
        {
            // 입력 없을 때 감쇠
            mouseAccumulatorX = Mathf.Lerp(mouseAccumulatorX, 0, Time.deltaTime * mouseDecaySpeed);
            mouseAccumulatorY = Mathf.Lerp(mouseAccumulatorY, 0, Time.deltaTime * mouseDecaySpeed);
        }
    }

    private void DetermineStance()
    {
        // 각도 기반 판정 (조금 더 정교한 포 아너 스타일)
        // Y가 일정 이상 크면 무조건 Top 우선 순위
        if (mouseAccumulatorY > mouseStepThreshold && mouseAccumulatorY > Mathf.Abs(mouseAccumulatorX))
        {
            currentStance = CombatStance.Top;
        }
        else if (Mathf.Abs(mouseAccumulatorX) > mouseStepThreshold)
        {
            currentStance = (mouseAccumulatorX < 0) ? CombatStance.Left : CombatStance.Right;
        }
    }

    private void UpdateCameraTransform()
    {
        if (targetObject == null || cameraHolder == null) return;

        float targetX = 0;
        if (currentStance == CombatStance.Left) targetX = shoulderOffset;
        else if (currentStance == CombatStance.Right) targetX = -shoulderOffset;
        // Top일 때는 중앙(0) 유지

        currentXOffset = Mathf.Lerp(currentXOffset, targetX, Time.deltaTime * camTransitionSpeed);
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);
        
        Vector3 lookPoint = transform.position + transform.forward * 50f;
        cameraHolder.LookAt(lookPoint);
    }

    private void UpdateActionLayerWeight()
    {
        if (anim == null || actionLayerIndex == -1) return;

        // 공격 중일 때도 아주 약간의 블렌딩 시간을 주는 것이 시각적으로 부드럽습니다.
        float targetWeight = isInteracting ? 1.0f : 0.0f;
        float currentW = anim.GetLayerWeight(actionLayerIndex);

        // 공격 시에는 더 빠르게(20f), 복귀 시에는 적당히(10f) 블렌딩
        float lerpSpeed = isInteracting ? 10f : 20f;
        anim.SetLayerWeight(actionLayerIndex, Mathf.MoveTowards(currentW, targetWeight, Time.deltaTime * lerpSpeed));
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useFootIK || anim == null) return;

        // 가중치를 먼저 적용
        anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, currentIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, currentIKWeight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, currentIKWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, currentIKWeight);

        if (currentIKWeight > 0.01f)
        {
            ApplyFootIK(AvatarIKGoal.LeftFoot);
            ApplyFootIK(AvatarIKGoal.RightFoot);
        }
    }

    private void ApplyFootIK(AvatarIKGoal goal)
    {
        Vector3 anklePos = anim.GetIKPosition(goal);

        // 발 위에서 아래로 레이캐스트 (안정성을 위해 1f 높이에서 쏨)
        if (Physics.Raycast(new Ray(anklePos + Vector3.up * 1f, Vector3.down), out RaycastHit hit, 2f, groundLayer))
        {
            Vector3 targetPos = hit.point;
            targetPos.y += footOffset;

            // [핵심 해결] 발이 허공에 떠 있을 때(발차기, 크게 내딛기 등)는 
            // 억지로 바닥으로 끌어내리지 않고 애니메이션 원본 높이(anklePos)를 그대로 사용합니다.
            if (anklePos.y > targetPos.y)
            {
                targetPos.y = anklePos.y;
            }

            anim.SetIKPosition(goal, targetPos);
            anim.SetIKRotation(goal, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal));
        }
    }

    //private void ResetActionLayer()
    //{
    //    // 시간이 다 되면 다시 평소(걷기/대기) 상태로 돌아갑니다.
    //    isInteracting = true;
    //}

    void OnDisable() => actionMap.Disable();
}
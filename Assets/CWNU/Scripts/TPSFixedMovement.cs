using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using Unity.Cinemachine;

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

    private int aimState = 0;
    private float mouseAccumulator = 0f;
    private float currentXOffset = 0f;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("IK Settings")]
    public bool useFootIK = true;
    public bool useHandIK = true;
    public LayerMask groundLayer;
    public float footOffset = 0.12f;
    public float ikWeight = 1f;
    private float currentIKWeight = 0f;
    public TwoBoneIKConstraint leftArmIK, rightArmIK;
    public string armsLayerName = "Arms Layer";
    private int armsLayerIndex;

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
        armsLayerIndex = anim.GetLayerIndex(armsLayerName);
    }

    void Update()
    {
        bool isGrounded = controller.isGrounded;
        anim.SetBool("IsGrounded", isGrounded);
        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        HandleAimStep();

        moveInput = moveAction.ReadValue<Vector2>();
        Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x);
        if (move.magnitude > 1f) move.Normalize();

        controller.Move(move * moveSpeed * Time.deltaTime);
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        anim.SetFloat("InputX", moveInput.x, 0.1f, Time.deltaTime);
        anim.SetFloat("InputY", moveInput.y, 0.1f, Time.deltaTime);

        UpdateArmsLayerWeight();
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, isGrounded ? ikWeight : 0f, Time.deltaTime * 5f);
    }

    void LateUpdate()
    {
        if (targetObject == null || cameraHolder == null) return;

        // 1. 캐릭터는 오직 좌우로만 타겟을 봅니다 (뒤로 눕기 방지)
        Vector3 targetDir = targetObject.position - transform.position;
        targetDir.y = 0;
        if (targetDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(targetDir);

        // 2. 카메라 위치 및 회전 업데이트
        UpdateCameraTransform();
    }

    private void HandleAimStep()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        mouseAccumulator -= lookInput.x;

        if (Mathf.Abs(lookInput.x) < 0.01f)
            mouseAccumulator = Mathf.Lerp(mouseAccumulator, 0, Time.deltaTime * mouseDecaySpeed);

        if (mouseAccumulator > mouseStepThreshold)
        {
            if (aimState < 1) aimState++;
            mouseAccumulator = 0f;
        }
        else if (mouseAccumulator < -mouseStepThreshold)
        {
            if (aimState > -1) aimState--;
            mouseAccumulator = 0f;
        }
    }

    private void UpdateCameraTransform()
    {
        // X축 위치를 타겟 상관없이 고정 이동
        float targetX = aimState * shoulderOffset;
        currentXOffset = Mathf.Lerp(currentXOffset, targetX, Time.deltaTime * camTransitionSpeed);
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);

        // [핵심] 좌우 에임 동작 + 밀림 방지 동시 해결
        // 타겟의 좌표를 직접 쳐다보지 않고, 캐릭터 정면 50m 앞을 바라보게 합니다.
        // 이렇게 하면 캐릭터가 좌우로 회전할 때 에임도 완벽하게 타겟을 따라가면서,
        // 카메라 각도가 안으로 꺾이지 않아 밀림 현상이 물리적으로 사라집니다.
        Vector3 lookPoint = transform.position + transform.forward * 50f;
        cameraHolder.LookAt(lookPoint);
    }

    private void UpdateArmsLayerWeight()
    {
        if (anim == null || armsLayerIndex == -1) return;
        float targetW = isInteracting ? 1.0f : 0.0f;
        float nextW = Mathf.Lerp(anim.GetLayerWeight(armsLayerIndex), targetW, Time.deltaTime * 10f);
        anim.SetLayerWeight(armsLayerIndex, nextW);
        if (useHandIK && leftArmIK != null && rightArmIK != null)
        {
            leftArmIK.weight = nextW;
            rightArmIK.weight = nextW;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useFootIK || anim == null) return;
        ApplyFootIK(AvatarIKGoal.LeftFoot, currentIKWeight);
        ApplyFootIK(AvatarIKGoal.RightFoot, currentIKWeight);
    }

    private void ApplyFootIK(AvatarIKGoal goal, float weight)
    {
        anim.SetIKPositionWeight(goal, weight);
        anim.SetIKRotationWeight(goal, weight);
        Vector3 anklePos = anim.GetIKPosition(goal);
        if (Physics.Raycast(new Ray(anklePos + Vector3.up * 0.5f, Vector3.down), out RaycastHit hit, 1.2f, groundLayer))
        {
            Vector3 targetPos = hit.point; targetPos.y += footOffset;
            anim.SetIKPosition(goal, (anklePos.y > targetPos.y) ? Vector3.Lerp(targetPos, anklePos, 0.5f) : targetPos);
            anim.SetIKRotation(goal, Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal));
        }
    }

    void OnDisable() => actionMap.Disable();
}
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using Unity.Cinemachine;

public class FPSMovement : MonoBehaviour
{
    [Header("Cameras")]
    public bool isFirstPerson = true;
    public CinemachineCamera firstPersonCam;
    public CinemachineCamera thirdPersonCam;
    public Transform cameraHolder;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float mouseSensitivity = 0.1f;
    public float turnSpeed = 15f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Foot IK Settings")]
    public bool useFootIK = true;
    public LayerMask groundLayer;
    [Range(0, 0.5f)] public float footOffset = 0.12f;
    [Range(0, 1f)] public float ikWeight = 1f;
    private float currentIKWeight = 0f; // 부드러운 가중치 전환용

    [Header("Arm & Hand IK Settings")]
    public bool useHandIK = true;
    public TwoBoneIKConstraint leftArmIK;
    public TwoBoneIKConstraint rightArmIK;
    public string armsLayerName = "Arms Layer";
    private int armsLayerIndex;

    [Header("References")]
    public CharacterController controller;
    public Animator anim;
    public Camera mainCamera;

    [Header("Spine Rotation Distribution")]
    public Transform spline1;
    public Transform spline2;
    public Transform neck;

    private Vector3 velocity;
    private float xRotation = 0f;
    private float yRotation = 0f;
    private bool isGrounded;
    private Vector2 moveInput;

    private InputAction moveAction, lookAction, jumpAction, toggleAction;
    private InputActionMap actionMap;

    [Header("State")]
    public bool isInteracting = false; // 도끼질이나 도구를 들고 있을 때 true로 변경

    void Awake()
    {
        actionMap = new InputActionMap("PlayerControls");
        moveAction = actionMap.AddAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        lookAction = actionMap.AddAction("Look", binding: "<Mouse>/delta");
        jumpAction = actionMap.AddAction("Jump", binding: "<Keyboard>/space");
        toggleAction = actionMap.AddAction("Toggle", binding: "<Keyboard>/v");
        actionMap.Enable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        if (transform.eulerAngles.y != 0) yRotation = transform.eulerAngles.y;
        armsLayerIndex = anim.GetLayerIndex(armsLayerName);
        UpdateCameraView();
    }

    void Update()
    {
        if (toggleAction.triggered) { isFirstPerson = !isFirstPerson; UpdateCameraView(); }

        isGrounded = controller.isGrounded;
        anim.SetBool("IsGrounded", isGrounded);

        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        if (isFirstPerson)
        {
            yRotation += lookInput.x * mouseSensitivity;
            xRotation -= lookInput.y * mouseSensitivity;
            xRotation = Mathf.Clamp(xRotation, -80f, 80f);
            transform.rotation = Quaternion.Euler(0, yRotation, 0);
            firstPersonCam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        }
        else
        {
            float targetY = mainCamera.transform.eulerAngles.y;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetY, 0), turnSpeed * Time.deltaTime);
        }

        moveInput = moveAction.ReadValue<Vector2>();
        Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x);
        if (move.magnitude > 1f)
        {
            move.Normalize();
        }
        controller.Move(move * moveSpeed * Time.deltaTime);

        if (jumpAction.triggered && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            anim.SetTrigger("Jump");
            currentIKWeight = 0f; // 점프 순간 IK 즉시 차단
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        anim.SetFloat("VerticalVelocity", velocity.y);
        anim.SetFloat("InputX", moveInput.x, 0.1f, Time.deltaTime);
        anim.SetFloat("InputY", moveInput.y, 0.1f, Time.deltaTime);

        UpdateArmsLayerWeight();

        // IK 가중치 로직 개선: 공중이면 0, 땅이면 서서히 ikWeight로 복귀
        float targetWeight = isGrounded ? ikWeight : 0f;
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, targetWeight, Time.deltaTime * 5f);
    }

    private void UpdateArmsLayerWeight()
    {
        if (anim == null || armsLayerIndex == -1) return;

        float targetW = 0f;

        if (isFirstPerson)
        {
            // 1인칭: 아이템을 들고 있다면 1, 맨손이면 0.5 정도로 낮춰서 애니메이션을 섞음
            targetW = isInteracting ? 1.0f : 0.0f;
        }
        else
        {
            // 3인칭: 도구를 쓸 때만 IK를 켜고, 평소(걷기)에는 0으로 해서 닭 동작 방지
            targetW = isInteracting ? 1.0f : 0.0f;
        }

        float currentLayerW = anim.GetLayerWeight(armsLayerIndex);
        float smoothedW = Mathf.Lerp(currentLayerW, targetW, Time.deltaTime * 10f);

        anim.SetLayerWeight(armsLayerIndex, smoothedW);

        if (useHandIK && leftArmIK != null)
        {
            // 핵심: 평소에 0이면 닭처럼 걷지 않고 애니메이션대로 걷습니다.
            leftArmIK.weight = smoothedW;
            rightArmIK.weight = smoothedW;
        }
    }

    void LateUpdate()
    {
        if (isFirstPerson)
        {
            if (spline1) spline1.localRotation *= Quaternion.Euler(xRotation * 0.3f, 0, 0);
            if (spline2) spline2.localRotation *= Quaternion.Euler(-xRotation * 0.3f, 0, 0);
            if (neck) neck.localRotation *= Quaternion.Euler(xRotation * 0.7f, 0, 0);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!useFootIK || anim == null) return;

        // Update에서 계산된 부드러운 가중치를 적용
        ApplyFootIK(AvatarIKGoal.LeftFoot, currentIKWeight);
        ApplyFootIK(AvatarIKGoal.RightFoot, currentIKWeight);
    }

    private void ApplyFootIK(AvatarIKGoal goal, float weight)
    {
        anim.SetIKPositionWeight(goal, weight);
        anim.SetIKRotationWeight(goal, weight);

        if (weight <= 0.001f) return;

        // 원본 애니메이션의 발 위치를 기준으로 레이를 쏨
        Vector3 anklePos = anim.GetIKPosition(goal);
        Ray ray = new Ray(anklePos + Vector3.up * 0.5f, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, 1.2f, groundLayer))
        {
            // 발이 지면보다 높이 있을 때만 IK 위치를 조정 (걷는 도중 발이 들리는 동작 보호)
            Vector3 targetPos = hit.point;
            targetPos.y += footOffset;

            // 원본 애니메이션 발 높이가 지면보다 훨씬 높다면(걷기 중 발 들기), IK를 적용하지 않거나 섞음
            if (anklePos.y > targetPos.y)
            {
                // 발이 들리는 중에는 애니메이션 위치를 우선시함 (뻣뻣함 방지)
                anim.SetIKPosition(goal, Vector3.Lerp(targetPos, anklePos, 0.5f));
            }
            else
            {
                anim.SetIKPosition(goal, targetPos);
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
            anim.SetIKRotation(goal, Quaternion.LookRotation(forward, hit.normal));
        }
    }

    private void UpdateCameraView()
    {
        if (firstPersonCam == null || thirdPersonCam == null) return;
        firstPersonCam.Priority = isFirstPerson ? 20 : 0;
        thirdPersonCam.Priority = isFirstPerson ? 0 : 20;

        int playerLayerIndex = LayerMask.NameToLayer("Player");
        if (playerLayerIndex != -1)
        {
            if (isFirstPerson) mainCamera.cullingMask &= ~(1 << playerLayerIndex);
            else mainCamera.cullingMask |= (1 << playerLayerIndex);
        }
    }

    void OnDisable() => actionMap.Disable();
}
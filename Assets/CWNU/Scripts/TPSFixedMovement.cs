using UnityEngine;
using UnityEngine.InputSystem;
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

        moveInput = moveAction.ReadValue<Vector2>();
        Vector3 move = (transform.forward * moveInput.y + transform.right * moveInput.x);
        if (move.magnitude > 1f) move.Normalize();

        controller.Move(move * moveSpeed * Time.deltaTime);
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        anim.SetFloat("InputX", moveInput.x, 0.1f, Time.deltaTime);
        anim.SetFloat("InputY", moveInput.y, 0.1f, Time.deltaTime);

        // 상체 레이어 가중치만 업데이트
        UpdateActionLayerWeight();
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, isGrounded ? ikWeight : 0f, Time.deltaTime * 5f);
    }

    void LateUpdate()
    {
        if (targetObject == null || cameraHolder == null) return;
        Vector3 targetDir = targetObject.position - transform.position;
        targetDir.y = 0;
        if (targetDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(targetDir);
        UpdateCameraTransform();
    }

    private void HandleAimStep()
    {
        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        mouseAccumulator -= lookInput.x;
        if (Mathf.Abs(lookInput.x) < 0.01f)
            mouseAccumulator = Mathf.Lerp(mouseAccumulator, 0, Time.deltaTime * mouseDecaySpeed);

        if (mouseAccumulator > mouseStepThreshold) { if (aimState < 1) aimState++; mouseAccumulator = 0f; }
        else if (mouseAccumulator < -mouseStepThreshold) { if (aimState > -1) aimState--; mouseAccumulator = 0f; }
    }

    private void UpdateCameraTransform()
    {
        float targetX = aimState * shoulderOffset;
        currentXOffset = Mathf.Lerp(currentXOffset, targetX, Time.deltaTime * camTransitionSpeed);
        cameraHolder.localPosition = new Vector3(currentXOffset, cameraHolder.localPosition.y, cameraHolder.localPosition.z);
        Vector3 lookPoint = transform.position + transform.forward * 50f;
        cameraHolder.LookAt(lookPoint);
    }

    private void UpdateActionLayerWeight()
    {
        if (anim == null || actionLayerIndex == -1) return;

        float targetW = isInteracting ? 1.0f : 0.0f;
        float currentW = anim.GetLayerWeight(actionLayerIndex);
        float nextW = Mathf.Lerp(currentW, targetW, Time.deltaTime * 10f);
        anim.SetLayerWeight(actionLayerIndex, nextW);
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
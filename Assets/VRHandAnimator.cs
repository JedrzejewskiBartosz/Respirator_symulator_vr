using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[RequireComponent(typeof(Animator))]
public class VRHandAnimator : MonoBehaviour
{
    public enum HandSide { Left, Right }

    [Header("Strona dłoni")]
    public HandSide handSide = HandSide.Left;

    [Header("Śledzenie Pozycji i Rotacji")]
    [Tooltip("Czy dłoń ma automatycznie śledzić pozycję kontrolera, jeśli rodzic nie jest śledzony")]
    public bool autoTrackTransform = true;

    [Header("Input System Actions (Z XRI Default Input Actions)")]
    [Tooltip("Akcja odpowiadająca za spust / palec wskazujący (np. Activate Value)")]
    public InputActionProperty triggerAction;

    [Tooltip("Akcja odpowiadająca za boczny uchwyt / zaciśnięcie dłoni (np. Select Value)")]
    public InputActionProperty gripAction;

    [Header("Płynność animacji")]
    [Range(5f, 35f)]
    public float animationSpeed = 25f;

    [Header("Testowanie w Edytorze (Play mode)")]
    [Range(0f, 1f)] public float testTrigger = 0f;
    [Range(0f, 1f)] public float testGrip = 0f;

    private Animator animator;
    private float currentTrigger = 0f;
    private float currentGrip = 0f;

    private static readonly int TriggerParam = Animator.StringToHash("Trigger");
    private static readonly int GripParam = Animator.StringToHash("Grip");

    void Awake()
    {
        FindAndSetupAnimator();
    }

    void Start()
    {
        FindAndSetupAnimator();
        EnableActions();
    }

    void OnEnable()
    {
        EnableActions();
    }

    void OnDisable()
    {
        if (triggerAction.action != null) triggerAction.action.Disable();
        if (gripAction.action != null) gripAction.action.Disable();
    }

    private void FindAndSetupAnimator()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private void EnableActions()
    {
        if (triggerAction.action != null && !triggerAction.action.enabled)
        {
            triggerAction.action.Enable();
        }
        if (gripAction.action != null && !gripAction.action.enabled)
        {
            gripAction.action.Enable();
        }
    }

    void Update()
    {
        UpdateTracking();
        UpdateAnimation();
    }

    private void UpdateTracking()
    {
        if (!autoTrackTransform) return;

        // Jeśli rodzic ma już TrackedPoseDriver, nie nadpisujemy pozycji
        if (transform.parent != null && transform.parent.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() != null)
        {
            return;
        }

        XRNode node = (handSide == HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 pos))
            {
                transform.localPosition = pos;
            }
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rot))
            {
                transform.localRotation = rot;
            }
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            FindAndSetupAnimator();
            if (animator == null) return;
        }

        float targetTrigger = 0f;
        float targetGrip = 0f;

        // 1. Suwaki testowe w Inspektorze
        if (testTrigger > 0f || testGrip > 0f)
        {
            targetTrigger = testTrigger;
            targetGrip = testGrip;
        }
        else
        {
            // 2. Odczyt z przypisanych akcji Input Systemu
            if (triggerAction.action != null)
            {
                if (!triggerAction.action.enabled) triggerAction.action.Enable();
                try
                {
                    targetTrigger = triggerAction.action.ReadValue<float>();
                }
                catch
                {
                    targetTrigger = triggerAction.action.IsPressed() ? 1f : 0f;
                }
            }

            if (gripAction.action != null)
            {
                if (!gripAction.action.enabled) gripAction.action.Enable();
                try
                {
                    targetGrip = gripAction.action.ReadValue<float>();
                }
                catch
                {
                    targetGrip = gripAction.action.IsPressed() ? 1f : 0f;
                }
            }

            // 3. Fallback: bezpośredni odczyt ze sterowników InputDevices (Legacy / OpenXR)
            if (targetTrigger <= 0.001f && targetGrip <= 0.001f)
            {
                XRNode node = (handSide == HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;
                UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid)
                {
                    device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out targetTrigger);
                    device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out targetGrip);
                }
            }
        }

        // Płynne zginanie palców w animacji
        currentTrigger = Mathf.MoveTowards(currentTrigger, targetTrigger, Time.deltaTime * animationSpeed);
        currentGrip = Mathf.MoveTowards(currentGrip, targetGrip, Time.deltaTime * animationSpeed);

        animator.SetFloat(TriggerParam, currentTrigger);
        animator.SetFloat(GripParam, currentGrip);
    }
}

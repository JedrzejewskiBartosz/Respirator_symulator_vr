using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[RequireComponent(typeof(Animator))]
public class AnimatedHandController : MonoBehaviour
{
    public enum HandType { Left, Right }
    
    [Header("Typ Dłoni")]
    public HandType handType = HandType.Left;

    [Header("Komponent Animator")]
    public Animator animator;

    [Header("Input System Actions (Z XRI Default Input Actions)")]
    public InputActionProperty triggerAction;
    public InputActionProperty gripAction;

    [Header("Ustawienia Animacji")]
    [Range(5f, 30f)]
    public float animationSpeed = 20f;

    [Header("Debug w Edytorze (bez gogli)")]
    [Range(0f, 1f)] public float debugTrigger = 0f;
    [Range(0f, 1f)] public float debugGrip = 0f;
    public bool useDebugValues = false;

    private float currentTrigger = 0f;
    private float currentGrip = 0f;

    private static readonly int TriggerHash = Animator.StringToHash("Trigger");
    private static readonly int GripHash = Animator.StringToHash("Grip");

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            // Always animate so hands don't freeze outside culling bounds
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    void OnEnable()
    {
        if (triggerAction.action != null) triggerAction.action.Enable();
        if (gripAction.action != null) gripAction.action.Enable();
    }

    void OnDisable()
    {
        if (triggerAction.action != null) triggerAction.action.Disable();
        if (gripAction.action != null) gripAction.action.Disable();
    }

    void Update()
    {
        if (animator == null) return;

        float targetTrigger = 0f;
        float targetGrip = 0f;

        if (useDebugValues)
        {
            targetTrigger = debugTrigger;
            targetGrip = debugGrip;
        }
        else
        {
            // 1. Odczyt z akcji przypisanych w Inspektorze (Input System)
            if (triggerAction.action != null && triggerAction.action.enabled)
            {
                targetTrigger = triggerAction.action.ReadValue<float>();
            }

            if (gripAction.action != null && gripAction.action.enabled)
            {
                targetGrip = gripAction.action.ReadValue<float>();
            }

            // 2. Bezpośredni odczyt z InputDevices (OpenXR / Oculus / Meta Quest / SteamVR)
            if (Mathf.Approximately(targetTrigger, 0f) && Mathf.Approximately(targetGrip, 0f))
            {
                XRNode node = (handType == HandType.Left) ? XRNode.LeftHand : XRNode.RightHand;
                UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid)
                {
                    device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out targetTrigger);
                    device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out targetGrip);
                }
            }
        }

        // Płynny ruch palców
        currentTrigger = Mathf.MoveTowards(currentTrigger, targetTrigger, Time.deltaTime * animationSpeed);
        currentGrip = Mathf.MoveTowards(currentGrip, targetGrip, Time.deltaTime * animationSpeed);

        animator.SetFloat(TriggerHash, currentTrigger);
        animator.SetFloat(GripHash, currentGrip);
    }
}

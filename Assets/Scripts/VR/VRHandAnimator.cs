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

    [Header("Poza Wskazywania na Trigger (Pointing Pose)")]
    [Tooltip("Gdy wciśnięty jest Trigger: dłoń zaciska się w pięść (Grip), a palec wskazujący wyprostowuje się do wciskania")]
    public bool pointingPoseOnTrigger = true;

    [Header("Płynność animacji")]
    [Range(5f, 45f)]
    public float animationSpeed = 30f;

    [Header("Testowanie w Edytorze (Play mode)")]
    [Range(0f, 1f)] public float testTrigger = 0f;
    [Range(0f, 1f)] public float testGrip = 0f;

    public bool IsTriggerPressed => (testTrigger > 0.35f) || (currentTrigger > 0.35f);
    public float TriggerValue => Mathf.Max(testTrigger, currentTrigger);

    private Animator animator;
    private float currentTrigger = 0f;
    private float currentGrip = 0f;

    private Transform index1Bone;
    private Transform index2Bone;
    private Transform index3Bone;

    private Quaternion openIndex1Rot;
    private Quaternion openIndex2Rot;
    private Quaternion openIndex3Rot;
    private bool bonesInitialized = false;

    private static readonly int TriggerParam = Animator.StringToHash("Trigger");
    private static readonly int GripParam = Animator.StringToHash("Grip");

    void Awake()
    {
        FindAndSetupAnimator();
        FindIndexBones();
    }

    void Start()
    {
        FindAndSetupAnimator();
        FindIndexBones();
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

    private void FindIndexBones()
    {
        if (bonesInitialized) return;

        string p = (handSide == HandSide.Left) ? "l" : "r";
        string[] i1Names = { $"hands:b_{p}_index1", $"b_{p}_index1", "index1", "index_01" };
        string[] i2Names = { $"hands:b_{p}_index2", $"b_{p}_index2", "index2", "index_02" };
        string[] i3Names = { $"hands:b_{p}_index3", $"b_{p}_index3", "index3", "index_03" };

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        foreach (var t in allChildren)
        {
            string n = t.name.ToLower();
            if (index1Bone == null && MatchesAny(n, i1Names)) index1Bone = t;
            if (index2Bone == null && MatchesAny(n, i2Names)) index2Bone = t;
            if (index3Bone == null && MatchesAny(n, i3Names)) index3Bone = t;
        }

        if (index1Bone != null) openIndex1Rot = index1Bone.localRotation;
        if (index2Bone != null) openIndex2Rot = index2Bone.localRotation;
        if (index3Bone != null) openIndex3Rot = index3Bone.localRotation;

        bonesInitialized = (index1Bone != null);
    }

    private bool MatchesAny(string name, string[] patterns)
    {
        foreach (var p in patterns)
        {
            if (name.Equals(p, System.StringComparison.OrdinalIgnoreCase) || name.EndsWith(p, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
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

        float rawTrigger = 0f;
        float rawGrip = 0f;

        // 1. Suwaki testowe w Edytorze
        if (testTrigger > 0f || testGrip > 0f)
        {
            rawTrigger = testTrigger;
            rawGrip = testGrip;
        }
        else
        {
            // 2. Odczyt z Input Systemu
            if (triggerAction.action != null)
            {
                if (!triggerAction.action.enabled) triggerAction.action.Enable();
                try { rawTrigger = triggerAction.action.ReadValue<float>(); }
                catch { rawTrigger = triggerAction.action.IsPressed() ? 1f : 0f; }
            }

            if (gripAction.action != null)
            {
                if (!gripAction.action.enabled) gripAction.action.Enable();
                try { rawGrip = gripAction.action.ReadValue<float>(); }
                catch { rawGrip = gripAction.action.IsPressed() ? 1f : 0f; }
            }

            // 3. Fallback: InputDevices
            if (rawTrigger <= 0.001f && rawGrip <= 0.001f)
            {
                XRNode node = (handSide == HandSide.Left) ? XRNode.LeftHand : XRNode.RightHand;
                UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid)
                {
                    device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out rawTrigger);
                    device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out rawGrip);
                }
            }
        }

        // Płynna interpolacja wejść
        currentTrigger = Mathf.MoveTowards(currentTrigger, rawTrigger, Time.deltaTime * animationSpeed);
        currentGrip = Mathf.MoveTowards(currentGrip, rawGrip, Time.deltaTime * animationSpeed);

        // Poza wskazywania (Pointing Pose):
        // Jeśli wciskamy Trigger, animujemy dłoń w pięść (Grip drive), a w LateUpdate trzymamy palec wskazujący wyprostowany!
        float animatorGrip = currentGrip;
        float animatorTrigger = currentTrigger;

        if (pointingPoseOnTrigger)
        {
            // Gdy trigger jest wciśnięty, zaciśnij resztę dłoni w pięść (jak przy Grip)
            animatorGrip = Mathf.Max(currentGrip, currentTrigger);
            // Wyłączamy zginanie palca wskazującego w blend tree na rzecz naszej wyprostowanej pozy
            animatorTrigger = (currentGrip > 0.5f) ? currentTrigger : 0f;
        }

        animator.SetFloat(TriggerParam, animatorTrigger);
        animator.SetFloat(GripParam, animatorGrip);
    }

    void LateUpdate()
    {
        // 4. Proceduralne prostowanie kości palca wskazującego w pozie celowania (Trigger Pointing Pose)
        if (pointingPoseOnTrigger && bonesInitialized)
        {
            // Gdy trigger jest wciśnięty, a grip NIE jest wciśnięty na maksa
            float pointWeight = Mathf.Clamp01(currentTrigger * (1f - currentGrip * 0.7f));

            if (pointWeight > 0.001f)
            {
                if (index1Bone != null)
                {
                    index1Bone.localRotation = Quaternion.Slerp(index1Bone.localRotation, openIndex1Rot, pointWeight);
                }
                if (index2Bone != null)
                {
                    index2Bone.localRotation = Quaternion.Slerp(index2Bone.localRotation, openIndex2Rot, pointWeight);
                }
                if (index3Bone != null)
                {
                    index3Bone.localRotation = Quaternion.Slerp(index3Bone.localRotation, openIndex3Rot, pointWeight);
                }
            }
        }
    }
}

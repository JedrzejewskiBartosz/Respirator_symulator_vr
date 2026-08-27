using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Collider))]
public class RespiratorDirectKnob : MonoBehaviour
{
    private XRGrabInteractable grabInteractable;
    private IXRSelectInteractor currentInteractor;
    private Transform currentInteractorTransform;
    private MeshRenderer meshRenderer;
    private Collider dialCollider;

    [Header("Logika Gry")]
    [Tooltip("Główny menedżer zarządzający awariami")]
    public RespiratorEventManager eventManager;

    [Header("Ustawienia Pokrętła")]
    [Tooltip("Lokalna oś obrotu pokrętła")]
    public Vector3 rotationAxis = Vector3.forward;

    [Header("Zamrażanie Dłoni (Hand Snapping)")]
    [Tooltip("Dystans zerwania w metrach (np. 0.25m = 25cm). Jeśli ręka oddali się dalej, chwyt puszcza")]
    public float maxBreakDistance = 0.25f;

    [Tooltip("Offset pozycji dłoni względem gałki pokrętła")]
    public Vector3 handSnapOffset = new Vector3(0f, 0f, 0.02f);

    [Header("Efekty Wizualne")]
    public Color highlightColor = Color.cyan;
    private Color originalColor = Color.white;

    private Vector3 initialHandDirection;
    private Quaternion initialKnobRotation;
    private float currentDialAngle = 0f;

    private Transform grabbedHandModel;
    private Quaternion initialHandModelLocalRot;
    private VRPhysicsHand grabbedPhysicsHand;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        meshRenderer = GetComponent<MeshRenderer>();
        dialCollider = GetComponent<Collider>();

        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            originalColor = meshRenderer.sharedMaterial.color;
        }

        if (grabInteractable != null)
        {
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            grabInteractable.throwOnDetach = false;

            grabInteractable.selectEntered.AddListener(OnGrab);
            grabInteractable.selectExited.AddListener(OnRelease);
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        currentInteractor = args.interactorObject;
        currentInteractorTransform = currentInteractor.transform;
        initialKnobRotation = transform.localRotation;

        // 1. Wyznaczamy wektor początkowy dłoni w płaszczyźnie obrotu
        Vector3 handPosLocal = transform.InverseTransformPoint(currentInteractorTransform.position);
        initialHandDirection = Vector3.ProjectOnPlane(handPosLocal, rotationAxis).normalized;

        if (initialHandDirection == Vector3.zero)
        {
            initialHandDirection = Vector3.up;
        }

        // 2. Znajdujemy model dłoni na kontrolerze do zamrożenia
        FindAndSnapHandModel(currentInteractorTransform);

        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = highlightColor;
        }

        Debug.Log($"[RespiratorKnob] Złapano pokrętło przez {currentInteractorTransform.name}. Dłoń zamrożona przy osi.");
    }

    private void FindAndSnapHandModel(Transform interactorRoot)
    {
        grabbedHandModel = null;
        grabbedPhysicsHand = null;

        // Szukamy modelu dłoni (VRHandAnimator lub Model)
        VRHandAnimator anim = interactorRoot.GetComponentInChildren<VRHandAnimator>(true);
        if (anim != null)
        {
            grabbedHandModel = anim.transform;
        }
        else
        {
            foreach (Transform child in interactorRoot)
            {
                string n = child.name.ToLower();
                if (n.Contains("hand") && n.Contains("model"))
                {
                    grabbedHandModel = child;
                    break;
                }
            }
        }

        if (grabbedHandModel != null)
        {
            initialHandModelLocalRot = grabbedHandModel.localRotation;
            grabbedPhysicsHand = grabbedHandModel.GetComponent<VRPhysicsHand>();
            if (grabbedPhysicsHand != null)
            {
                grabbedPhysicsHand.enabled = false; // Wyłączamy physics solver na czas chwytu
            }
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        ReleaseHandModel();

        currentInteractor = null;
        currentInteractorTransform = null;

        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = originalColor;
        }

        Debug.Log("[RespiratorKnob] Zwolniono pokrętło. Dłoń odmrożona.");
    }

    private void ReleaseHandModel()
    {
        if (grabbedHandModel != null && currentInteractorTransform != null)
        {
            grabbedHandModel.localPosition = Vector3.zero;
            grabbedHandModel.localRotation = initialHandModelLocalRot;

            if (grabbedPhysicsHand != null)
            {
                grabbedPhysicsHand.TeleportToTarget();
                grabbedPhysicsHand.enabled = true;
            }
        }

        grabbedHandModel = null;
        grabbedPhysicsHand = null;
    }

    void Update()
    {
        if (currentInteractorTransform == null) return;

        // 1. Sprawdzamy dystans zerwania (Break Distance)
        float distToController = Vector3.Distance(currentInteractorTransform.position, transform.position);
        if (distToController > maxBreakDistance)
        {
            Debug.Log($"[RespiratorKnob] Przekroczono dystans zerwania ({distToController:F2}m > {maxBreakDistance:F2}m). Przerywam chwyt!");
            if (grabInteractable.interactionManager != null && currentInteractor != null)
            {
                grabInteractable.interactionManager.SelectCancel(currentInteractor, grabInteractable);
            }
            else
            {
                ReleaseHandModel();
                currentInteractor = null;
                currentInteractorTransform = null;
            }
            return;
        }

        // 2. Logika fizycznego obracania pokrętła wokół rotationAxis
        Vector3 currentHandLocal = transform.InverseTransformPoint(currentInteractorTransform.position);
        Vector3 currentHandDirection = Vector3.ProjectOnPlane(currentHandLocal, rotationAxis).normalized;

        if (currentHandDirection != Vector3.zero && initialHandDirection != Vector3.zero)
        {
            float angleDelta = Vector3.SignedAngle(initialHandDirection, currentHandDirection, rotationAxis);
            transform.localRotation = initialKnobRotation * Quaternion.AngleAxis(angleDelta, rotationAxis);
        }

        currentDialAngle = GetNormalizedAngleOnAxis();

        // 3. Przekazywanie wartości kąta do menedżera zdarzeń
        if (eventManager != null)
        {
            eventManager.CheckDialValue(currentDialAngle);
        }
    }

    void LateUpdate()
    {
        // 4. Zamrożenie dłoni: Model dłoni obraca się idealnie razem z pokrętłem w przestrzeni świata
        if (grabbedHandModel != null && currentInteractorTransform != null)
        {
            Vector3 worldOffset = transform.TransformDirection(handSnapOffset);
            grabbedHandModel.position = transform.position + worldOffset;
            grabbedHandModel.rotation = transform.rotation * Quaternion.Euler(0, 0, 0);
        }
    }

    private float GetNormalizedAngleOnAxis()
    {
        Vector3 euler = transform.localEulerAngles;
        float angle = 0f;

        if (Mathf.Abs(rotationAxis.z) > 0.5f) angle = euler.z;
        else if (Mathf.Abs(rotationAxis.y) > 0.5f) angle = euler.y;
        else if (Mathf.Abs(rotationAxis.x) > 0.5f) angle = euler.x;

        return Mathf.Repeat(angle, 360f);
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrab);
            grabInteractable.selectExited.RemoveListener(OnRelease);
        }
    }
}

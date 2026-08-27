using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

[RequireComponent(typeof(XRSimpleInteractable))]
public class RespiratorPushButton : MonoBehaviour
{
    [Header("Identyfikator Przycisku")]
    [Tooltip("Litera przycisku (np. Z, C, Y, N, B, A, P, R) przesyłana do menedżera zdarzeń")]
    public string buttonID = "Z";

    [Tooltip("Menedżer zdarzeń respiratora")]
    public RespiratorEventManager eventManager;

    [Header("Elementy Wizualne Przycisku")]
    [Tooltip("Ruchoma płytka przycisku (Button_plate), która zanurza się w ramce (Button_frame)")]
    public Transform buttonPlate;

    [Tooltip("Nieruchoma ramka przycisku (Button_frame)")]
    public Transform buttonFrame;

    [Header("Parametry Fizycznego Wciskania")]
    [Tooltip("Lokalny kierunek wciskania w głąb ramki (domyślnie (0, -1, 0))")]
    public Vector3 pushAxis = new Vector3(0, -1f, 0);

    [Tooltip("Maksymalna głębokość zanurzenia płytki w metrach świata (np. 0.012m = 1.2 cm)")]
    public float maxPushDepthMeters = 0.012f;

    [Tooltip("Promień strefy czułości przycisku w metrach (np. 0.045m = 4.5 cm)")]
    public float buttonRadiusMeters = 0.045f;

    [Tooltip("Próg aktywacji kliknięcia (np. 0.75 = 75% zanurzenia)")]
    [Range(0.50f, 0.95f)]
    public float activationThreshold = 0.75f;

    [Tooltip("Próg całkowitego odtopienia przed kolejnym klikiem (np. 0.15 = 15%)")]
    [Range(0.05f, 0.30f)]
    public float fullReleaseThreshold = 0.15f;

    [Tooltip("Prędkość powrotu sprężyny po puszczeniu")]
    public float returnSpeed = 25f;

    [Header("Wymagania Gestyki VR")]
    [Tooltip("Czy wymagać trzymania przycisku Trigger (domyślnie false - reaguje na sam fizyczny dotyk palca fingercollider)")]
    public bool requireTriggerPressed = false;

    [Tooltip("Maksymalny kąt natarcia palca od pionu")]
    [Range(20f, 75f)]
    public float maxApproachAngleDegrees = 50f;

    [Header("Efekty Wizualne i Dźwiękowe")]
    public Color normalColor = Color.white;
    public Color clickColor = Color.green;
    public AudioSource clickAudio;

    private Vector3 initialPlateLocalPos;
    private float currentDepthMeters = 0f;
    private float targetDepthMeters = 0f;
    private bool hasTriggeredClick = false;
    private bool isCurrentlyFullyPressed = false;

    private XRSimpleInteractable interactable;
    private MeshRenderer plateRenderer;

    private struct HandTracker
    {
        public Transform fingerTransform;
        public VRHandAnimator handAnimator;
    }
    private List<HandTracker> activeHands = new List<HandTracker>();
    private HashSet<Transform> physicalTouchers = new HashSet<Transform>();

    public bool IsFullyPressed => isCurrentlyFullyPressed;
    public float CurrentDepthMeters => currentDepthMeters;
    public Vector3 WorldButtonSurfacePosition => buttonPlate != null ? buttonPlate.position : transform.position;

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        InitComponents();
        FindHandsInScene();
    }

    void Start()
    {
        InitComponents();
        FindHandsInScene();
    }

    void OnEnable()
    {
        FindHandsInScene();
    }

    private void InitComponents()
    {
        if (buttonPlate == null)
        {
            buttonPlate = transform.Find("Button_plate") ?? 
                          transform.Find("button") ?? 
                          transform.Find("Cube");
            if (buttonPlate == null && transform.childCount > 0)
            {
                buttonPlate = transform.GetChild(0);
            }
        }

        if (buttonFrame == null)
        {
            buttonFrame = transform.Find("Button_frame") ?? 
                          transform.Find("frame");
        }

        if (buttonPlate != null)
        {
            initialPlateLocalPos = buttonPlate.localPosition;
            plateRenderer = buttonPlate.GetComponent<MeshRenderer>();
        }

        if (plateRenderer != null && plateRenderer.sharedMaterial != null)
        {
            normalColor = plateRenderer.sharedMaterial.color;
        }
    }

    public void FindHandsInScene()
    {
        activeHands.Clear();

        // 1. Znajdź wszystkie collidery / obiekty o nazwie fingercollider w scenie
        var allColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (var col in allColliders)
        {
            if (col.gameObject.name.Equals("fingercollider", System.StringComparison.OrdinalIgnoreCase))
            {
                VRHandAnimator anim = col.GetComponentInParent<VRHandAnimator>();
                if (!activeHands.Exists(h => h.fingerTransform == col.transform))
                {
                    activeHands.Add(new HandTracker
                    {
                        fingerTransform = col.transform,
                        handAnimator = anim
                    });
                }
            }
        }

        // 2. Przeszukaj wszystkich VRHandAnimator w głąb hierarchii
        var animators = Object.FindObjectsByType<VRHandAnimator>(FindObjectsSortMode.None);
        foreach (var anim in animators)
        {
            Transform foundFinger = null;

            // Szukaj obiektu "fingercollider" wśród potomków
            foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name.Equals("fingercollider", System.StringComparison.OrdinalIgnoreCase))
                {
                    foundFinger = t;
                    break;
                }
            }

            // Jeśli nie ma "fingercollider", szukaj kości palca wskazującego index3
            if (foundFinger == null)
            {
                string p = (anim.handSide == VRHandAnimator.HandSide.Left) ? "l" : "r";
                foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLower();
                    if (n.Contains($"b_{p}_index3") || n.Contains("index3") || n.Contains("index_ignore"))
                    {
                        foundFinger = t;
                        break;
                    }
                }
            }

            if (foundFinger != null)
            {
                if (!activeHands.Exists(h => h.fingerTransform == foundFinger))
                {
                    activeHands.Add(new HandTracker
                    {
                        fingerTransform = foundFinger,
                        handAnimator = anim
                    });
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsFingerOrHand(other))
        {
            physicalTouchers.Add(other.transform);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (IsFingerOrHand(other))
        {
            physicalTouchers.Add(other.transform);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            physicalTouchers.Remove(other.transform);
        }
    }

    private bool IsFingerOrHand(Collider col)
    {
        if (col == null) return false;
        if (col.transform.IsChildOf(transform)) return false;

        string n = col.gameObject.name.ToLower();
        if (n.Contains("finger") || n.Contains("index") || n.Contains("hand")) return true;
        if (col.GetComponentInParent<VRHandAnimator>() != null) return true;

        return false;
    }

    void Update()
    {
        if (buttonPlate == null) return;

        if (activeHands.Count == 0)
        {
            FindHandsInScene();
        }

        Vector3 pushWorldDir = transform.TransformDirection(pushAxis).normalized;
        Vector3 surfaceNormalWorld = -pushWorldDir;
        Vector3 restingTopPos = transform.TransformPoint(initialPlateLocalPos);

        targetDepthMeters = 0f;
        float maxPenetration = 0f;

        // Gromadzimy wszystkie unikalne punkty dotykowe
        List<Transform> candidateFingers = new List<Transform>();

        foreach (var hand in activeHands)
        {
            if (hand.fingerTransform != null && !candidateFingers.Contains(hand.fingerTransform))
            {
                if (requireTriggerPressed && hand.handAnimator != null && !hand.handAnimator.IsTriggerPressed)
                {
                    continue;
                }
                candidateFingers.Add(hand.fingerTransform);
            }
        }

        foreach (var t in physicalTouchers)
        {
            if (t != null && !candidateFingers.Contains(t))
            {
                candidateFingers.Add(t);
            }
        }

        foreach (var finger in candidateFingers)
        {
            if (finger == null) continue;

            Vector3 fingerPos = finger.position;
            Vector3 toFinger = fingerPos - restingTopPos;

            // Odległość wzdłuż normalnej przycisku (wysokość nad przyciskiem)
            float heightAboveSurface = Vector3.Dot(toFinger, surfaceNormalWorld);
            Vector3 radialVector = toFinger - (surfaceNormalWorld * heightAboveSurface);
            float radialDist = radialVector.magnitude;

            float effectiveRadius = Mathf.Max(buttonRadiusMeters, 0.045f);

            if (radialDist <= effectiveRadius)
            {
                // Penetracja: gdy palec jest poniżej powierzchni spoczynkowej
                float penetration = -heightAboveSurface;

                if (penetration >= -0.015f && penetration <= maxPushDepthMeters * 3.5f)
                {
                    float clamped = Mathf.Clamp(Mathf.Max(0f, penetration), 0f, maxPushDepthMeters);
                    if (clamped > maxPenetration)
                    {
                        maxPenetration = clamped;
                    }
                }
            }
        }

        // Jeśli fizyczny trigger wykrył kolizję bezpośrednią i palec jest bardzo blisko
        if (physicalTouchers.Count > 0 && maxPenetration < maxPushDepthMeters * activationThreshold)
        {
            foreach (var t in physicalTouchers)
            {
                if (t != null && Vector3.Distance(t.position, restingTopPos) < 0.06f)
                {
                    maxPenetration = maxPushDepthMeters;
                    break;
                }
            }
        }

        targetDepthMeters = maxPenetration;

        // 3. Płynna sprężyna powrotna
        currentDepthMeters = Mathf.MoveTowards(currentDepthMeters, targetDepthMeters, Time.deltaTime * (returnSpeed * maxPushDepthMeters * 4f));

        // 4. Aktualizacja pozycji płytki
        float parentScaleY = buttonPlate.parent != null ? buttonPlate.parent.lossyScale.y : 1f;
        float localShift = (parentScaleY > 0.0001f) ? (currentDepthMeters / parentScaleY) : currentDepthMeters;

        buttonPlate.localPosition = initialPlateLocalPos + pushAxis.normalized * localShift;

        // 5. Procent wciśnięcia
        float depthPercent = maxPushDepthMeters > 0.0001f ? (currentDepthMeters / maxPushDepthMeters) : 0f;
        isCurrentlyFullyPressed = (depthPercent >= activationThreshold);

        // 6. Obsługa kliknięcia (Histereza zapobiegająca drganiom)
        if (depthPercent >= activationThreshold && !hasTriggeredClick)
        {
            TriggerClick();
        }
        else if (depthPercent <= fullReleaseThreshold && hasTriggeredClick)
        {
            ResetClick();
        }
    }

    private void TriggerClick()
    {
        hasTriggeredClick = true;

        if (plateRenderer != null)
        {
            plateRenderer.material.color = clickColor;
        }

        if (clickAudio != null)
        {
            clickAudio.Play();
        }

        if (eventManager != null)
        {
            eventManager.OnButtonPressed(buttonID);
            Debug.Log($"[RespiratorPushButton] KLIK! Pomyślnie wciśnięto przycisk [{buttonID}] (Zanurzenie: {currentDepthMeters * 1000f:F1}mm / {activationThreshold * 100f:F0}%)");
        }
    }

    private void ResetClick()
    {
        hasTriggeredClick = false;

        if (plateRenderer != null)
        {
            plateRenderer.material.color = normalColor;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 top = buttonPlate != null ? buttonPlate.position : transform.position;
        Vector3 dir = transform.TransformDirection(pushAxis.normalized);
        Gizmos.DrawLine(top, top + dir * maxPushDepthMeters);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(top + dir * (maxPushDepthMeters * activationThreshold), buttonRadiusMeters);
    }
}

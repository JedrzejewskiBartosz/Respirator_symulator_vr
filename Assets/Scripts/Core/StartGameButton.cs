using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System.Collections.Generic;

public class StartGameButton : MonoBehaviour
{
    [Header("Menedżer Gry")]
    public HospitalManager hospitalManager;

    [Header("Wizualia Przycisku")]
    [Tooltip("Ruchoma część przycisku (kopuła / płytka)")]
    public Transform movingButtonMesh;

    [Tooltip("Lokalny kierunek wciskania")]
    public Vector3 localPushDirection = new Vector3(0, -1f, 0);

    [Tooltip("Maksymalny skok przycisku w metrach świata (np. 0.025m = 2.5 cm)")]
    public float maxPushDepthMeters = 0.025f;

    [Tooltip("Promień strefy przycisku w metrach (np. 0.10m = 10 cm)")]
    public float buttonRadiusMeters = 0.10f;

    [Tooltip("Próg aktywacji (85% głębokości)")]
    [Range(0.70f, 0.95f)]
    public float activationThreshold = 0.85f;

    [Tooltip("Próg pełnego odtopienia przed kolejnym wciśnięciem (10%)")]
    [Range(0.05f, 0.30f)]
    public float fullReleaseThreshold = 0.10f;

    [Tooltip("Prędkość powrotu sprężyny")]
    public float returnSpeed = 20f;

    [Header("Wymagania Gestyki VR")]
    [Tooltip("Wymagaj trzymania przycisku Trigger (domyślnie false)")]
    public bool requireTriggerPressed = false;

    [Tooltip("Maksymalny kąt natarcia palca od pionu")]
    [Range(20f, 75f)]
    public float maxApproachAngleDegrees = 55f;

    [Header("Wyświetlacz Tekstowy i Światło")]
    public TextMeshProUGUI statusLabel;
    public MeshRenderer statusLight;
    public Material activeLightMaterial;
    public Material standbyLightMaterial;

    [Header("Dźwięk")]
    public AudioSource clickAudio;

    [Header("Zdarzenia")]
    public UnityEvent onGameStarted;
    public UnityEvent onGameReset;

    private bool isGameRunning = false;
    private bool hasTriggeredClick = false;
    private bool isCurrentlyFullyPressed = false;
    private Vector3 initialLocalPos;
    private float currentDepthMeters = 0f;
    private float targetDepthMeters = 0f;

    private struct HandTracker
    {
        public Transform fingerTransform;
        public VRHandAnimator handAnimator;
    }
    private List<HandTracker> activeHands = new List<HandTracker>();
    private HashSet<Transform> physicalTouchers = new HashSet<Transform>();

    public bool IsFullyPressed => isCurrentlyFullyPressed;
    public float CurrentDepthMeters => currentDepthMeters;

    void Awake()
    {
        if (hospitalManager == null)
        {
            hospitalManager = Object.FindFirstObjectByType<HospitalManager>();
        }

        if (movingButtonMesh == null)
        {
            movingButtonMesh = transform.Find("Start_Button") ?? 
                               transform.Find("Button") ?? 
                               transform.Find("button") ?? 
                               transform;
        }

        if (movingButtonMesh != null)
        {
            initialLocalPos = movingButtonMesh.localPosition;
        }

        FindHandsInScene();
        UpdateVisuals(false);
    }

    void Start()
    {
        FindHandsInScene();
        UpdateVisuals(isGameRunning);
    }

    void OnEnable()
    {
        FindHandsInScene();
    }

    private void FindHandsInScene()
    {
        activeHands.Clear();

        // 1. Znajdź wszystkie obiekty fingercollider
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

        // 2. Przeszukaj drzewa potomków VRHandAnimator
        var animators = Object.FindObjectsByType<VRHandAnimator>(FindObjectsSortMode.None);
        foreach (var anim in animators)
        {
            Transform foundFinger = null;
            foreach (Transform t in anim.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name.Equals("fingercollider", System.StringComparison.OrdinalIgnoreCase))
                {
                    foundFinger = t;
                    break;
                }
            }

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
        if (IsFingerOrHand(other)) physicalTouchers.Add(other.transform);
    }

    void OnTriggerStay(Collider other)
    {
        if (IsFingerOrHand(other)) physicalTouchers.Add(other.transform);
    }

    void OnTriggerExit(Collider other)
    {
        if (other != null) physicalTouchers.Remove(other.transform);
    }

    private bool IsFingerOrHand(Collider col)
    {
        if (col == null) return false;
        if (col.transform.IsChildOf(transform)) return false;
        string n = col.gameObject.name.ToLower();
        return n.Contains("finger") || n.Contains("index") || n.Contains("hand") || col.GetComponentInParent<VRHandAnimator>() != null;
    }

    void Update()
    {
        if (movingButtonMesh == null) return;

        if (activeHands.Count == 0)
        {
            FindHandsInScene();
        }

        Vector3 pushWorldDir = transform.TransformDirection(localPushDirection).normalized;
        Vector3 worldNormal = -pushWorldDir;
        Vector3 restingTopPos = transform.TransformPoint(initialLocalPos);

        targetDepthMeters = 0f;
        float maxPenetration = 0f;

        List<Transform> candidateFingers = new List<Transform>();
        foreach (var hand in activeHands)
        {
            if (hand.fingerTransform != null && !candidateFingers.Contains(hand.fingerTransform))
            {
                if (requireTriggerPressed && hand.handAnimator != null && !hand.handAnimator.IsTriggerPressed)
                    continue;

                candidateFingers.Add(hand.fingerTransform);
            }
        }
        foreach (var t in physicalTouchers)
        {
            if (t != null && !candidateFingers.Contains(t)) candidateFingers.Add(t);
        }

        foreach (var finger in candidateFingers)
        {
            if (finger == null) continue;

            Vector3 fingerPos = finger.position;
            Vector3 toFinger = fingerPos - restingTopPos;

            float heightAboveResting = Vector3.Dot(toFinger, worldNormal);
            Vector3 radialVector = toFinger - (worldNormal * heightAboveResting);
            float radialDistance = radialVector.magnitude;

            if (radialDistance <= buttonRadiusMeters)
            {
                float penetrationDepth = -heightAboveResting;

                if (penetrationDepth >= -0.015f && penetrationDepth <= maxPushDepthMeters * 2.5f)
                {
                    float clamped = Mathf.Clamp(Mathf.Max(0f, penetrationDepth), 0f, maxPushDepthMeters);
                    if (clamped > maxPenetration)
                    {
                        maxPenetration = clamped;
                    }
                }
            }
        }

        if (physicalTouchers.Count > 0 && maxPenetration < maxPushDepthMeters * activationThreshold)
        {
            foreach (var t in physicalTouchers)
            {
                if (t != null && Vector3.Distance(t.position, restingTopPos) < 0.12f)
                {
                    maxPenetration = maxPushDepthMeters;
                    break;
                }
            }
        }

        targetDepthMeters = maxPenetration;

        // 3. Sprężyna i ruch
        currentDepthMeters = Mathf.MoveTowards(currentDepthMeters, targetDepthMeters, Time.deltaTime * (returnSpeed * maxPushDepthMeters * 3f));

        float parentScaleY = movingButtonMesh.parent != null ? movingButtonMesh.parent.lossyScale.y : 1f;
        float localShift = (parentScaleY > 0.0001f) ? (currentDepthMeters / parentScaleY) : currentDepthMeters;

        movingButtonMesh.localPosition = initialLocalPos + localPushDirection.normalized * localShift;

        // 4. Procent wciśnięcia i kliknięcie przy 85%
        float depthPercent = maxPushDepthMeters > 0.0001f ? (currentDepthMeters / maxPushDepthMeters) : 0f;
        isCurrentlyFullyPressed = (depthPercent >= activationThreshold);

        if (depthPercent >= activationThreshold && !hasTriggeredClick)
        {
            hasTriggeredClick = true;
            OnButtonPressed();
        }
        else if (depthPercent <= fullReleaseThreshold && hasTriggeredClick)
        {
            hasTriggeredClick = false;
        }
    }

    public void OnButtonPressed()
    {
        if (clickAudio != null)
        {
            clickAudio.Play();
        }

        if (!isGameRunning)
        {
            StartGame();
        }
        else
        {
            ResetGame();
        }
    }

    public void StartGame()
    {
        isGameRunning = true;

        if (hospitalManager == null)
        {
            hospitalManager = Object.FindFirstObjectByType<HospitalManager>();
        }

        if (hospitalManager != null)
        {
            hospitalManager.enabled = true;
            hospitalManager.RozpocznijSymulacje();
        }

        UpdateVisuals(true);
        onGameStarted?.Invoke();

        Debug.Log("[StartGameButton] START! Rozpoczęto rozgrywkę symulatora!");
    }

    public void ResetGame()
    {
        isGameRunning = false;

        if (hospitalManager == null)
        {
            hospitalManager = Object.FindFirstObjectByType<HospitalManager>();
        }

        if (hospitalManager != null)
        {
            hospitalManager.ZatrzymajIZresetujSymulacje();
        }

        UpdateVisuals(false);
        onGameReset?.Invoke();

        Debug.Log("[StartGameButton] Zresetowano symulację.");
    }

    private void UpdateVisuals(bool active)
    {
        if (statusLabel != null)
        {
            if (active)
            {
                statusLabel.text = "<color=#00FF66>SYMULACJA AKTYWNA</color>\n<size=16>Wciśnij, aby zresetować</size>";
            }
            else
            {
                statusLabel.text = "<color=#FFFF33>START SYMULACJI</color>\n<size=16>Wciśnij przycisk, aby rozpocząć</size>";
            }
        }

        if (statusLight != null)
        {
            if (active && activeLightMaterial != null) statusLight.material = activeLightMaterial;
            else if (!active && standbyLightMaterial != null) statusLight.material = standbyLightMaterial;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 top = movingButtonMesh != null ? movingButtonMesh.position : transform.position;
        Vector3 dir = transform.TransformDirection(localPushDirection).normalized;
        Gizmos.DrawLine(top, top + dir * maxPushDepthMeters);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(top + dir * (maxPushDepthMeters * activationThreshold), buttonRadiusMeters);
    }
}

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

    [Tooltip("Promień strefy przycisku w metrach (np. 0.08m = 8 cm)")]
    public float buttonRadiusMeters = 0.09f;

    [Tooltip("Próg aktywacji (80% głębokości)")]
    [Range(0.5f, 0.95f)]
    public float activationThreshold = 0.80f;

    [Tooltip("Prędkość powrotu sprężyny")]
    public float returnSpeed = 15f;

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
    private bool hasTriggered = false;
    private Vector3 initialLocalPos;
    private float currentDepthMeters = 0f;
    private float targetDepthMeters = 0f;

    private List<Transform> handPointers = new List<Transform>();

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

    private void FindHandsInScene()
    {
        handPointers.Clear();

        // Szukamy kontrolerów i colliderów palców w scenie
        GameObject leftHand = GameObject.Find("Left Hand");
        GameObject rightHand = GameObject.Find("Right Hand");

        if (leftHand != null)
        {
            Transform f = leftHand.transform.Find("fingercollider") ?? leftHand.transform;
            handPointers.Add(f);
        }

        if (rightHand != null)
        {
            Transform f = rightHand.transform.Find("fingercollider") ?? rightHand.transform;
            handPointers.Add(f);
        }

        // Dodajemy ewentualne obiekty VRPhysicsHand
        var physicsHands = Object.FindObjectsByType<VRPhysicsHand>(FindObjectsSortMode.None);
        foreach (var ph in physicsHands)
        {
            if (!handPointers.Contains(ph.transform))
            {
                handPointers.Add(ph.transform);
            }
        }
    }

    void Update()
    {
        if (movingButtonMesh == null) return;

        if (handPointers.Count == 0)
        {
            FindHandsInScene();
        }

        // 1. Precyzyjne geometryczne rzutowanie 3D pozycji dłoni na oś przycisku w przestrzeni świata
        Vector3 worldButtonTop = movingButtonMesh.position;
        Vector3 worldPushAxis = transform.TransformDirection(localPushDirection).normalized;

        targetDepthMeters = 0f;
        float maxPenetrationFound = 0f;

        foreach (var hand in handPointers)
        {
            if (hand == null) continue;

            Vector3 handPos = hand.position;
            Vector3 toHand = handPos - worldButtonTop;

            // Rzutowanie na oś wciskania (głębokość poniżej powierzchni przycisku)
            float depth = Vector3.Dot(toHand, worldPushAxis);

            // Odległość promieniowa od osi przycisku (czy dłoń jest nad przyciskiem)
            Vector3 radialVector = toHand - (worldPushAxis * depth);
            float radialDistance = radialVector.magnitude;

            // Jeśli dłoń znajduje się w cylindrze przycisku i naciska od góry
            if (radialDistance <= buttonRadiusMeters && depth >= -0.015f && depth <= maxPushDepthMeters * 2.5f)
            {
                float clampedDepth = Mathf.Clamp(depth, 0f, maxPushDepthMeters);
                if (clampedDepth > maxPenetrationFound)
                {
                    maxPenetrationFound = clampedDepth;
                }
            }
        }

        targetDepthMeters = maxPenetrationFound;

        // 2. Sprężyna i płynna interpolacja pozycji
        currentDepthMeters = Mathf.MoveTowards(currentDepthMeters, targetDepthMeters, Time.deltaTime * (returnSpeed * maxPushDepthMeters * 3f));

        // 3. Przesunięcie wizualne płytki przycisku
        float localDepthScale = movingButtonMesh.parent != null ? movingButtonMesh.parent.lossyScale.y : 1f;
        float localShift = (localDepthScale > 0.0001f) ? (currentDepthMeters / localDepthScale) : currentDepthMeters;

        movingButtonMesh.localPosition = initialLocalPos + localPushDirection.normalized * localShift;

        // 4. Próg aktywacji (80% głębokości)
        float depthPercent = maxPushDepthMeters > 0.0001f ? (currentDepthMeters / maxPushDepthMeters) : 0f;

        if (depthPercent >= activationThreshold && !hasTriggered)
        {
            hasTriggered = true;
            OnButtonPressed();
        }
        else if (depthPercent <= 0.30f && hasTriggered)
        {
            hasTriggered = false;
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

        if (hospitalManager != null)
        {
            hospitalManager.enabled = true;
            hospitalManager.Invoke(nameof(HospitalManager.WylosujIAktywujAwarie), 1.0f);
        }

        UpdateVisuals(true);
        onGameStarted?.Invoke();

        Debug.Log("[StartGameButton] START! Rozpoczęto rozgrywkę symulatora szpitala!");
    }

    public void ResetGame()
    {
        isGameRunning = false;

        if (hospitalManager != null)
        {
            if (hospitalManager.respiratory != null)
            {
                foreach (var resp in hospitalManager.respiratory)
                {
                    if (resp != null) resp.ResetujAlarm();
                }
            }
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

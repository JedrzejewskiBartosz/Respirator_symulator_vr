using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

[RequireComponent(typeof(XRSimpleInteractable))]
public class RespiratorPushButton : MonoBehaviour
{
    [Header("Identyfikator Przycisku")]
    [Tooltip("Litera przycisku (np. Z, C, Y, N, B, A, P, R) przesyłana do menedżera zdarzeń")]
    public string buttonID = "Z";

    [Tooltip("Menedżer zdarzeń respiratora")]
    public RespiratorEventManager eventManager;

    [Header("Ruchoma Część Przycisku (Mesh)")]
    [Tooltip("Ruchomy sześcian/płytka przycisku")]
    public Transform buttonMesh;

    [Header("Parametry Fizycznego Wciskania (w metrach świata)")]
    [Tooltip("Lokalna oś wciskania (domyślnie (0, -1, 0))")]
    public Vector3 pushAxis = new Vector3(0, -1f, 0);

    [Tooltip("Maksymalna głębokość wciśnięcia w metrach świata (np. 0.015m = 1.5 cm)")]
    public float maxPushDepthMeters = 0.015f;

    [Tooltip("Promień strefy czułości przycisku w metrach (np. 0.035m = 3.5 cm)")]
    public float buttonRadiusMeters = 0.035f;

    [Tooltip("Próg aktywacji (80% zanurzenia)")]
    [Range(0.5f, 0.95f)]
    public float activationThreshold = 0.80f;

    [Tooltip("Próg zwolnienia po wciśnięciu (30% zanurzenia)")]
    [Range(0.1f, 0.5f)]
    public float resetThreshold = 0.30f;

    [Tooltip("Prędkość sprężyny powrotnej")]
    public float returnSpeed = 20f;

    [Header("Efekty Wizualne i Dźwiękowe")]
    public Color normalColor = Color.white;
    public Color clickColor = Color.green;
    public AudioSource clickAudio;

    private Vector3 initialLocalPos;
    private float currentDepthMeters = 0f;
    private float targetDepthMeters = 0f;
    private bool hasTriggeredClick = false;

    private XRSimpleInteractable interactable;
    private MeshRenderer meshRenderer;
    private List<Transform> handPointers = new List<Transform>();

    void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();

        InitButtonMesh();
        FindHandsInScene();

        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnXRSelectEntered);
        }
    }

    void Start()
    {
        FindHandsInScene();
    }

    private void InitButtonMesh()
    {
        if (buttonMesh == null)
        {
            buttonMesh = transform.Find("Button_plate") ?? 
                         transform.Find("button") ?? 
                         transform.Find("Cube");
            if (buttonMesh == null && transform.childCount > 0)
            {
                buttonMesh = transform.GetChild(0);
            }
        }

        if (buttonMesh != null)
        {
            initialLocalPos = buttonMesh.localPosition;
            meshRenderer = buttonMesh.GetComponent<MeshRenderer>();
        }
        else
        {
            initialLocalPos = Vector3.zero;
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshRenderer != null && meshRenderer.material != null)
        {
            normalColor = meshRenderer.material.color;
        }
    }

    private void FindHandsInScene()
    {
        handPointers.Clear();

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
        if (buttonMesh == null)
        {
            InitButtonMesh();
            if (buttonMesh == null) return;
        }

        if (handPointers.Count == 0)
        {
            FindHandsInScene();
        }

        // 1. Geometryczne 3D rzutowanie wektorowe pozycji palców na oś przycisku
        Vector3 worldButtonTop = buttonMesh.position;
        Vector3 worldPushAxis = transform.TransformDirection(pushAxis).normalized;

        targetDepthMeters = 0f;
        float maxPenetrationFound = 0f;

        foreach (var hand in handPointers)
        {
            if (hand == null) continue;

            Vector3 handPos = hand.position;
            Vector3 toHand = handPos - worldButtonTop;

            // Głębokość wzdłuż osi wciskania
            float depth = Vector3.Dot(toHand, worldPushAxis);

            // Odległość od środka przycisku
            Vector3 radialVector = toHand - (worldPushAxis * depth);
            float radialDistance = radialVector.magnitude;

            if (radialDistance <= buttonRadiusMeters && depth >= -0.01f && depth <= maxPushDepthMeters * 2.5f)
            {
                float clampedDepth = Mathf.Clamp(depth, 0f, maxPushDepthMeters);
                if (clampedDepth > maxPenetrationFound)
                {
                    maxPenetrationFound = clampedDepth;
                }
            }
        }

        targetDepthMeters = maxPenetrationFound;

        // 2. Płynna sprężyna powrotna
        currentDepthMeters = Mathf.MoveTowards(currentDepthMeters, targetDepthMeters, Time.deltaTime * (returnSpeed * maxPushDepthMeters * 3f));

        // 3. Przesunięcie płytki w przestrzeni lokalnej
        float localDepthScale = buttonMesh.parent != null ? buttonMesh.parent.lossyScale.y : 1f;
        float localShift = (localDepthScale > 0.0001f) ? (currentDepthMeters / localDepthScale) : currentDepthMeters;

        buttonMesh.localPosition = initialLocalPos + pushAxis.normalized * localShift;

        // 4. Próg aktywacji (80% zanurzenia)
        float depthPercent = maxPushDepthMeters > 0.0001f ? (currentDepthMeters / maxPushDepthMeters) : 0f;

        if (depthPercent >= activationThreshold && !hasTriggeredClick)
        {
            TriggerClick();
        }
        else if (depthPercent <= resetThreshold && hasTriggeredClick)
        {
            ResetClick();
        }
    }

    private void TriggerClick()
    {
        hasTriggeredClick = true;

        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = clickColor;
        }

        if (clickAudio != null)
        {
            clickAudio.Play();
        }

        if (eventManager != null)
        {
            eventManager.OnButtonPressed(buttonID);
            Debug.Log($"[RespiratorPushButton] KLIK! Wciśnięto przycisk [{buttonID}] (Głębokość: {currentDepthMeters * 1000f:F1}mm / {activationThreshold * 100f:F0}%)");
        }
    }

    private void ResetClick()
    {
        hasTriggeredClick = false;

        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = normalColor;
        }
    }

    private void OnXRSelectEntered(SelectEnterEventArgs args)
    {
        targetDepthMeters = maxPushDepthMeters * 0.9f;
        TriggerClick();
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnXRSelectEntered);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 origin = buttonMesh != null ? buttonMesh.position : transform.position;
        Vector3 worldPushDir = transform.TransformDirection(pushAxis.normalized);
        Gizmos.DrawLine(origin, origin + worldPushDir * maxPushDepthMeters);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin + worldPushDir * (maxPushDepthMeters * activationThreshold), buttonRadiusMeters);
    }
}

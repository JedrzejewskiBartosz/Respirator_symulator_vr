using UnityEngine;

public class VRPhysicalPlayerAndCameraCollision : MonoBehaviour
{
    [Header("Referencje")]
    [Tooltip("Kamera gracza (Main Camera)")]
    public Transform playerCamera;

    [Tooltip("Główny obiekt gracza (XR Origin)")]
    public Transform xrOriginRoot;

    [Header("Parametry Kolizji Głowy i Ciała")]
    [Tooltip("Warstwy przeszkód (stół, maszyny, ściany)")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Promień strefy głowy/ciała gracza")]
    public float bodyRadius = 0.20f;

    [Tooltip("Maksymalny dystans tunelowania (grubość stołu, np. 0.55m). Po jego przekroczeniu gracz przeskakuje na drugą stronę")]
    public float maxTunnelDistance = 0.55f;

    [Header("Debug")]
    public bool showDebugOnScreen = false;
    public bool logToConsole = false;

    private Vector3 previousValidOriginPos;
    private Vector3 previousValidCamPos;
    private bool isColliding = false;
    private string lastHitObjectName = "Brak";
    private float penetrationAmount = 0f;

    void Awake()
    {
        FindReferences();
    }

    void Start()
    {
        FindReferences();
        if (xrOriginRoot != null && playerCamera != null)
        {
            previousValidOriginPos = xrOriginRoot.position;
            previousValidCamPos = playerCamera.position;
        }
    }

    private void FindReferences()
    {
        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>() ?? Camera.main;
            if (cam != null) playerCamera = cam.transform;
        }

        if (xrOriginRoot == null)
        {
            GameObject originObj = GameObject.Find("XR Origin (VR)") ?? 
                                  GameObject.Find("XR Origin") ?? 
                                  GameObject.Find("XR Rig");
            if (originObj != null) xrOriginRoot = originObj.transform;
            else xrOriginRoot = transform.root;
        }
    }

    void LateUpdate()
    {
        if (playerCamera == null || xrOriginRoot == null)
        {
            FindReferences();
            if (playerCamera == null || xrOriginRoot == null) return;
        }

        Vector3 camPos = playerCamera.position;

        // 1. Sprawdzamy czy głowa/ciało weszło w przeszkodę
        Collider[] hits = Physics.OverlapSphere(camPos, bodyRadius, obstacleLayers, QueryTriggerInteraction.Ignore);

        Collider obstacleHit = null;
        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(xrOriginRoot)) continue;
            obstacleHit = h;
            break;
        }

        if (obstacleHit != null)
        {
            isColliding = true;
            lastHitObjectName = obstacleHit.gameObject.name;

            // Wyznaczamy najbliższy punkt na przeszkodzie
            Vector3 closestPoint = obstacleHit.ClosestPoint(camPos);
            Vector3 pushVector = camPos - closestPoint;
            float currentDist = pushVector.magnitude;

            // Ile gracz wniknął w głąb strefy bezpieczeństwa
            penetrationAmount = bodyRadius - currentDist;

            // Sprawdzamy tunelowanie: jeśli gracz przeszedł bardzo daleko (> maxTunnelDistance)
            float totalShift = Vector3.Distance(camPos, previousValidCamPos);
            if (totalShift > maxTunnelDistance)
            {
                // Gracz przeszedł na drugą stronę przeszkody (pop-through)
                previousValidOriginPos = xrOriginRoot.position;
                previousValidCamPos = camPos;
                if (logToConsole) Debug.Log($"[VR Kolizja] Gracz przeskoczył przez przeszkodę: {lastHitObjectName}");
            }
            else
            {
                // Odpychamy XR Origin tak, aby kamera zatrzymała się na krawędzi stołu/respiratora
                Vector3 pushDirection;
                if (currentDist > 0.001f)
                {
                    pushDirection = pushVector.normalized;
                }
                else
                {
                    // Kamera jest wewnątrz bryły - cofamy w stronę poprzedniej poprawnej pozycji
                    pushDirection = (previousValidCamPos - camPos).normalized;
                    if (pushDirection == Vector3.zero) pushDirection = -playerCamera.forward;
                }

                // Odpychamy tylko w płaszczyźnie poziomej (XZ) i pionowej
                Vector3 correction = pushDirection * Mathf.Max(0.01f, penetrationAmount);
                xrOriginRoot.position += correction;

                if (logToConsole)
                {
                    Debug.Log($"[VR Kolizja] ZATRZYMANO GRACZA przed obiektem: [{lastHitObjectName}] | Korekta: {correction.magnitude * 100f:F1} cm | Kamera: ({camPos.x:F2}, {camPos.y:F2}, {camPos.z:F2})");
                }
            }
        }
        else
        {
            isColliding = false;
            lastHitObjectName = "Brak";
            penetrationAmount = 0f;
            previousValidOriginPos = xrOriginRoot.position;
            previousValidCamPos = camPos;
        }
    }

    void OnGUI()
    {
        if (!showDebugOnScreen) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.fontStyle = UnityEngine.FontStyle.Bold;
        style.normal.textColor = isColliding ? Color.red : Color.green;

        Vector3 cPos = playerCamera != null ? playerCamera.position : Vector3.zero;
        Vector3 oPos = xrOriginRoot != null ? xrOriginRoot.position : Vector3.zero;

        string text = $"[VR Player Physics]\n" +
                      $"Pozycja Kamery (Głowa): ({cPos.x:F2}, {cPos.y:F2}, {cPos.z:F2})\n" +
                      $"Pozycja XR Origin: ({oPos.x:F2}, {oPos.y:F2}, {oPos.z:F2})\n" +
                      $"Status Kolizji: {(isColliding ? $"<color=red>ZATRZYMANY ({lastHitObjectName}, wniknięcie: {penetrationAmount * 100f:F1}cm)</color>" : "<color=green>WOLNY RUCH</color>")}";

        GUI.Label(new Rect(20, 20, 700, 110), text, style);
    }
}

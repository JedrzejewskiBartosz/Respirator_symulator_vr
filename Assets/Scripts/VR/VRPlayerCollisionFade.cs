using UnityEngine;

public class VRPlayerCollisionFade : MonoBehaviour
{
    [Header("Wykrywanie Przeszkód")]
    [Tooltip("Warstwy przeszkód (stół, maszyny, ściany)")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Promień strefy głowy gracza")]
    public float headCollisionRadius = 0.16f;

    [Header("Debug")]
    public bool showOnScreenDebug = false;
    public bool logMovementToConsole = false;

    private Camera playerCam;
    private Transform xrOriginRoot;
    private Vector3 lastLoggedPos;
    private string lastCollidedObjectName = "Brak";
    private bool isColliding = false;

    void Awake()
    {
        playerCam = GetComponent<Camera>() ?? Camera.main;
        FindXROrigin();
        CleanupOldCanvas();
    }

    void Start()
    {
        if (playerCam != null)
        {
            lastLoggedPos = playerCam.transform.position;
        }
        CleanupOldCanvas();
    }

    void OnDisable()
    {
        CleanupOldCanvas();
    }

    void OnDestroy()
    {
        CleanupOldCanvas();
    }

    private void CleanupOldCanvas()
    {
        Transform existing = transform.Find("CollisionFadeCanvas");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    private void FindXROrigin()
    {
        if (xrOriginRoot == null)
        {
            GameObject originObj = GameObject.Find("XR Origin (VR)") ?? 
                                  GameObject.Find("XR Origin") ?? 
                                  GameObject.Find("XR Rig");
            if (originObj != null) xrOriginRoot = originObj.transform;
            else if (transform.parent != null && transform.parent.parent != null)
            {
                xrOriginRoot = transform.parent.parent;
            }
        }
    }

    void Update()
    {
        if (playerCam == null)
        {
            playerCam = GetComponent<Camera>() ?? Camera.main;
            if (playerCam == null) return;
        }

        Vector3 camPos = playerCam.transform.position;

        // Sprawdzamy kolizję głowy z przeszkodami
        Collider[] hits = Physics.OverlapSphere(camPos, headCollisionRadius, obstacleLayers, QueryTriggerInteraction.Ignore);
        
        isColliding = false;
        lastCollidedObjectName = "Brak";

        foreach (var h in hits)
        {
            if (h.transform.IsChildOf(xrOriginRoot != null ? xrOriginRoot : transform.root)) continue;

            isColliding = true;
            lastCollidedObjectName = h.gameObject.name;
            break;
        }

        if (logMovementToConsole)
        {
            float distMoved = Vector3.Distance(camPos, lastLoggedPos);
            if (distMoved > 0.3f)
            {
                lastLoggedPos = camPos;
                string colInfo = isColliding ? $" [KOLIZJA: {lastCollidedObjectName}]" : " [Wolna przestrzeń]";
                Debug.Log($"[VR Ruch Gracza] Pozycja Kamery: ({camPos.x:F2}, {camPos.y:F2}, {camPos.z:F2}){colInfo}");
            }
        }
    }

    void OnGUI()
    {
        if (!showOnScreenDebug) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.fontStyle = UnityEngine.FontStyle.Bold;
        style.normal.textColor = isColliding ? Color.red : Color.green;

        Vector3 camPos = playerCam != null ? playerCam.transform.position : Vector3.zero;
        Vector3 originPos = xrOriginRoot != null ? xrOriginRoot.position : Vector3.zero;

        string debugText = $"[VR Debug] Gracz Pozycja: ({camPos.x:F2}, {camPos.y:F2}, {camPos.z:F2})\n" +
                           $"XR Origin: ({originPos.x:F2}, {originPos.y:F2}, {originPos.z:F2})\n" +
                           $"Kolizja Głowy: {(isColliding ? $"<color=red>TAK ({lastCollidedObjectName})</color>" : "<color=green>BRAK</color>")}";

        GUI.Label(new Rect(25, 25, 600, 100), debugText, style);
    }
}

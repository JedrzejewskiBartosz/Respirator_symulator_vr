using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRPlayerCollisionFade : MonoBehaviour
{
    [Header("Wykrywanie Przeszkód")]
    [Tooltip("Warstwy przeszkód (stół, maszyny, ściany)")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Promień strefy głowy gracza")]
    public float headCollisionRadius = 0.16f;

    [Header("Płynność Ściemnienia")]
    public float fadeSpeed = 10f;

    [Header("Debug")]
    [Tooltip("Wyświetlaj informacje o pozycji gracza i kolizjach na ekranie")]
    public bool showOnScreenDebug = true;
    public bool logMovementToConsole = true;

    private Camera playerCam;
    private Transform xrOriginRoot;
    private Image fadeImage;
    private TextMeshProUGUI warningText;
    private Canvas fadeCanvas;
    private float currentAlpha = 0f;
    private Vector3 lastLoggedPos;
    private string lastCollidedObjectName = "Brak";
    private bool isColliding = false;

    void Awake()
    {
        playerCam = GetComponent<Camera>() ?? Camera.main;
        FindXROrigin();
        CreateFadeCanvas();
    }

    void Start()
    {
        if (playerCam != null)
        {
            lastLoggedPos = playerCam.transform.position;
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

    private void CreateFadeCanvas()
    {
        Transform existing = transform.Find("CollisionFadeCanvas");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        GameObject canvasObj = new GameObject("CollisionFadeCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0, 0, 0.18f);
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = Vector3.one * 0.001f;

        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.WorldSpace;
        fadeCanvas.sortingOrder = 9999;

        // Tło ściemniające
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = imageObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = new Vector2(1000, 1000);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0.1f, 0f, 0f, 0f); // Ciemny bordowo-czarny

        // Napis ostrzegawczy
        GameObject textObj = new GameObject("WarningText");
        textObj.transform.SetParent(imageObj.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0, 0.3f);
        textRT.anchorMax = new Vector2(1, 0.7f);
        textRT.sizeDelta = Vector2.zero;

        warningText = textObj.AddComponent<TextMeshProUGUI>();
        warningText.text = "KOLIZJA Z PRZESZKODĄ\n<size=18>Cofnij się do bezpiecznej strefy</size>";
        warningText.fontSize = 24;
        warningText.fontStyle = FontStyles.Bold;
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.color = new Color(1f, 0.3f, 0.3f, 0f);
    }

    void Update()
    {
        if (playerCam == null)
        {
            playerCam = GetComponent<Camera>() ?? Camera.main;
            if (playerCam == null) return;
        }

        Vector3 camPos = playerCam.transform.position;

        // 1. Sprawdzamy kolizję głowy z przeszkodami (wielopunktowy sweep)
        Collider[] hits = Physics.OverlapSphere(camPos, headCollisionRadius, obstacleLayers, QueryTriggerInteraction.Ignore);
        
        isColliding = false;
        lastCollidedObjectName = "Brak";

        foreach (var h in hits)
        {
            // Ignorujemy własne collidery dłoni i gracza
            if (h.transform.IsChildOf(xrOriginRoot != null ? xrOriginRoot : transform.root)) continue;

            isColliding = true;
            lastCollidedObjectName = h.gameObject.name;
            break;
        }

        // 2. Płynne ściemnianie obrazu
        float targetAlpha = isColliding ? 0.96f : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

        if (fadeImage != null)
        {
            fadeImage.color = new Color(0.1f, 0f, 0f, currentAlpha);
        }

        if (warningText != null)
        {
            warningText.color = new Color(1f, 0.3f, 0.3f, currentAlpha);
        }

        // 3. Logowanie pozycji do konsoli, gdy gracz się przemieszcza
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

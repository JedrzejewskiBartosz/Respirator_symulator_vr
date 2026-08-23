using UnityEngine;

public class WristMenuVisibility : MonoBehaviour
{
    [Header("Powiązania (Referencje)")]
    [Tooltip("Kamera gracza (Main Camera) z XR Origin")]
    public Transform headTransform;

    [Tooltip("Główny obiekt Canvas zegarka / menu, który ma znikać")]
    public GameObject menuCanvas;

    [Header("Ustawienia czułości")]
    [Tooltip("Maksymalny kąt odchylenia (w stopniach)")]
    [Range(20f, 90f)]
    public float activationAngle = 60f;

    [Tooltip("Zawsze pokazuj menu (przydatne do testów)")]
    public bool alwaysVisible = false;

    [Header("Debug")]
    [Tooltip("Czy wyświetlać informacje debug w oknie Game")]
    public bool showDebugGUI = false;

    private float currentAngle = 0f;
    private bool isMenuVisible = false;

    void Awake()
    {
        if (menuCanvas == null)
        {
            Canvas childCanvas = GetComponentInChildren<Canvas>(true);
            if (childCanvas != null)
            {
                menuCanvas = childCanvas.gameObject;
            }
        }
    }

    void Start()
    {
        FindHeadCamera();

        if (menuCanvas != null && !alwaysVisible)
        {
            // Na starcie menu może być widoczne lub sprawdzane w pierwszym Update
            menuCanvas.SetActive(true);
        }
    }

    void Update()
    {
        if (alwaysVisible)
        {
            if (menuCanvas != null && !menuCanvas.activeSelf) menuCanvas.SetActive(true);
            return;
        }

        if (headTransform == null)
        {
            FindHeadCamera();
        }

        if (headTransform == null || menuCanvas == null) return;

        // Wektor kierunku: od nadgarstka do oczu gracza
        Vector3 directionToHead = headTransform.position - transform.position;

        // Sprawdzamy kąt zarówno do przodu jak i do góry (gdyby tarcza była obrócona)
        float angleForward = Vector3.Angle(transform.forward, directionToHead);
        float angleUp = Vector3.Angle(transform.up, directionToHead);
        currentAngle = Mathf.Min(angleForward, angleUp);

        isMenuVisible = (currentAngle < activationAngle);

        if (menuCanvas.activeSelf != isMenuVisible)
        {
            menuCanvas.SetActive(isMenuVisible);
        }
    }

    private void FindHeadCamera()
    {
        if (Camera.main != null)
        {
            headTransform = Camera.main.transform;
            return;
        }

        Camera anyCam = FindAnyObjectByType<Camera>();
        if (anyCam != null)
        {
            headTransform = anyCam.transform;
        }
    }

    void OnGUI()
    {
        if (!showDebugGUI) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = (isMenuVisible || alwaysVisible) ? Color.green : Color.red;

        GUI.Label(new Rect(20, 100, 600, 40),
            $"Kąt nadgarstka: {currentAngle:F1}° (Limit: {activationAngle:F0}° | Widoczne: {menuCanvas?.activeSelf})",
            style);
    }
}

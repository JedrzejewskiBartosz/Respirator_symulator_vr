using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class ElastycznyWaz : MonoBehaviour
{
    [Header("Punkty Połączenia")]
    [Tooltip("Punkt wyjścia rury z ziemi / podłogi (Ground / Wall Anchor). Jeśli puste, używa tego obiektu.")]
    public Transform punktPoczatkowy;

    [Tooltip("Końcówka / wtyczka rury (np. Pipe - obiekt chwytany przez gracza)")]
    public Transform wtyczka;

    [Header("Ustawienia Wizualne Węża")]
    [Tooltip("Grubość rury")]
    [Range(0.01f, 0.1f)]
    public float gruboscRury = 0.035f;

    [Tooltip("Liczba segmentów linii (gładkość krzywizny)")]
    [Range(10, 60)]
    public int segmentCount = 30;

    [Tooltip("Współczynnik naturalnego opadania w dół (grawitacja węża)")]
    [Range(0f, 1f)]
    public float sagAmount = 0.35f;

    [Tooltip("Sztywność rury (jak bardzo wychodzi prostopadle z punktów zaczepienia)")]
    [Range(0f, 1f)]
    public float stiffness = 0.4f;

    private LineRenderer lr;

    void Awake()
    {
        SetupLineRenderer();
    }

    void OnEnable()
    {
        SetupLineRenderer();
    }

    private void SetupLineRenderer()
    {
        if (lr == null) lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.useWorldSpace = true;
            lr.positionCount = segmentCount;
            lr.startWidth = gruboscRury;
            lr.endWidth = gruboscRury;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 6;
        }
    }

    void Update()
    {
        if (wtyczka == null)
        {
            // Próba automatycznego odnalezienia wtyczki o nazwie "Pipe" w sąsiedztwie
            if (transform.parent != null)
            {
                Transform foundPipe = transform.parent.Find("Pipe");
                if (foundPipe != null) wtyczka = foundPipe;
            }
            if (wtyczka == null) return;
        }

        if (lr == null) SetupLineRenderer();
        if (lr == null) return;

        if (lr.positionCount != segmentCount)
        {
            lr.positionCount = segmentCount;
        }
        lr.startWidth = gruboscRury;
        lr.endWidth = gruboscRury;

        Vector3 pStart = (punktPoczatkowy != null) ? punktPoczatkowy.position : transform.position;
        Vector3 pEnd = wtyczka.position;

        // Kierunki wyprowadzenia rury ze złączy
        Vector3 startDir = (punktPoczatkowy != null) ? punktPoczatkowy.up : Vector3.up;
        Vector3 endDir = -wtyczka.up; // kierunek tyłu wtyczki

        float dist = Vector3.Distance(pStart, pEnd);
        float handleLength = Mathf.Clamp(dist * stiffness, 0.1f, 1.0f);

        // Punkty kontrolne krzywej Béziera 3. stopnia (Cubic Bézier)
        Vector3 p0 = pStart;
        Vector3 p1 = pStart + startDir * handleLength + Vector3.down * (sagAmount * dist * 0.4f);
        Vector3 p2 = pEnd + endDir * handleLength + Vector3.down * (sagAmount * dist * 0.4f);
        Vector3 p3 = pEnd;

        // Generowanie punktów
        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            Vector3 point = CalculateCubicBezierPoint(t, p0, p1, p2, p3);
            lr.SetPosition(i, point);
        }
    }

    private Vector3 CalculateCubicBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        // B(t) = (1-t)^3 * P0 + 3(1-t)^2 * t * P1 + 3(1-t) * t^2 * P2 + t^3 * P3
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3f * uu * t * p1;
        p += 3f * u * tt * p2;
        p += ttt * p3;

        return p;
    }
}

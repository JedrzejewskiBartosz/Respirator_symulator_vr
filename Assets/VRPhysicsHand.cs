using UnityEngine;

public class VRPhysicsHand : MonoBehaviour
{
    [Header("Śledzenie Kontrolera")]
    [Tooltip("Transform fizycznego kontrolera / rodzica, który śledzi ruch w przestrzeni")]
    public Transform targetController;

    [Header("Ustawienia Kolizji")]
    [Tooltip("Warstwy, z którymi dłoń ma kolidować (domyślnie wszystko poza strefami triggerów)")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("Promień sfery kolizyjnej dłoni (np. 0.018 = 1.8 cm)")]
    public float handRadius = 0.018f;

    [Tooltip("Offset środka dłoni względem punktu chwytu")]
    public Vector3 handCenterOffset = new Vector3(0f, -0.01f, 0.03f);

    [Header("Tunelowanie (Anti-Tunneling)")]
    [Tooltip("Maksymalne oddalenie fizycznego kontrolera od zablokowanej dłoni (np. 0.35m) po którym dłoń przeskakuje przez przeszkodę")]
    public float maxTeleportDistance = 0.35f;

    [Tooltip("Prędkość płynnego podążania dłoni")]
    public float followSpeed = 45f;

    [Tooltip("Prędkość rotacji dłoni")]
    public float rotateSpeed = 40f;

    [Header("Debug")]
    public bool showGizmo = true;

    private Vector3 currentHandPos;
    private Quaternion currentHandRot;
    private bool isInitialized = false;

    void Start()
    {
        if (targetController == null && transform.parent != null)
        {
            targetController = transform.parent;
        }

        if (targetController != null)
        {
            currentHandPos = targetController.position;
            currentHandRot = targetController.rotation;
            isInitialized = true;
        }
    }

    void LateUpdate()
    {
        if (targetController == null)
        {
            if (transform.parent != null) targetController = transform.parent;
            else return;
        }

        Vector3 targetPos = targetController.position;
        Quaternion targetRot = targetController.rotation;

        if (!isInitialized)
        {
            currentHandPos = targetPos;
            currentHandRot = targetRot;
            isInitialized = true;
        }

        // 1. Sprawdzamy odległość fizycznego kontrolera od zablokowanego modelu dłoni
        float distToController = Vector3.Distance(currentHandPos, targetPos);

        // 2. Warunek tunelowania: Jeśli kontroler przeszedł na drugą stronę przeszkody (> maxTeleportDistance)
        if (distToController > maxTeleportDistance)
        {
            currentHandPos = targetPos;
            currentHandRot = targetRot;
        }
        else
        {
            // 3. Sprawdzamy kolizję za pomocą SphereCast
            Vector3 startCenter = currentHandPos + (currentHandRot * handCenterOffset);
            Vector3 targetCenter = targetPos + (targetRot * handCenterOffset);
            Vector3 moveVector = targetCenter - startCenter;
            float moveDist = moveVector.magnitude;

            if (moveDist > 0.001f)
            {
                Vector3 moveDir = moveVector / moveDist;

                RaycastHit[] hits = Physics.SphereCastAll(startCenter, handRadius, moveDir, moveDist, collisionLayers, QueryTriggerInteraction.Ignore);

                RaycastHit closestValidHit = default;
                bool foundObstacle = false;
                float minHitDist = float.MaxValue;

                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(targetController)) continue;

                    // Ignorujemy przyciski i pokrętła, aby dłoń mogła swobodnie dotknąć i wcisnąć klawisz!
                    if (h.collider.GetComponentInParent<StartGameButton>() != null ||
                        h.collider.GetComponentInParent<RespiratorPushButton>() != null ||
                        h.collider.GetComponentInParent<RespiratorDirectKnob>() != null ||
                        h.collider.name.ToLower().Contains("button") ||
                        h.collider.isTrigger)
                    {
                        continue;
                    }

                    if (h.distance < minHitDist)
                    {
                        minHitDist = h.distance;
                        closestValidHit = h;
                        foundObstacle = true;
                    }
                }

                if (foundObstacle)
                {
                    // Dłoń zatrzymuje się na powierzchni przeszkody (stół, ściana, obudowa)
                    float allowedDist = Mathf.Max(0f, closestValidHit.distance - 0.002f);
                    currentHandPos += moveDir * allowedDist;
                }
                else
                {
                    // Brak przeszkód - dłoń płynnie podąża za kontrolerem
                    float step = Mathf.Max(followSpeed * Time.deltaTime, moveDist * 0.85f);
                    currentHandPos = Vector3.MoveTowards(currentHandPos, targetPos, step);
                }
            }
            else
            {
                currentHandPos = targetPos;
            }

            // Płynna rotacja dłoni
            currentHandRot = Quaternion.Slerp(currentHandRot, targetRot, Time.deltaTime * rotateSpeed);
        }

        // Przypisanie pozycji w przestrzeni świata
        transform.position = currentHandPos;
        transform.rotation = currentHandRot;
    }

    public void TeleportToTarget()
    {
        if (targetController != null)
        {
            currentHandPos = targetController.position;
            currentHandRot = targetController.rotation;
            transform.position = currentHandPos;
            transform.rotation = currentHandRot;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = Color.cyan;
        Vector3 c = transform.position + (transform.rotation * handCenterOffset);
        Gizmos.DrawWireSphere(c, handRadius);
    }
}

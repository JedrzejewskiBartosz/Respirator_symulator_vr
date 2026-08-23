using UnityEngine;

public class MenuTeleporter : MonoBehaviour
{
    [Header("Twój Gracz (XR Origin)")]
    [Tooltip("Przeci¹gnij tutaj ca³y obiekt XR Origin ze sceny")]
    public Transform playerRig;

    [Header("Punkty Docelowe (Kotwice)")]
    public Transform location1;
    public Transform location2;
    public Transform location3;

    // Funkcje wywo³ywane przez przyciski z Wrist Menu

    public void GoToLocation1()
    {
        if (playerRig != null && location1 != null)
        {
            playerRig.position = location1.position;
            // Opcjonalnie: ustawiamy te¿ rotacjê, ¿eby gracz patrzy³ w dobr¹ stronê
            playerRig.rotation = location1.rotation;
        }
    }

    public void GoToLocation2()
    {
        if (playerRig != null && location2 != null)
        {
            playerRig.position = location2.position;
            playerRig.rotation = location2.rotation;
        }
    }

    public void GoToLocation3()
    {
        if (playerRig != null && location3 != null)
        {
            playerRig.position = location3.position;
            playerRig.rotation = location3.rotation;
        }
    }
}
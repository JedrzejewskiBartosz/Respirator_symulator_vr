using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class WristMenuController : MonoBehaviour
{
    [Header("Gracz (XR Origin)")]
    [Tooltip("Główny obiekt gracza XR Origin")]
    public Transform playerRig;

    [Header("Stanowiska Respiratorów")]
    public Transform station1;
    public Transform station2;
    public Transform station3;

    [Header("Zarządca Szpitala")]
    public HospitalManager hospitalManager;

    [Header("Elementy UI Zegarka")]
    public TextMeshProUGUI statusText1;
    public TextMeshProUGUI statusText2;
    public TextMeshProUGUI statusText3;
    public TextMeshProUGUI globalStatusText;

    private List<RespiratorEventManager> respirators = new List<RespiratorEventManager>();

    void Awake()
    {
        AutoFindReferences();
    }

    void Start()
    {
        AutoFindReferences();
    }

    void Update()
    {
        UpdateLiveStatus();
    }

    public void AutoFindReferences()
    {
        // 1. Szukamy XR Origin
        if (playerRig == null)
        {
            GameObject originObj = GameObject.Find("XR Origin (VR)") ?? 
                                  GameObject.Find("XR Origin") ?? 
                                  GameObject.Find("XR Rig");
            if (originObj != null) playerRig = originObj.transform;
            else
            {
                Camera cam = Camera.main;
                if (cam != null && cam.transform.parent != null && cam.transform.parent.parent != null)
                {
                    playerRig = cam.transform.parent.parent;
                }
            }
        }

        // 2. Szukamy stacji respiratorów w scenie
        if (station1 == null) station1 = GameObject.Find("Loc_Respirator")?.transform;
        if (station2 == null) station2 = GameObject.Find("Loc_Respirator (1)")?.transform;
        if (station3 == null) station3 = GameObject.Find("Loc_Respirator (2)")?.transform;

        // 3. Szukamy HospitalManager
        if (hospitalManager == null)
        {
            hospitalManager = Object.FindFirstObjectByType<HospitalManager>();
        }

        // 4. Szukamy respiratorów i sortujemy według osi Z (od Stacji 1 do 3)
        if (respirators.Count == 0)
        {
            var found = Object.FindObjectsByType<RespiratorEventManager>(FindObjectsSortMode.None);
            var sorted = new List<RespiratorEventManager>(found);
            sorted.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            respirators.AddRange(sorted);
        }
    }

    // --- FUNKCJE TELEPORTACJI DO STACJI ---

    public void TeleportToStation1() => TeleportToStation(station1, 0);
    public void TeleportToStation2() => TeleportToStation(station2, 1);
    public void TeleportToStation3() => TeleportToStation(station3, 2);

    public void TeleportToStation(Transform stationTarget, int stationIndex)
    {
        if (playerRig == null)
        {
            AutoFindReferences();
            if (playerRig == null)
            {
                Debug.LogError("[WristMenu] Nie znaleziono XR Origin do teleportacji!");
                return;
            }
        }

        if (stationTarget == null)
        {
            Debug.LogError($"[WristMenu] Stacja docelowa {stationIndex + 1} nie jest przypisana!");
            return;
        }

        // 1. Docelowa pozycja punktu stacji na podłodze
        Vector3 targetSpotPos = stationTarget.position;

        // 2. Docelowy kierunek spojrzenia: twarzą w stronę respiratora (+X lub wektor w stronę respiratora)
        Vector3 targetLookDir = Vector3.right;
        if (respirators != null && stationIndex >= 0 && stationIndex < respirators.Count && respirators[stationIndex] != null)
        {
            Vector3 toResp = respirators[stationIndex].transform.position - targetSpotPos;
            toResp.y = 0f;
            if (toResp.sqrMagnitude > 0.01f)
            {
                targetLookDir = toResp.normalized;
            }
        }

        // 3. Prawidłowa obsługa VR HMD (kompensacja Room-Scale Tracking Offset)
        Camera vrCam = Camera.main;

        if (vrCam != null)
        {
            // A. Obrót: obracamy XR Origin wokół aktualnej pozycji głowy, aby spojrzenie trafiło wprost na respirator
            float currentYaw = vrCam.transform.eulerAngles.y;
            float desiredYaw = Quaternion.LookRotation(targetLookDir, Vector3.up).eulerAngles.y;
            float deltaYaw = desiredYaw - currentYaw;

            playerRig.RotateAround(vrCam.transform.position, Vector3.up, deltaYaw);

            // B. Pozycja: obliczamy przesunięcie głowy gracza w fizycznym pokoju względem środka XR Origin
            Vector3 headOffsetInRoom = vrCam.transform.position - playerRig.position;
            headOffsetInRoom.y = 0f;

            // Umieszczamy gracza tak, aby jego głowa znalazła się dokładnie nad punktem Loc_Respirator
            Vector3 finalRigPos = targetSpotPos - headOffsetInRoom;
            finalRigPos.y = targetSpotPos.y;

            playerRig.position = finalRigPos;
        }
        else
        {
            Vector3 finalRigPos = targetSpotPos;
            finalRigPos.y = targetSpotPos.y;
            playerRig.position = finalRigPos;
            playerRig.rotation = Quaternion.LookRotation(targetLookDir, Vector3.up);
        }

        Debug.Log($"[WristMenu] Pomyślnie zateleportowano gracza przed Stację {stationIndex + 1} ({stationTarget.name})!");
    }

    // --- FUNKCJE ALARMÓW (TEST / ROZGRYWKA) ---

    public void TriggerRandomAlarm()
    {
        if (hospitalManager != null)
        {
            hospitalManager.WylosujIAktywujAwarie();
        }
        else if (respirators.Count > 0)
        {
            int r = Random.Range(0, respirators.Count);
            respirators[r].WywolajLosowyAlarm();
        }
    }

    // --- PODGLĄD STATUSU NA ŻYWO ---

    private void UpdateLiveStatus()
    {
        if (hospitalManager != null && hospitalManager.respiratory != null && hospitalManager.respiratory.Count > 0)
        {
            UpdateSingleStatus(hospitalManager.respiratory, 0, statusText1, "Stacja 1");
            UpdateSingleStatus(hospitalManager.respiratory, 1, statusText2, "Stacja 2");
            UpdateSingleStatus(hospitalManager.respiratory, 2, statusText3, "Stacja 3");
        }
        else if (respirators.Count > 0)
        {
            UpdateSingleStatus(respirators, 0, statusText1, "Stacja 1");
            UpdateSingleStatus(respirators, 1, statusText2, "Stacja 2");
            UpdateSingleStatus(respirators, 2, statusText3, "Stacja 3");
        }
    }

    private void UpdateSingleStatus(List<RespiratorEventManager> list, int index, TextMeshProUGUI textElement, string stationLabel)
    {
        if (textElement == null) return;

        if (index < list.Count && list[index] != null)
        {
            var resp = list[index];
            if (resp.currentEvent == RespiratorEventManager.EventType.Brak)
            {
                textElement.text = $"{stationLabel}: <color=#00FF66>OK</color>";
            }
            else
            {
                string eventName = resp.currentEvent.ToString();
                textElement.text = $"{stationLabel}: <color=#FF2222>ALARM ({eventName})</color>";
            }
        }
        else
        {
            textElement.text = $"{stationLabel}: <color=grey>---</color>";
        }
    }
}

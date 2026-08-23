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

        // 4. Szukamy respiratorów
        if (respirators.Count == 0)
        {
            var found = Object.FindObjectsByType<RespiratorEventManager>(FindObjectsSortMode.None);
            respirators.AddRange(found);
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

        // Obliczamy bezpieczną pozycję stojącą przed respiratorem
        Vector3 spawnPos = stationTarget.position;
        Quaternion spawnRot = stationTarget.rotation;

        // Odsuwamy gracza lekko w kierunku przodu respiratora (lub wektora forward)
        Vector3 forwardOffset = stationTarget.forward * 0.85f;
        Vector3 targetPlayerPos = spawnPos + forwardOffset;
        targetPlayerPos.y = playerRig.position.y; // Zachowujemy wysokość podłogi

        // Obracamy gracza twarzą w stronę respiratora
        Vector3 lookDir = (stationTarget.position - targetPlayerPos).normalized;
        lookDir.y = 0;
        Quaternion targetRot = lookDir != Vector3.zero ? Quaternion.LookRotation(lookDir) : spawnRot;

        playerRig.position = targetPlayerPos;
        playerRig.rotation = targetRot;

        Debug.Log($"[WristMenu] Zateleportowano gracza do Stacji {stationIndex + 1} ({stationTarget.name})");
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

using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class SetupWristMenu : EditorWindow
{
    [MenuItem("Magisterka/3. Odtwórz i wstaw Wrist Menu (Zegarek z teleportacją i statusami)")]
    public static void CreateWristMenuOnLeftHand()
    {
        // 1. Szukamy Left Hand w aktywnej scenie
        Transform leftHand = FindLeftHand();
        if (leftHand == null)
        {
            EditorUtility.DisplayDialog("Błąd", "Nie znaleziono obiektu Left Hand w scenie. Uruchom najpierw opcję 'Magisterka -> 1. Wstaw pełne dłonie...'", "OK");
            return;
        }

        // 2. Szukamy XR Origin i stacji
        Transform xrOrigin = GameObject.Find("XR Origin (VR)")?.transform ?? 
                             GameObject.Find("XR Origin")?.transform ?? 
                             GameObject.Find("XR Rig")?.transform;
        Transform st1 = GameObject.Find("Loc_Respirator")?.transform;
        Transform st2 = GameObject.Find("Loc_Respirator (1)")?.transform;
        Transform st3 = GameObject.Find("Loc_Respirator (2)")?.transform;
        HospitalManager hospital = Object.FindFirstObjectByType<HospitalManager>();

        // 3. Usuwamy stary WristMenuAnchor / Wrist_Menu jeśli istnieje
        Transform oldWrist = leftHand.Find("Wrist_Menu") ?? leftHand.Find("WirstMenuAnchor");
        if (oldWrist != null)
        {
            Undo.DestroyObjectImmediate(oldWrist.gameObject);
        }

        // 4. Tworzymy główny obiekt Wrist_Menu
        GameObject wristRoot = new GameObject("Wrist_Menu");
        Undo.RegisterCreatedObjectUndo(wristRoot, "Create Wrist Menu");
        wristRoot.transform.SetParent(leftHand, false);
        wristRoot.transform.localPosition = new Vector3(0.015f, 0.04f, 0.035f);
        wristRoot.transform.localRotation = Quaternion.Euler(-30f, 90f, 0f);
        wristRoot.transform.localScale = Vector3.one;

        // 5. Dodajemy komponenty sterujące
        WristMenuVisibility visibility = wristRoot.AddComponent<WristMenuVisibility>();
        visibility.activationAngle = 65f;
        visibility.alwaysVisible = false;

        WristMenuController controller = wristRoot.AddComponent<WristMenuController>();
        controller.playerRig = xrOrigin;
        controller.station1 = st1;
        controller.station2 = st2;
        controller.station3 = st3;
        controller.hospitalManager = hospital;

        // 6. Tworzymy Canvas (World Space)
        GameObject canvasObj = new GameObject("Wrist_Canvas");
        canvasObj.transform.SetParent(wristRoot.transform, false);
        canvasObj.transform.localPosition = Vector3.zero;
        canvasObj.transform.localRotation = Quaternion.identity;
        canvasObj.transform.localScale = new Vector3(0.0006f, 0.0006f, 0.0006f);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;

        canvasObj.AddComponent<GraphicRaycaster>();
        canvasObj.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRT = canvasObj.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(300, 380);

        visibility.menuCanvas = canvasObj;

        // 7. Tworzymy Tło (Background Panel)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        RectTransform bgRT = bgObj.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.12f, 0.18f, 0.92f);

        // 8. Tytuł (Header)
        GameObject headerObj = new GameObject("Header_Text");
        headerObj.transform.SetParent(bgObj.transform, false);
        RectTransform headerRT = headerObj.AddComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0, 1);
        headerRT.anchorMax = new Vector2(1, 1);
        headerRT.pivot = new Vector2(0.5f, 1);
        headerRT.anchoredPosition = new Vector2(0, -12);
        headerRT.sizeDelta = new Vector2(280, 40);

        TextMeshProUGUI headerTMP = headerObj.AddComponent<TextMeshProUGUI>();
        headerTMP.text = "STANOWISKA VR";
        headerTMP.fontSize = 24;
        headerTMP.fontStyle = FontStyles.Bold;
        headerTMP.alignment = TextAlignmentOptions.Center;
        headerTMP.color = new Color(0.3f, 0.85f, 1f, 1f);

        // 9. Przyciski Stacji (1, 2, 3)
        var btn1 = CreateStationButton(bgObj.transform, "Btn_Station1", "1. Respirator Lewy", -60, new Color(0.15f, 0.35f, 0.55f, 1f));
        var btn2 = CreateStationButton(bgObj.transform, "Btn_Station2", "2. Respirator Środkowy", -135, new Color(0.15f, 0.35f, 0.55f, 1f));
        var btn3 = CreateStationButton(bgObj.transform, "Btn_Station3", "3. Respirator Prawy", -210, new Color(0.15f, 0.35f, 0.55f, 1f));

        // Podpinamy OnClick dla przycisków stacji
        UnityEventTools.AddPersistentListener(btn1.button.onClick, new UnityAction(controller.TeleportToStation1));
        UnityEventTools.AddPersistentListener(btn2.button.onClick, new UnityAction(controller.TeleportToStation2));
        UnityEventTools.AddPersistentListener(btn3.button.onClick, new UnityAction(controller.TeleportToStation3));

        controller.statusText1 = btn1.statusText;
        controller.statusText2 = btn2.statusText;
        controller.statusText3 = btn3.statusText;

        // 10. Przycisk wywołania losowej awarii
        GameObject alarmBtnObj = new GameObject("Btn_TriggerAlarm");
        alarmBtnObj.transform.SetParent(bgObj.transform, false);
        RectTransform alarmRT = alarmBtnObj.AddComponent<RectTransform>();
        alarmRT.anchorMin = new Vector2(0, 1);
        alarmRT.anchorMax = new Vector2(1, 1);
        alarmRT.pivot = new Vector2(0.5f, 1);
        alarmRT.anchoredPosition = new Vector2(0, -290);
        alarmRT.sizeDelta = new Vector2(270, 50);

        Image alarmImg = alarmBtnObj.AddComponent<Image>();
        alarmImg.color = new Color(0.65f, 0.15f, 0.15f, 1f);

        Button alarmBtn = alarmBtnObj.AddComponent<Button>();
        alarmBtn.targetGraphic = alarmImg;
        UnityEventTools.AddPersistentListener(alarmBtn.onClick, new UnityAction(controller.TriggerRandomAlarm));

        GameObject alarmTextObj = new GameObject("Text");
        alarmTextObj.transform.SetParent(alarmBtnObj.transform, false);
        RectTransform atRT = alarmTextObj.AddComponent<RectTransform>();
        atRT.anchorMin = Vector2.zero;
        atRT.anchorMax = Vector2.one;
        atRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI alarmTMP = alarmTextObj.AddComponent<TextMeshProUGUI>();
        alarmTMP.text = "⚡ WYWOŁAJ AWARIĘ";
        alarmTMP.fontSize = 20;
        alarmTMP.fontStyle = FontStyles.Bold;
        alarmTMP.alignment = TextAlignmentOptions.Center;
        alarmTMP.color = Color.white;

        // 11. Podpinamy teksty do HospitalManager
        if (hospital != null)
        {
            Undo.RecordObject(hospital, "Update HospitalManager Status Texts");
            hospital.tekstyStatusu = new TextMeshProUGUI[] { btn1.statusText, btn2.statusText, btn3.statusText };
            EditorUtility.SetDirty(hospital);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Sukces!", 
            "Pomyślnie odtworzono Wrist Menu na lewej ręce:\n\n" +
            "✔ Widoczność gestem: Pojawia się, gdy obracasz nadgarstek w stronę oczu (kąt < 65°)\n" +
            "✔ Teleportacja: Przyciski teleportacji do Stacji 1, 2 i 3\n" +
            "✔ Status na żywo: Wyświetla 'OK' (zielony) lub 'ALARM' (czerwony) dla każdej maszyny\n" +
            "✔ Przycisk losowania awarii (⚡)\n\n" +
            "Zapisz scenę (Ctrl+S) i wciśnij Play!", "Super!");

        Debug.Log("[Magisterka] Pomyślnie utworzono Wrist Menu na lewym nadgarstku.");
    }

    private struct StationButtonResult
    {
        public Button button;
        public TextMeshProUGUI statusText;
    }

    private static StationButtonResult CreateStationButton(Transform parent, string name, string buttonLabel, float yPos, Color btnColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRT = btnObj.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0, 1);
        btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot = new Vector2(0.5f, 1);
        btnRT.anchoredPosition = new Vector2(0, yPos);
        btnRT.sizeDelta = new Vector2(270, 60);

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        // Tytuł przycisku
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);
        RectTransform lRT = labelObj.AddComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0, 0.45f);
        lRT.anchorMax = new Vector2(1, 1f);
        lRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = buttonLabel;
        labelTMP.fontSize = 18;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.color = Color.white;

        // Podpis statusu pod przyciskiem
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(btnObj.transform, false);
        RectTransform sRT = statusObj.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0, 0f);
        sRT.anchorMax = new Vector2(1, 0.45f);
        sRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI statusTMP = statusObj.AddComponent<TextMeshProUGUI>();
        statusTMP.text = "Stan: OK";
        statusTMP.fontSize = 15;
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.color = new Color(0.2f, 1f, 0.4f, 1f);

        return new StationButtonResult { button = btn, statusText = statusTMP };
    }

    private static Transform FindLeftHand()
    {
        List<Transform> allTransforms = new List<Transform>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));
        }

        string[] candidates = { "Left Hand", "LeftHand", "Left Controller", "LeftController" };
        foreach (string name in candidates)
        {
            Transform found = allTransforms.Find(t => t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
        }

        return null;
    }
}

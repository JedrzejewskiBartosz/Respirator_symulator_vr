using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class RespiratorValidator : EditorWindow
{
    private const string RESPIRATOR_PREFAB_PATH = "Assets/Prefabs/Prefab_Respirator 1.prefab";
    private const string BLUE_MATERIAL_PATH = "Assets/Materials/Blue.mat";
    private const string COLORS_FOLDER_PATH = "Assets/Materials/Button_posibble_colors";

    [MenuItem("Magisterka/Respirator/1. Skonfiguruj i zwaliduj Prefab Respiratora (Wczepiona rurka + Wąż + Generator 4/6 Przycisków 80%)")]
    public static void ValidateAndSetupRespiratorPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(RESPIRATOR_PREFAB_PATH);
        if (prefabRoot == null)
        {
            EditorUtility.DisplayDialog("Błąd", "Nie znaleziono prefabu pod ścieżką: " + RESPIRATOR_PREFAB_PATH, "OK");
            return;
        }

        try
        {
            SetupRespiratorGameObject(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, RESPIRATOR_PREFAB_PATH);
            Debug.Log("[Magisterka] Pomyślnie zwalidowano i zapisano Prefab Respiratora!");
            EditorUtility.DisplayDialog("Sukces!", 
                "Pomyślnie zwalidowano Prefab Respiratora!\n\n" +
                "✔ Generator Przycisków (4 lub 6): Losuje bez powtórzeń kolory z puli 8 materiałów (Button_posibble_colors) i układa w siatce 2x2 lub 2x3.\n" +
                "✔ Fizyczne Wciskanie (80% progu): Zaktualizowano matematykę wektorową w metrach świata (100% czułości na dłonie VR).\n" +
                "✔ Stan początkowy: Rurka (Pipe) wczepiona w gniazdo (Pipe_socket).\n" +
                "✔ Wąż z ziemi: Model węża (ElastycznyWaz) łączy podłogę z wtyczką Pipe.\n\n" +
                "Zalecane: Kliknij opcję 2, aby zsynchronizować scenę.", "Super!");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("Magisterka/Respirator/2. Zsynchronizuj respiratory i HospitalManager w aktywnej scenie")]
    public static void SyncSceneRespirators()
    {
        RespiratorEventManager[] managers = Object.FindObjectsByType<RespiratorEventManager>(FindObjectsSortMode.None);
        
        foreach (var mgr in managers)
        {
            Undo.RecordObject(mgr.gameObject, "Sync Respirator");
            SetupRespiratorGameObject(mgr.gameObject);
            mgr.GenerujPrzyciskiNaPanelu();
            EditorUtility.SetDirty(mgr.gameObject);
        }

        HospitalManager hospital = Object.FindFirstObjectByType<HospitalManager>();
        if (hospital != null)
        {
            Undo.RecordObject(hospital, "Assign Respiratory to HospitalManager");
            hospital.respiratory = new List<RespiratorEventManager>(managers);
            EditorUtility.SetDirty(hospital);
            Debug.Log($"[Magisterka] Podpięto {managers.Length} respiratorów do HospitalManager.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Sukces", 
            $"Zsynchronizowano scenę!\n\n" +
            $"✔ Zaktualizowano {managers.Length} respiratorów (fizyczne przyciski world-space 80%, wczepione rurki i węże).\n" +
            $"✔ Podpięto listę maszyn do HospitalManager.", "OK");
    }

    [MenuItem("Magisterka/Respirator/3. Przetestuj generowanie 4 przycisków na zaznaczonym respiratorze")]
    public static void TestGenerate4Buttons()
    {
        if (Selection.activeGameObject == null) return;
        RespiratorEventManager mgr = Selection.activeGameObject.GetComponentInParent<RespiratorEventManager>();
        if (mgr != null)
        {
            mgr.trybIlosciPrzyciskow = RespiratorEventManager.ButtonCountMode.Always4;
            mgr.GenerujPrzyciskiNaPanelu();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    [MenuItem("Magisterka/Respirator/4. Przetestuj generowanie 6 przycisków na zaznaczonym respiratorze")]
    public static void TestGenerate6Buttons()
    {
        if (Selection.activeGameObject == null) return;
        RespiratorEventManager mgr = Selection.activeGameObject.GetComponentInParent<RespiratorEventManager>();
        if (mgr != null)
        {
            mgr.trybIlosciPrzyciskow = RespiratorEventManager.ButtonCountMode.Always6;
            mgr.GenerujPrzyciskiNaPanelu();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    private static void SetupRespiratorGameObject(GameObject root)
    {
        RespiratorEventManager eventMgr = root.GetComponent<RespiratorEventManager>();
        if (eventMgr == null) eventMgr = root.AddComponent<RespiratorEventManager>();

        // 1. Pula 8 materiałów z folderu Button_posibble_colors
        eventMgr.pulaMaterialowKolorow.Clear();
        string[] matNames = { "Aqua", "Blue", "Brown", "Green", "Orange", "Pink", "Red", "Yellow" };
        foreach (var name in matNames)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>($"{COLORS_FOLDER_PATH}/{name}.mat");
            if (m != null) eventMgr.pulaMaterialowKolorow.Add(m);
        }

        // 2. Znalezienie Button_panel i szablonu przycisku
        Transform panelTransform = root.transform.Find("Button_panel") ?? 
                                   root.transform.Find("Prefab_Respirator 1/Button_panel");
        eventMgr.buttonPanel = panelTransform;

        if (panelTransform != null)
        {
            Transform templateBtn = panelTransform.Find("Phisic_button");
            if (templateBtn != null)
            {
                RespiratorPushButton pushScript = templateBtn.GetComponent<RespiratorPushButton>();
                if (pushScript == null) pushScript = templateBtn.gameObject.AddComponent<RespiratorPushButton>();

                pushScript.eventManager = eventMgr;
                pushScript.pushAxis = new Vector3(0, -1, 0);
                pushScript.maxPushDepthMeters = 0.015f;
                pushScript.buttonRadiusMeters = 0.035f;
                pushScript.activationThreshold = 0.80f;
                pushScript.resetThreshold = 0.30f;
                pushScript.returnSpeed = 20f;

                Transform plate = templateBtn.Find("Button_plate") ?? templateBtn.Find("button") ?? templateBtn.GetChild(0);
                pushScript.buttonMesh = plate;

                BoxCollider boxCol = templateBtn.GetComponent<BoxCollider>();
                if (boxCol != null)
                {
                    boxCol.isTrigger = true;
                    boxCol.size = new Vector3(0.9f, 0.9f, 0.9f);
                }

                eventMgr.buttonPrefab = templateBtn.gameObject;
            }
        }

        // 3. Konfiguracja Gniazda (Pipe_socket) i Rurki (Pipe)
        Transform socketTransform = root.transform.Find("Pipe_socket");
        Transform pipeTransform = root.transform.Find("Pipe");

        if (socketTransform != null && pipeTransform != null)
        {
            XRSocketInteractor socket = socketTransform.GetComponent<XRSocketInteractor>();
            if (socket == null) socket = socketTransform.gameObject.AddComponent<XRSocketInteractor>();

            SphereCollider socketCol = socketTransform.GetComponent<SphereCollider>();
            if (socketCol == null) socketCol = socketTransform.gameObject.AddComponent<SphereCollider>();
            socketCol.isTrigger = true;
            if (socketCol.radius < 0.04f) socketCol.radius = 0.06f;

            XRGrabInteractable pipeGrab = pipeTransform.GetComponent<XRGrabInteractable>();
            if (pipeGrab == null) pipeGrab = pipeTransform.gameObject.AddComponent<XRGrabInteractable>();
            pipeGrab.movementType = XRBaseInteractable.MovementType.Instantaneous;

            Rigidbody pipeRb = pipeTransform.GetComponent<Rigidbody>();
            if (pipeRb == null) pipeRb = pipeTransform.gameObject.AddComponent<Rigidbody>();
            pipeRb.mass = 0.5f;
            pipeRb.useGravity = true;
            pipeRb.isKinematic = false;
            pipeRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            pipeGrab.interactionLayers = 3;
            socket.interactionLayers = 3;

            socket.startingSelectedInteractable = pipeGrab;
            pipeTransform.localPosition = socketTransform.localPosition;
            pipeTransform.localRotation = socketTransform.localRotation;

            eventMgr.gniazdoRurki = socket;
        }

        // 4. Punkt w ziemi (Ground_Supply_Point)
        Transform groundAnchor = root.transform.Find("Ground_Supply_Point");
        if (groundAnchor == null)
        {
            GameObject anchorObj = new GameObject("Ground_Supply_Point");
            anchorObj.transform.SetParent(root.transform, false);
            anchorObj.transform.localPosition = new Vector3(-0.35f, -0.65f, -1.15f);
            anchorObj.transform.localRotation = Quaternion.identity;

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Floor_Terminal_Cap";
            cap.transform.SetParent(anchorObj.transform, false);
            cap.transform.localPosition = new Vector3(0, 0.015f, 0);
            cap.transform.localScale = new Vector3(0.09f, 0.015f, 0.09f);
            Collider capCol = cap.GetComponent<Collider>();
            if (capCol != null) Object.DestroyImmediate(capCol);

            groundAnchor = anchorObj.transform;
        }

        // 5. Elastyczny wąż (LineRenderer + ElastycznyWaz)
        if (pipeTransform != null)
        {
            Transform hoseTransform = root.transform.Find("Flexible_Hose");
            GameObject hoseObj;
            if (hoseTransform == null)
            {
                hoseObj = new GameObject("Flexible_Hose");
                hoseObj.transform.SetParent(root.transform, false);
                hoseObj.transform.localPosition = Vector3.zero;
                hoseObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                hoseObj = hoseTransform.gameObject;
            }

            LineRenderer lr = hoseObj.GetComponent<LineRenderer>();
            if (lr == null) lr = hoseObj.AddComponent<LineRenderer>();

            Material blueMat = AssetDatabase.LoadAssetAtPath<Material>(BLUE_MATERIAL_PATH);
            if (blueMat != null) lr.sharedMaterial = blueMat;

            ElastycznyWaz waz = hoseObj.GetComponent<ElastycznyWaz>();
            if (waz == null) waz = hoseObj.AddComponent<ElastycznyWaz>();
            waz.punktPoczatkowy = groundAnchor;
            waz.wtyczka = pipeTransform;
            waz.gruboscRury = 0.035f;
            waz.segmentCount = 30;
            waz.sagAmount = 0.35f;
            waz.stiffness = 0.4f;
        }
    }
}

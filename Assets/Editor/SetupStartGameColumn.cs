using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class SetupStartGameColumn : EditorWindow
{
    [MenuItem("Magisterka/4. Skonfiguruj kolumnę startową (start game column)")]
    public static void ConfigureStartGameColumn()
    {
        // 1. Szukamy obiektu start game column w scenie
        GameObject columnObj = FindStartGameColumnObject();

        if (columnObj == null)
        {
            columnObj = CreateStartGameColumnHierarchy();
            Undo.RegisterCreatedObjectUndo(columnObj, "Create Start Game Column");
        }

        // 2. Wyrównanie pozycji na wygodną wysokość
        if (columnObj.transform.position.y > 1.5f || columnObj.transform.localScale.y > 1.5f)
        {
            Undo.RecordObject(columnObj.transform, "Adjust Column Height");
            columnObj.transform.position = new Vector3(-1.2f, 0.45f, -1.0f);
            columnObj.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
        }

        // 3. Szukamy lub dodajemy komponent StartGameButton
        StartGameButton startBtn = columnObj.GetComponentInChildren<StartGameButton>();
        if (startBtn == null)
        {
            startBtn = columnObj.AddComponent<StartGameButton>();
        }

        startBtn.hospitalManager = Object.FindFirstObjectByType<HospitalManager>();

        // 4. Szukamy ruchomej części przycisku
        Transform buttonMesh = columnObj.transform.Find("Start_Button") ?? 
                               columnObj.transform.Find("Button") ?? 
                               columnObj.transform.Find("button") ?? 
                               columnObj.transform.Find("Button_plate") ?? 
                               columnObj.transform.Find("Cap");

        if (buttonMesh == null)
        {
            GameObject btnObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            btnObj.name = "Start_Button";
            btnObj.transform.SetParent(columnObj.transform, false);
            btnObj.transform.localPosition = new Vector3(0, 0.98f, 0);
            btnObj.transform.localScale = new Vector3(0.6f, 0.08f, 0.6f);
            
            Material redMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Button_posibble_colors/Red.mat");
            if (redMat != null) btnObj.GetComponent<MeshRenderer>().material = redMat;

            buttonMesh = btnObj.transform;
        }
        else
        {
            buttonMesh.localPosition = new Vector3(0, 0.98f, 0);
            buttonMesh.localScale = new Vector3(0.6f, 0.08f, 0.6f);
        }

        // Upewniamy się, że przycisk ma Trigger collider (nie blokuje dłoni)
        Collider btnCol = buttonMesh.GetComponent<Collider>();
        if (btnCol != null) btnCol.isTrigger = true;

        startBtn.movingButtonMesh = buttonMesh;
        startBtn.localPushDirection = new Vector3(0, -1, 0);
        startBtn.maxPushDepthMeters = 0.025f;
        startBtn.buttonRadiusMeters = 0.12f;
        startBtn.activationThreshold = 0.80f;
        startBtn.returnSpeed = 15f;

        // 5. Dodajemy tablicę statusu 3D nad kolumną
        Transform canvasT = columnObj.transform.Find("Status_Canvas");
        if (canvasT == null)
        {
            GameObject canvasObj = new GameObject("Status_Canvas");
            canvasObj.transform.SetParent(columnObj.transform, false);
            canvasObj.transform.localPosition = new Vector3(0, 1.45f, 0);
            canvasObj.transform.localRotation = Quaternion.Euler(0, 0, 0);
            canvasObj.transform.localScale = Vector3.one * 0.002f;

            Canvas c = canvasObj.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;

            RectTransform rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350, 150);

            GameObject textObj = new GameObject("StatusText");
            textObj.transform.SetParent(canvasObj.transform, false);
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "<color=#FFFF33>START SYMULACJI</color>\n<size=16>Wciśnij przycisk, aby rozpocząć</size>";
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            startBtn.statusLabel = tmp;
        }
        else
        {
            startBtn.statusLabel = canvasT.GetComponentInChildren<TextMeshProUGUI>();
        }

        EditorUtility.SetDirty(columnObj);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Sukces!", 
            "Pomyślnie skonfigurowano Kolumnę Startową:\n\n" +
            "✔ Dłoń może swobodnie dotykać przycisku (nie blokuje się 5cm nad kolumną)\n" +
            "✔ Płynne wciskanie sześcianu/kopuły z oporem sprężyny\n" +
            "✔ Kliknięcie po osiągnięciu 80% zanurzenia\n" +
            "✔ Podpięto HospitalManager\n\n" +
            "Zapisz scenę (Ctrl+S) i wciśnij Play!", "Super!");

        Debug.Log($"[Magisterka] Pomyślnie skonfigurowano kolumnę startową: {columnObj.name}");
    }

    private static GameObject FindStartGameColumnObject()
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLower().Replace("_", " ").Trim();
                if (n.Contains("start game column") || n.Contains("start column") || n.Contains("kolumna start"))
                {
                    return t.gameObject;
                }
            }
        }
        return null;
    }

    private static GameObject CreateStartGameColumnHierarchy()
    {
        GameObject colObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        colObj.name = "Start_game_column";
        colObj.transform.position = new Vector3(-1.2f, 0.45f, -1.0f);
        colObj.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);

        Material greyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Grey.mat");
        if (greyMat != null) colObj.GetComponent<MeshRenderer>().material = greyMat;

        return colObj;
    }
}

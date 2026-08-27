using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

[InitializeOnLoad]
public class SetupRoomAndBackground
{
    private const string MAT_DIR = "Assets/Materials";

    // Wywołuje się automatycznie po skompilowaniu skryptu w Unity Editor
    [InitializeOnLoadMethod]
    public static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Application.isPlaying && !EditorApplication.isCompiling)
            {
                BuildHospitalRoomAndBackground(false);
            }
        };
    }

    [MenuItem("Tools/🏥 Zbuduj Sciany Szpitala i Tlo")]
    public static void MenuBuildRoom()
    {
        BuildHospitalRoomAndBackground(true);
    }

    public static void BuildHospitalRoomAndBackground(bool showDialog)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        // 1. Tworzymy / pobieramy materiały szpitalne
        EnsureMaterialsExist();

        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_Floor.mat");
        Material wallMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_Wall.mat");
        Material trimMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_Wall_Trim.mat");
        Material ceilingMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_Ceiling.mat");
        Material lightMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_CeilingLight.mat");
        Material windowMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_WindowView.mat");
        Material doorMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_Door.mat");
        Material medGasMat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Hospital_MedGas.mat");

        // 2. Podpinamy materiał pod główną podłogę (Plane)
        GameObject floorObj = GameObject.Find("Plane");
        if (floorObj != null && floorMat != null)
        {
            var mr = floorObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Undo.RecordObject(mr, "Assign Hospital Floor Material");
                mr.sharedMaterial = floorMat;
            }
        }

        // 3. Usuwamy stary pokój jeśli już istnieje
        GameObject oldRoom = GameObject.Find("Hospital_Room");
        if (oldRoom != null)
        {
            Undo.DestroyObjectImmediate(oldRoom);
        }

        // 4. Tworzymy główny kontener Hospital_Room
        GameObject roomRoot = new GameObject("Hospital_Room");
        Undo.RegisterCreatedObjectUndo(roomRoot, "Create Hospital Room");
        roomRoot.transform.position = Vector3.zero;
        roomRoot.transform.rotation = Quaternion.identity;

        float roomWidth = 10.0f;   // Oś X (-5 do +5)
        float roomLength = 10.0f;  // Oś Z (-5 do +5)
        float wallHeight = 3.8f;   // Wysokość ścian
        float wallThick = 0.15f;   // Grubość ścian

        // --- ŚCIANA WSCHODNIA (+X = 5.0, za stołem i respiratorami) ---
        GameObject wallEast = CreateWallSegment("Wall_East_BehindTable", 
            new Vector3(roomWidth * 0.5f + wallThick * 0.5f, wallHeight * 0.5f, 0), 
            new Vector3(wallThick, wallHeight, roomLength + wallThick * 2), 
            wallMat, roomRoot.transform);

        // Pas ochronny (szpitalny odbojnik) na ścianie wschodniej
        CreateWallSegment("Trim_East", 
            new Vector3(roomWidth * 0.5f - 0.01f, 1.0f, 0), 
            new Vector3(0.02f, 0.25f, roomLength), 
            trimMat, wallEast.transform);

        // Listwa instalacji gazów medycznych (Tlen, Powietrze, Próżnia) nad stołem
        GameObject medGasPanel = CreateWallSegment("Medical_Gas_Panel", 
            new Vector3(roomWidth * 0.5f - 0.03f, 1.6f, 0), 
            new Vector3(0.04f, 0.35f, 7.5f), 
            medGasMat, wallEast.transform);

        // --- ŚCIANA ZACHODNIA (-X = -5.0, za kolumną startową i graczem) ---
        GameObject wallWest = CreateWallSegment("Wall_West_WithWindow", 
            new Vector3(-roomWidth * 0.5f - wallThick * 0.5f, wallHeight * 0.5f, 0), 
            new Vector3(wallThick, wallHeight, roomLength + wallThick * 2), 
            wallMat, roomRoot.transform);

        // Pas ochronny na ścianie zachodniej
        CreateWallSegment("Trim_West", 
            new Vector3(-roomWidth * 0.5f + 0.01f, 1.0f, 0), 
            new Vector3(0.02f, 0.25f, roomLength), 
            trimMat, wallWest.transform);

        // Duże okno szpitalne z widokiem na panoramę/dzienne niebo
        GameObject windowFrame = CreateWallSegment("Hospital_Window_Frame", 
            new Vector3(-roomWidth * 0.5f + 0.02f, 2.1f, 0), 
            new Vector3(0.05f, 1.8f, 5.0f), 
            trimMat, wallWest.transform);

        GameObject windowGlass = CreateWallSegment("Window_View_Pane", 
            new Vector3(-roomWidth * 0.5f + 0.03f, 2.1f, 0), 
            new Vector3(0.02f, 1.65f, 4.8f), 
            windowMat, windowFrame.transform);

        // --- ŚCIANA PÓŁNOCNA (+Z = 5.0, z drzwiami wejściowymi na oddział) ---
        GameObject wallNorth = CreateWallSegment("Wall_North_WithDoor", 
            new Vector3(0, wallHeight * 0.5f, roomLength * 0.5f + wallThick * 0.5f), 
            new Vector3(roomWidth, wallHeight, wallThick), 
            wallMat, roomRoot.transform);

        CreateWallSegment("Trim_North", 
            new Vector3(0, 1.0f, roomLength * 0.5f - 0.01f), 
            new Vector3(roomWidth, 0.25f, 0.02f), 
            trimMat, wallNorth.transform);

        // Drzwi szpitalne (szklano-aluminiowe drzwi automatyczne OIT)
        GameObject doorFrame = CreateWallSegment("Hospital_ICU_Door", 
            new Vector3(-1.5f, 1.3f, roomLength * 0.5f - 0.02f), 
            new Vector3(2.2f, 2.6f, 0.05f), 
            doorMat, wallNorth.transform);

        // --- ŚCIANA POŁUDNIOWA (-Z = -5.0) ---
        GameObject wallSouth = CreateWallSegment("Wall_South", 
            new Vector3(0, wallHeight * 0.5f, -roomLength * 0.5f - wallThick * 0.5f), 
            new Vector3(roomWidth, wallHeight, wallThick), 
            wallMat, roomRoot.transform);

        CreateWallSegment("Trim_South", 
            new Vector3(0, 1.0f, -roomLength * 0.5f + 0.01f), 
            new Vector3(roomWidth, 0.25f, 0.02f), 
            trimMat, wallSouth.transform);

        // --- SUFIT (Ceiling) ---
        GameObject ceiling = CreateWallSegment("Ceiling", 
            new Vector3(0, wallHeight + 0.05f, 0), 
            new Vector3(roomWidth + wallThick * 2, 0.1f, roomLength + wallThick * 2), 
            ceilingMat, roomRoot.transform);

        // --- PANELE ŚWIETLNE LED NA SUFICIE ---
        Vector3[] lightPositions = new Vector3[]
        {
            new Vector3(-2.2f, wallHeight - 0.02f, -2.5f),
            new Vector3(-2.2f, wallHeight - 0.02f,  2.5f),
            new Vector3( 2.2f, wallHeight - 0.02f, -2.5f),
            new Vector3( 2.2f, wallHeight - 0.02f,  2.5f),
            new Vector3( 2.2f, wallHeight - 0.02f,  0.0f),
            new Vector3(-2.2f, wallHeight - 0.02f,  0.0f)
        };

        for (int i = 0; i < lightPositions.Length; i++)
        {
            GameObject lightPanel = CreateWallSegment($"Ceiling_LED_Panel_{i + 1}", 
                lightPositions[i], 
                new Vector3(1.2f, 0.04f, 0.6f), 
                lightMat, ceiling.transform);

            // Dodajemy punktowe miękkie światło rozproszone dla VR
            GameObject pLightObj = new GameObject($"PointLight_{i + 1}");
            pLightObj.transform.SetParent(lightPanel.transform, false);
            pLightObj.transform.localPosition = new Vector3(0, -0.2f, 0);

            Light light = pLightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.95f, 0.98f, 1.0f);
            light.intensity = 1.2f;
            light.range = 7.0f;
            light.shadows = LightShadows.None; // bez obciążania VR
        }

        // 5. Ustawienia oświetlenia otoczenia (RenderSettings)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.85f, 0.92f, 0.96f);     // Jasne medyczne niebo
        RenderSettings.ambientEquatorColor = new Color(0.70f, 0.76f, 0.80f); // Ściany
        RenderSettings.ambientGroundColor = new Color(0.50f, 0.55f, 0.58f);  // Podłoga
        RenderSettings.ambientIntensity = 1.15f;

        // 6. Dostosowanie głównego światła kierunkowego
        Light dirLight = Object.FindFirstObjectByType<Light>();
        if (dirLight != null && dirLight.type == LightType.Directional)
        {
            dirLight.color = new Color(1.0f, 0.98f, 0.95f);
            dirLight.intensity = 0.95f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        EditorUtility.SetDirty(roomRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Magisterka] 🏥 Pomyślnie zbudowano ściany sali szpitalnej (10x10m), sufit, oświetlenie LED i tło!");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Sukces!", 
                "Pomyślnie wygenerowano salę szpitalną i tło otoczenia:\n\n" +
                "✔ 4 ściany na obrzeżach podłogi (10x10m, wysokość 3.8m)\n" +
                "✔ Medyczny pas ochronny (odbojnik) i panel gazów medycznych (O2/Air/Vac)\n" +
                "✔ Duże okno dzienne z jasnym tłem zewnętrznym\n" +
                "✔ Drzwi automatyczne Oddziału Intensywnej Terapii (OIT)\n" +
                "✔ Sufit z 6 panelami LED i zbalansowanym oświetleniem VR\n\n" +
                "Scena została zaktualizowana i zapisana!", "Świetnie!");
        }
    }

    private static GameObject CreateWallSegment(string name, Vector3 pos, Vector3 size, Material mat, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = size;

        if (mat != null)
        {
            var mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
        }

        return obj;
    }

    private static void EnsureMaterialsExist()
    {
        if (!Directory.Exists(MAT_DIR))
        {
            Directory.CreateDirectory(MAT_DIR);
        }

        Shader standardShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Diffuse");

        // 1. Hospital Floor (Czyste jasnoszare/seledynowe linoleum szpitalne)
        CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_Floor.mat", standardShader, 
            new Color(0.80f, 0.84f, 0.83f, 1f), 0.05f, 0.45f);

        // 2. Hospital Wall (Jasny, sterylny medyczny odcień ścian)
        CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_Wall.mat", standardShader, 
            new Color(0.92f, 0.95f, 0.96f, 1f), 0.0f, 0.15f);

        // 3. Hospital Wall Trim (Medyczny błękitno-turkusowy pas odbojowy)
        CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_Wall_Trim.mat", standardShader, 
            new Color(0.18f, 0.42f, 0.55f, 1f), 0.1f, 0.4f);

        // 4. Hospital Ceiling (Matowy biały sufit)
        CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_Ceiling.mat", standardShader, 
            new Color(0.95f, 0.96f, 0.97f, 1f), 0.0f, 0.05f);

        // 5. Hospital Ceiling Light (Świecące panele LED z emisją)
        Material lightMat = CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_CeilingLight.mat", standardShader, 
            new Color(0.98f, 0.99f, 1f, 1f), 0.0f, 0.8f);
        if (lightMat != null)
        {
            lightMat.EnableKeyword("_EMISSION");
            lightMat.SetColor("_EmissionColor", new Color(1.2f, 1.2f, 1.25f, 1f));
        }

        // 6. Hospital Window View (Słoneczne dzienne tło za oknem)
        Material winMat = CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_WindowView.mat", standardShader, 
            new Color(0.75f, 0.88f, 1.0f, 1f), 0.0f, 0.6f);
        if (winMat != null)
        {
            winMat.EnableKeyword("_EMISSION");
            winMat.SetColor("_EmissionColor", new Color(0.85f, 0.95f, 1.15f, 1f));
        }

        // 7. Hospital Door (Szklano-aluminiowe drzwi)
        CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_Door.mat", standardShader, 
            new Color(0.35f, 0.48f, 0.58f, 1f), 0.4f, 0.6f);

        // 8. Hospital MedGas (Instalacja gazów medycznych: mosiądz/stal/kolory)
        CreateOrUpdateMaterial($"{MAT_DIR}/Hospital_MedGas.mat", standardShader, 
            new Color(0.70f, 0.75f, 0.78f, 1f), 0.6f, 0.7f);

        AssetDatabase.SaveAssets();
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader, Color col, float metallic, float smoothness)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = col;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

        EditorUtility.SetDirty(mat);
        return mat;
    }
}

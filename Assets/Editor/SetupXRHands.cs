using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class SetupXRHands : EditorWindow
{
    private const string LEFT_HAND_PREFAB_PATH = "Assets/Animated Hands/Prefabs/Left Hand Model.prefab";
    private const string RIGHT_HAND_PREFAB_PATH = "Assets/Animated Hands/Prefabs/Right Hand Model.prefab";
    private const string INPUT_ACTIONS_PATH = "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/XRI Default Input Actions.inputactions";

    [MenuItem("Magisterka/1. Wstaw pełne dłonie + Animacje + Chwytanie + Fizyka Kolizji (All-In-One)")]
    public static void BuildHandsAndGrabbingFromScratch()
    {
        GameObject leftHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LEFT_HAND_PREFAB_PATH);
        GameObject rightHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RIGHT_HAND_PREFAB_PATH);
        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH);

        if (leftHandPrefab == null || rightHandPrefab == null)
        {
            EditorUtility.DisplayDialog("Błąd", "Nie znaleziono prefabów dłoni w:\nAssets/Animated Hands/Prefabs/", "OK");
            return;
        }

        if (inputActions == null)
        {
            EditorUtility.DisplayDialog("Błąd", "Nie znaleziono pliku Input Actions w:\n" + INPUT_ACTIONS_PATH, "OK");
            return;
        }

        XRInteractionManager interactionManager = EnsureXRInteractionManager();
        EnsureInputActionManager(inputActions);

        List<Transform> allTransforms = new List<Transform>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));
        }

        Transform cameraOffset = allTransforms.Find(t => t.name.Equals("Camera Offset", System.StringComparison.OrdinalIgnoreCase));
        Transform leftHandTarget = FindHandTransform(allTransforms, true);
        Transform rightHandTarget = FindHandTransform(allTransforms, false);

        if (cameraOffset != null)
        {
            if (leftHandTarget == null || leftHandTarget.parent != cameraOffset)
            {
                Transform existingLeft = cameraOffset.Find("Left Hand");
                if (existingLeft != null) leftHandTarget = existingLeft;
                else
                {
                    GameObject newLeft = new GameObject("Left Hand");
                    Undo.RegisterCreatedObjectUndo(newLeft, "Create Left Hand GameObject");
                    newLeft.transform.SetParent(cameraOffset, false);
                    leftHandTarget = newLeft.transform;
                }
            }

            if (rightHandTarget == null || rightHandTarget.parent != cameraOffset)
            {
                Transform existingRight = cameraOffset.Find("Right Hand");
                if (existingRight != null) rightHandTarget = existingRight;
                else
                {
                    GameObject newRight = new GameObject("Right Hand");
                    Undo.RegisterCreatedObjectUndo(newRight, "Create Right Hand GameObject");
                    newRight.transform.SetParent(cameraOffset, false);
                    rightHandTarget = newRight.transform;
                }
            }
        }

        if (leftHandTarget == null || rightHandTarget == null)
        {
            EditorUtility.DisplayDialog("Błąd", "Nie znaleziono Camera Offset ani obiektów rąk na scenie.", "OK");
            return;
        }

        InputActionReference leftPosRef = FindActionReference(inputActions, "XRI Left", "Position");
        InputActionReference leftRotRef = FindActionReference(inputActions, "XRI Left", "Rotation");
        InputActionReference leftTriggerRef = FindActionReference(inputActions, "XRI Left Interaction", "Activate Value") ??
                                              FindActionReference(inputActions, "XRI Left Interaction", "Activate");
        InputActionReference leftGripRef = FindActionReference(inputActions, "XRI Left Interaction", "Select Value") ??
                                           FindActionReference(inputActions, "XRI Left Interaction", "Select");

        InputActionReference rightPosRef = FindActionReference(inputActions, "XRI Right", "Position");
        InputActionReference rightRotRef = FindActionReference(inputActions, "XRI Right", "Rotation");
        InputActionReference rightTriggerRef = FindActionReference(inputActions, "XRI Right Interaction", "Activate Value") ??
                                               FindActionReference(inputActions, "XRI Right Interaction", "Activate");
        InputActionReference rightGripRef = FindActionReference(inputActions, "XRI Right Interaction", "Select Value") ??
                                            FindActionReference(inputActions, "XRI Right Interaction", "Select");

        SetupTrackedPoseDriver(leftHandTarget.gameObject, leftPosRef, leftRotRef);
        SetupTrackedPoseDriver(rightHandTarget.gameObject, rightPosRef, rightRotRef);

        CleanAndSetupHand(leftHandTarget, leftHandPrefab, VRHandAnimator.HandSide.Left, leftTriggerRef, leftGripRef);
        CleanAndSetupHand(rightHandTarget, rightHandPrefab, VRHandAnimator.HandSide.Right, rightTriggerRef, rightGripRef);

        SetupDirectInteractor(leftHandTarget.gameObject, leftTriggerRef, leftGripRef, interactionManager);
        SetupDirectInteractor(rightHandTarget.gameObject, rightTriggerRef, rightGripRef, interactionManager);

        SetupFingerColliderOnBone(leftHandTarget, true);
        SetupFingerColliderOnBone(rightHandTarget, false);

        // Fizyczna kolizja gracza i kamery ze stołem
        SetupPhysicalPlayerCollision(allTransforms);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Sukces!", 
            "Pomyślnie skonfigurowano pełne dłonie i fizykę gracza:\n\n" +
            "✔ Fizyczne zatrzymywanie gracza i kamery przed stołem/respiratorem (VRPhysicalPlayerAndCameraCollision)\n" +
            "✔ Fizyczne zatrzymywanie obu dłoni na przeszkodach (VRPhysicsHand)\n" +
            "✔ Animacje dłoni: Zginanie palców (Trigger & Grip)\n" +
            "✔ Chwytanie: XRDirectInteractor\n" +
            "✔ Wciskanie przycisków: Finger Collider na czubku palca\n" +
            "✔ Ściemnienie kolizyjne głowy (VRPlayerCollisionFade)\n\n" +
            "Zapisz scenę (Ctrl+S) i wciśnij Play!", "Super!");
        
        Debug.Log("[Magisterka] Pomyślnie zintegrowano dłonie, fizykę gracza i kolizje.");
    }

    [MenuItem("Magisterka/Interakcje/1. Skonfiguruj Fizyczne Kolizje Dłoni i Ciała (Anti-Clipping ze stołem i respiratorem)")]
    public static void SetupHandPhysicsAndBodyCollisionOnly()
    {
        List<Transform> allTransforms = new List<Transform>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));
        }

        Transform leftHandTarget = FindHandTransform(allTransforms, true);
        Transform rightHandTarget = FindHandTransform(allTransforms, false);

        if (leftHandTarget != null)
        {
            Transform leftModel = FindModelChild(leftHandTarget);
            if (leftModel != null)
            {
                VRPhysicsHand ph = leftModel.GetComponent<VRPhysicsHand>() ?? leftModel.gameObject.AddComponent<VRPhysicsHand>();
                ph.targetController = leftHandTarget;
                ph.maxTeleportDistance = 0.35f;
                ph.handRadius = 0.045f;
                EditorUtility.SetDirty(leftModel);
                Debug.Log($"[Magisterka] Podpięto VRPhysicsHand pod Lewą Dłoń: {leftModel.name}");
            }
        }

        if (rightHandTarget != null)
        {
            Transform rightModel = FindModelChild(rightHandTarget);
            if (rightModel != null)
            {
                VRPhysicsHand ph = rightModel.GetComponent<VRPhysicsHand>() ?? rightModel.gameObject.AddComponent<VRPhysicsHand>();
                ph.targetController = rightHandTarget;
                ph.maxTeleportDistance = 0.35f;
                ph.handRadius = 0.045f;
                EditorUtility.SetDirty(rightModel);
                Debug.Log($"[Magisterka] Podpięto VRPhysicsHand pod Prawą Dłoń: {rightModel.name}");
            }
        }

        SetupPhysicalPlayerCollision(allTransforms);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Sukces", 
            "Skonfigurowano pełne fizyczne kolizje:\n\n" +
            "✔ Zatrzymywanie gracza i kamery przed stołem/respiratorem (VRPhysicalPlayerAndCameraCollision)\n" +
            "✔ Zatrzymywanie obu dłoni na stole i respiratorze (VRPhysicsHand)\n" +
            "✔ Ściemnienie głowy w przeszkodach i podgląd pozycji gracza na żywo!", "OK");
    }

    private static void SetupPhysicalPlayerCollision(List<Transform> allTransforms)
    {
        Camera cam = FindMainCamera(allTransforms);
        Transform xrOrigin = GameObject.Find("XR Origin (VR)")?.transform ?? 
                             GameObject.Find("XR Origin")?.transform ?? 
                             GameObject.Find("XR Rig")?.transform;

        if (xrOrigin != null)
        {
            var physCol = xrOrigin.GetComponent<VRPhysicalPlayerAndCameraCollision>() ?? xrOrigin.gameObject.AddComponent<VRPhysicalPlayerAndCameraCollision>();
            physCol.xrOriginRoot = xrOrigin;
            if (cam != null) physCol.playerCamera = cam.transform;
            physCol.bodyRadius = 0.22f;
            physCol.maxTunnelDistance = 0.55f;
            EditorUtility.SetDirty(xrOrigin.gameObject);
            Debug.Log($"[Magisterka] Podpięto VRPhysicalPlayerAndCameraCollision pod XR Origin: {xrOrigin.name}");
        }

        if (cam != null)
        {
            if (cam.GetComponent<VRPlayerCollisionFade>() == null)
            {
                cam.gameObject.AddComponent<VRPlayerCollisionFade>();
            }
            EditorUtility.SetDirty(cam.gameObject);
        }
    }

    private static Transform FindModelChild(Transform handRoot)
    {
        VRHandAnimator anim = handRoot.GetComponentInChildren<VRHandAnimator>(true);
        if (anim != null) return anim.transform;

        foreach (Transform child in handRoot)
        {
            string n = child.name.ToLower();
            if (n.Contains("model") || n.Contains("hand"))
            {
                return child;
            }
        }

        return handRoot.childCount > 0 ? handRoot.GetChild(0) : null;
    }

    private static Camera FindMainCamera(List<Transform> allTransforms)
    {
        if (Camera.main != null) return Camera.main;

        Transform camT = allTransforms.Find(t => t.name.Equals("Main Camera", System.StringComparison.OrdinalIgnoreCase));
        if (camT != null) return camT.GetComponent<Camera>();

        Camera anyCam = Object.FindFirstObjectByType<Camera>();
        return anyCam;
    }

    [MenuItem("Magisterka/Interakcje/2. Skonfiguruj tylko Collidery Palca Wskazującego na dłoniach")]
    public static void SetupOnlyFingerColliders()
    {
        List<Transform> allTransforms = new List<Transform>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));
        }

        Transform leftHandTarget = FindHandTransform(allTransforms, true);
        Transform rightHandTarget = FindHandTransform(allTransforms, false);

        if (leftHandTarget != null) SetupFingerColliderOnBone(leftHandTarget, true);
        if (rightHandTarget != null) SetupFingerColliderOnBone(rightHandTarget, false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Sukces", "Skonfigurowano collidery na czubkach palców wskazujących dla obu dłoni!", "OK");
    }

    public static void SetupFingerColliderOnBone(Transform handRoot, bool isLeft)
    {
        string[] targetBoneNames = isLeft ? 
            new string[] { "hands:b_l_index_ignore", "hands:b_l_index3", "b_l_index3", "index3", "index_tip" } :
            new string[] { "hands:b_r_index_ignore", "hands:b_r_index3", "b_r_index3", "index3", "index_tip" };

        Transform indexBone = null;
        Transform[] allChildren = handRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            foreach (var bName in targetBoneNames)
            {
                if (t.name.Equals(bName, System.StringComparison.OrdinalIgnoreCase))
                {
                    indexBone = t;
                    break;
                }
            }
            if (indexBone != null) break;
        }

        Transform parentBone = indexBone != null ? indexBone : handRoot;

        Transform existingFinger = parentBone.Find("fingercollider");
        if (existingFinger == null)
        {
            foreach (var t in allChildren)
            {
                if (t.name.Equals("fingercollider", System.StringComparison.OrdinalIgnoreCase))
                {
                    existingFinger = t;
                    break;
                }
            }
        }

        GameObject fingerObj;
        if (existingFinger == null)
        {
            fingerObj = new GameObject("fingercollider");
            Undo.RegisterCreatedObjectUndo(fingerObj, "Create Finger Collider");
            fingerObj.transform.SetParent(parentBone, false);
            if (indexBone != null)
            {
                fingerObj.transform.localPosition = Vector3.zero;
            }
            else
            {
                fingerObj.transform.localPosition = isLeft ? new Vector3(-0.015f, 0.01f, 0.095f) : new Vector3(0.015f, 0.01f, 0.095f);
            }
            fingerObj.transform.localRotation = Quaternion.identity;
            fingerObj.transform.localScale = Vector3.one;
        }
        else
        {
            fingerObj = existingFinger.gameObject;
            if (indexBone != null && fingerObj.transform.parent != indexBone)
            {
                Undo.SetTransformParent(fingerObj.transform, indexBone, "Reparent Finger Collider to Index Bone");
                fingerObj.transform.localPosition = Vector3.zero;
            }
        }

        SphereCollider sc = fingerObj.GetComponent<SphereCollider>();
        if (sc == null) sc = fingerObj.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.015f;
        sc.center = Vector3.zero;

        Rigidbody rb = fingerObj.GetComponent<Rigidbody>();
        if (rb == null) rb = fingerObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        EditorUtility.SetDirty(fingerObj);
    }

    private static XRInteractionManager EnsureXRInteractionManager()
    {
        XRInteractionManager manager = Object.FindFirstObjectByType<XRInteractionManager>();
        if (manager == null)
        {
            GameObject mgrObj = new GameObject("XR Interaction Manager");
            Undo.RegisterCreatedObjectUndo(mgrObj, "Create XR Interaction Manager");
            manager = mgrObj.AddComponent<XRInteractionManager>();
        }
        return manager;
    }

    private static void EnsureInputActionManager(InputActionAsset asset)
    {
        InputActionManager manager = Object.FindFirstObjectByType<InputActionManager>();
        if (manager == null)
        {
            GameObject mgrObj = GameObject.Find("XR Interaction Manager") ?? new GameObject("Input Action Manager");
            manager = mgrObj.GetComponent<InputActionManager>() ?? Undo.AddComponent<InputActionManager>(mgrObj);
        }

        if (manager != null && asset != null)
        {
            SerializedObject so = new SerializedObject(manager);
            SerializedProperty listProp = so.FindProperty("m_ActionAssets");
            if (listProp != null)
            {
                bool exists = false;
                for (int i = 0; i < listProp.arraySize; i++)
                {
                    if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    int index = listProp.arraySize;
                    listProp.InsertArrayElementAtIndex(index);
                    listProp.GetArrayElementAtIndex(index).objectReferenceValue = asset;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(manager);
                }
            }
        }
    }

    private static void SetupDirectInteractor(GameObject handRoot, InputActionReference activateRef, InputActionReference selectRef, XRInteractionManager manager)
    {
        Undo.RecordObject(handRoot, "Setup Direct Interactor");

        SphereCollider col = handRoot.GetComponent<SphereCollider>();
        if (col == null) col = Undo.AddComponent<SphereCollider>(handRoot);
        col.isTrigger = true;
        col.radius = 0.085f;
        col.center = new Vector3(0f, -0.02f, 0.04f);

        XRDirectInteractor direct = handRoot.GetComponent<XRDirectInteractor>();
        if (direct == null) direct = Undo.AddComponent<XRDirectInteractor>(handRoot);
        direct.interactionManager = manager;
        direct.improveAccuracyWithSphereCollider = true;

        SerializedObject so = new SerializedObject(direct);
        SerializedProperty selectProp = so.FindProperty("m_SelectInput");
        SerializedProperty activateProp = so.FindProperty("m_ActivateInput");

        if (selectProp != null && selectRef != null)
        {
            selectProp.FindPropertyRelative("m_InputSourceMode").enumValueIndex = (int)XRInputButtonReader.InputSourceMode.InputActionReference;
            selectProp.FindPropertyRelative("m_InputActionReferencePerformed").objectReferenceValue = selectRef;
        }

        if (activateProp != null && activateRef != null)
        {
            activateProp.FindPropertyRelative("m_InputSourceMode").enumValueIndex = (int)XRInputButtonReader.InputSourceMode.InputActionReference;
            activateProp.FindPropertyRelative("m_InputActionReferencePerformed").objectReferenceValue = activateRef;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(handRoot);
    }

    private static void SetInputActionProperty(SerializedObject so, string propertyName, InputActionReference actionRef)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null && actionRef != null)
        {
            prop.FindPropertyRelative("m_UseReference").boolValue = true;
            prop.FindPropertyRelative("m_Reference").objectReferenceValue = actionRef;
        }
    }

    private static InputActionReference FindActionReference(InputActionAsset asset, string mapName, string actionName)
    {
        if (asset == null) return null;

        string path = AssetDatabase.GetAssetPath(asset);
        Object[] allSubAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        
        foreach (var sub in allSubAssets)
        {
            if (sub is InputActionReference actRef && actRef.action != null)
            {
                bool mapMatch = string.IsNullOrEmpty(mapName) || 
                    (actRef.action.actionMap != null && 
                     (actRef.action.actionMap.name.Equals(mapName, System.StringComparison.OrdinalIgnoreCase) ||
                      actRef.action.actionMap.name.Replace(" ", "").Equals(mapName.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase)));
                
                bool actionMatch = actRef.action.name.Equals(actionName, System.StringComparison.OrdinalIgnoreCase);

                if (mapMatch && actionMatch) return actRef;
            }
        }

        InputAction directAct = asset.FindActionMap(mapName)?.FindAction(actionName);
        if (directAct != null)
        {
            foreach (var sub in allSubAssets)
            {
                if (sub is InputActionReference actRef && actRef.action != null && actRef.action.id == directAct.id)
                {
                    return actRef;
                }
            }
            return InputActionReference.Create(directAct);
        }

        return null;
    }

    private static void SetupTrackedPoseDriver(GameObject target, InputActionReference posRef, InputActionReference rotRef)
    {
        TrackedPoseDriver driver = target.GetComponent<TrackedPoseDriver>();
        if (driver == null) driver = Undo.AddComponent<TrackedPoseDriver>(target);

        driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

        SerializedObject so = new SerializedObject(driver);
        SerializedProperty posProp = so.FindProperty("m_PositionInput");
        SerializedProperty rotProp = so.FindProperty("m_RotationInput");

        if (posProp != null && posRef != null)
        {
            posProp.FindPropertyRelative("m_UseReference").boolValue = true;
            posProp.FindPropertyRelative("m_Reference").objectReferenceValue = posRef;
        }

        if (rotProp != null && rotRef != null)
        {
            rotProp.FindPropertyRelative("m_UseReference").boolValue = true;
            rotProp.FindPropertyRelative("m_Reference").objectReferenceValue = rotRef;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static Transform FindHandTransform(List<Transform> allTransforms, bool isLeft)
    {
        string[] candidates = isLeft
            ? new string[] { "Left Hand", "LeftHand", "Left Controller", "LeftController" }
            : new string[] { "Right Hand", "RightHand", "Right Controller", "RightController" };

        foreach (string name in candidates)
        {
            Transform found = allTransforms.Find(t => t.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
        }

        string keyword = isLeft ? "left" : "right";
        return allTransforms.Find(t => {
            string n = t.name.ToLower();
            return n.Contains(keyword) && (n.Contains("hand") || n.Contains("controller")) && 
                   !n.Contains("teleport") && !n.Contains("turn") && !n.Contains("move") && !n.Contains("snap");
        });
    }

    private static void CleanAndSetupHand(Transform handRoot, GameObject handPrefab, VRHandAnimator.HandSide side, InputActionReference triggerRef, InputActionReference gripRef)
    {
        string handName = side == VRHandAnimator.HandSide.Left ? "Left Hand Model" : "Right Hand Model";

        Transform[] children = handRoot.GetComponentsInChildren<Transform>(true);
        foreach (var t in children)
        {
            if (t == handRoot) continue;
            string n = t.name.ToLower();
            if (n.Contains("universalcontroller") || n.Contains("controller_base") || 
                n.Contains("thumbstick") || n.Contains("trigger") || n.Contains("bumper") || 
                n.Contains("touchpad") || n.Contains("button_") || n.Contains("questvisual") ||
                n.Contains("androidxrvisual") || n.Contains("visualmodel"))
            {
                Undo.RecordObject(t.gameObject, "Hide Old Visual Mesh");
                t.gameObject.SetActive(false);
            }
        }

        Transform oldInstance = handRoot.Find(handName);
        if (oldInstance != null)
        {
            Undo.DestroyObjectImmediate(oldInstance.gameObject);
        }

        GameObject handInstance = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, handRoot);
        handInstance.name = handName;
        handInstance.transform.localPosition = Vector3.zero;
        handInstance.transform.localRotation = Quaternion.identity;
        handInstance.transform.localScale = Vector3.one;
        handInstance.SetActive(true);

        VRHandAnimator animatorDriver = handInstance.GetComponent<VRHandAnimator>();
        if (animatorDriver == null) animatorDriver = handInstance.AddComponent<VRHandAnimator>();
        animatorDriver.handSide = side;
        animatorDriver.autoTrackTransform = true;

        // Dodanie fizyki kolizji ze stołem (VRPhysicsHand)
        VRPhysicsHand physicsHand = handInstance.GetComponent<VRPhysicsHand>();
        if (physicsHand == null) physicsHand = handInstance.AddComponent<VRPhysicsHand>();
        physicsHand.targetController = handRoot;
        physicsHand.maxTeleportDistance = 0.35f;
        physicsHand.handRadius = 0.045f;

        SerializedObject so = new SerializedObject(animatorDriver);
        SetInputActionProperty(so, "triggerAction", triggerRef);
        SetInputActionProperty(so, "gripAction", gripRef);
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(handInstance, "Instantiate Animated Hand");
        EditorUtility.SetDirty(handInstance);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerHandVisualizer : MonoBehaviour
{
    public enum HandSide { Left, Right }
    public HandSide handSide = HandSide.Left;

    [Header("Modele Dłoni")]
    [Tooltip("Prefab modelu dłoni do wyświetlenia (np. LeftHandQuestVisual / RightHandQuestVisual)")]
    public GameObject handModelPrefab;

    [Header("Ukrywanie plastiku kontrolera")]
    [Tooltip("Obiekt siatki kontrolera do ukrycia")]
    public GameObject controllerMeshToHide;

    [Header("Offset pozycji i rotacji dłoni względem kontrolera")]
    public Vector3 localPositionOffset = Vector3.zero;
    public Vector3 localRotationOffset = Vector3.zero;

    private GameObject spawnedHandInstance;

    void Awake()
    {
        // 1. Ukrywamy model plastikowego pilota
        HideControllerMesh();

        // 2. Tworzymy i dołączamy model dłoni
        SpawnHandModel();
    }

    private void HideControllerMesh()
    {
        if (controllerMeshToHide != null)
        {
            controllerMeshToHide.SetActive(false);
            return;
        }

        // Szukamy obiektu UniversalController / Controller_Base w hierarchii
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child == transform) continue;

            string n = child.name.ToLower();
            if (n.Contains("universalcontroller") || n.Contains("controller_base") || n.Contains("controller visual"))
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void SpawnHandModel()
    {
        if (handModelPrefab != null && spawnedHandInstance == null)
        {
            spawnedHandInstance = Instantiate(handModelPrefab, transform);
            spawnedHandInstance.name = $"{handSide}Hand_VisualModel";
            spawnedHandInstance.transform.localPosition = localPositionOffset;
            spawnedHandInstance.transform.localRotation = Quaternion.Euler(localRotationOffset);
            spawnedHandInstance.transform.localScale = Vector3.one;
            spawnedHandInstance.SetActive(true);
        }
    }
}

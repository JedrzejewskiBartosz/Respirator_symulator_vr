using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class RespiratorButton : MonoBehaviour
{
    [Tooltip("Litera przypisana do przycisku, np. Z dla Zielonego")]
    public string buttonID = "Z";

    [Tooltip("G��wny mened�er zarz�dzaj�cy awariami")]
    public RespiratorEventManager eventManager;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        // Podpinamy si� pod event "Select Entered" (czyli wci�ni�cie przycisku/lasera)
        interactable.selectEntered.AddListener(OnPush);
    }

    private void OnPush(SelectEnterEventArgs args)
    {
        if (eventManager != null)
        {
            // Wysy�amy nasze ID do mened�era
            eventManager.OnButtonPressed(buttonID);
            Debug.Log("Wci�ni�to przycisk: " + buttonID);
        }
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnPush);
        }
    }
}
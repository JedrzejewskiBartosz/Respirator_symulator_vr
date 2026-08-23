using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class LaserToggle : MonoBehaviour
{
    public XRRayInteractor prawyLaser; // Przeci¹gnij tu swój XR Ray Interactor

    public void PrzelaczLaser()
    {
        if (prawyLaser != null)
        {
            // W³¹cza/wy³¹cza komponent rysuj¹cy czerwon¹ liniê
            var linia = prawyLaser.GetComponent<XRInteractorLineVisual>();
            if (linia != null) linia.enabled = !linia.enabled;
        }
    }
}
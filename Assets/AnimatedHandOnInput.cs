using UnityEngine;
using UnityEngine.InputSystem;

public class AnimatedHandOnInput : MonoBehaviour
{
    public InputActionProperty triggerValue;
    public InputActionProperty gripValue;

    public Animator handAnimator;

    void Awake()
    {
        if (handAnimator == null)
        {
            handAnimator = GetComponent<Animator>();
            if (handAnimator == null) handAnimator = GetComponentInChildren<Animator>();
        }

        if (handAnimator != null)
        {
            handAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    void OnEnable()
    {
        if (triggerValue.action != null && !triggerValue.action.enabled) triggerValue.action.Enable();
        if (gripValue.action != null && !gripValue.action.enabled) gripValue.action.Enable();
    }

    void Update()
    {
        if (handAnimator == null)
        {
            handAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (handAnimator == null) return;
        }

        float trigger = 0f;
        float grip = 0f;

        if (triggerValue.action != null)
        {
            if (!triggerValue.action.enabled) triggerValue.action.Enable();
            try { trigger = triggerValue.action.ReadValue<float>(); }
            catch { trigger = triggerValue.action.IsPressed() ? 1f : 0f; }
        }

        if (gripValue.action != null)
        {
            if (!gripValue.action.enabled) gripValue.action.Enable();
            try { grip = gripValue.action.ReadValue<float>(); }
            catch { grip = gripValue.action.IsPressed() ? 1f : 0f; }
        }

        handAnimator.SetFloat("Trigger", trigger);
        handAnimator.SetFloat("Grip", grip);
    }
}

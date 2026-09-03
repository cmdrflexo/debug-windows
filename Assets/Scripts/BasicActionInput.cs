using UnityEngine;
using UnityEngine.InputSystem;

public class BasicActionInput : MonoBehaviour {
    public InputActionReference toggleAction;

    private void OnEnable() {
        toggleAction.action.performed += OnToggle;
        toggleAction.action.Enable();
    }

    private void OnDisable() {
        toggleAction.action.performed -= OnToggle;
        toggleAction.action.Disable();
    }

    protected virtual void OnToggle(InputAction.CallbackContext context){ }
}
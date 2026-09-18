using UnityEngine;
using UnityEngine.InputSystem;

public class BasicActionInput : MonoBehaviour {
    
    public InputActionReference toggleAction;
    public bool toggleOnStart;

	private void Start() {
        if(toggleOnStart) OnToggle();
	}

	private void OnEnable() {
        toggleAction.action.performed += OnToggleInput;
        toggleAction.action.Enable();
    }

    private void OnDisable() {
        toggleAction.action.performed -= OnToggleInput;
        toggleAction.action.Disable();
    }

    protected virtual void OnToggle(){ }
    private void OnToggleInput(InputAction.CallbackContext context) {
        OnToggle();
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

public class BasicToggleComponents : BasicActionInput {
    
    [SerializeField] private Behaviour[] components;

    protected override void OnToggle(InputAction.CallbackContext context) {
        foreach(Behaviour component in components)
            component.enabled = !component.enabled;
    }
}

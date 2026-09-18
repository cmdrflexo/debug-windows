using UnityEngine;

public class BasicToggleComponents : BasicActionInput {
    
    [SerializeField] private Behaviour[] components;

    protected override void OnToggle() {
        foreach(Behaviour component in components)
            component.enabled = !component.enabled;
    }
}

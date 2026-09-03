using UnityEngine;

public class BasicToggleComponent : MonoBehaviour {

	public Behaviour component;
	public KeyCode keyCode;

	private void Update() {
		if(Input.GetKeyDown(keyCode))
			component.enabled = !component.enabled;

	}

}

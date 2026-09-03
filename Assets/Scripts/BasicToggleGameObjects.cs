using UnityEngine;
using UnityEngine.InputSystem;

public class BasicToggleGameObjects : BasicActionInput {
    
    [SerializeField] private GameObject[] gameObjects;

    protected override void OnToggle(InputAction.CallbackContext context) {
        foreach(GameObject _gameObject in gameObjects)
            _gameObject.SetActive(!_gameObject.activeSelf);
    }
}

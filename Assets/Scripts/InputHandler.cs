using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{

    public NotPlayerController CharacterController;

    private InputAction _moveAction, _turnAction, _diveAction, _flapAction, _fishAction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _moveAction = InputSystem.actions.FindAction("Move");
        _turnAction = InputSystem.actions.FindAction("Turn");
        _diveAction = InputSystem.actions.FindAction("Dive");
        _flapAction = InputSystem.actions.FindAction("Flap");
        _fishAction = InputSystem.actions.FindAction("Fish");

        Cursor.visible = false; 
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveVec = _moveAction.ReadValue<Vector2>();
        CharacterController.Move(moveVec);
        
        Vector2 turnVec = _turnAction.ReadValue<Vector2>();
        CharacterController.Rotate(turnVec);

        Vector3 diveVec = _diveAction.ReadValue<Vector3>();
        CharacterController.Dive(diveVec);

        Vector3 riseVec = _flapAction.ReadValue<Vector3>();
        CharacterController.Rise(riseVec);
        
    }
}

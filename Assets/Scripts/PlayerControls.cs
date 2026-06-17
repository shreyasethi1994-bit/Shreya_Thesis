using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public Rigidbody2D Rigidbody2D;

    public float moveSpeed;

    private Vector2 _moveDirection;

    public InputActionReference move;

    private void Update()
    {
        _moveDirection = move.action.ReadValue<Vector2>();
    }


    private void FixedUpdate()
    {
        rb.velocity = new Vector2(x:_moveDirection.x * moveSpeed, y:_moveDirection.y * moveSpeed);
    }
}

using UnityEngine;

public class NotPlayerController : MonoBehaviour
{

    private CharacterController characterController;

    public float moveSpeed = 5.0f;
    public float turnSpeed = 0.75f;
    public float diveSpeed = 1.17f;
    public float riseSpeed = 1.01f;
    public float gravity = 9.8f;
    public float weight = 675.0f;

    private float _XAxis, _YAxis, _ZAxis;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    /*void Update()
    {
        
    }
    */
    
    public void Move(Vector2 moveVec)
    {
        Vector3 movement = transform.forward * moveVec.y + transform.right * moveVec.x;
        movement = movement * moveSpeed * Time.deltaTime;
        characterController.Move(movement);

    }

    public void Rotate(Vector2 rotateVec)
    {
        _YAxis += rotateVec.x * turnSpeed * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(0, _YAxis, 0);
    }

    public void Dive(Vector3 diveVec)
    {
        _ZAxis -= diveVec.z * diveSpeed * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(0, 0, _ZAxis);
    }

    public void Rise(Vector3 riseVec)
    {
        _ZAxis += riseVec.z * riseSpeed * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(0, 0, _ZAxis);
    }
}

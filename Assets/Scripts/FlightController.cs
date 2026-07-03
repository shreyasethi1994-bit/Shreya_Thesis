//usings

using UnityEngine;
using UnityEngine.InputSystem;

//-------------

public class FlightController : MonoBehaviour
{

    [SerializeField] public float thrust;
    [SerializeField] public float thrustMultiplier;
    [SerializeField] public float yawMulitiplier;
    [SerializeField] public float pitchMultiplier;

    new Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        float pitch = Input.GetAxis("Vertical");

        float yaw = Input.GetAxis("Horizontal");

        rb.AddRelativeForce(0f, (thrust * thrustMultiplier * deltaTime), 0f);
        rb.AddRelativeForce((pitch * pitchMultiplier * deltaTime), (yaw * yawMulitiplier * deltaTime), (-yaw * yawMulitiplier * deltaTime));
    }

}
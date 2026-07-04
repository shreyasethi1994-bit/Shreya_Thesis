using UnityEngine;
using UnityEngine.InputSystem;

public class FlightController : MonoBehaviour
{

    [SerializeField] public float thrust;
    [SerializeField] public float thrustMultiplier;
    [SerializeField] public float yawMulitiplier;

    [Tooltip("How quickly speed changes catch up to the stick input. Higher = snappier, lower = more gliding/inertia.")]
    [SerializeField] private float accelSmoothing = 5f;

    [Header("Reverse Hold Turnaround")]
    [Tooltip("How long the left stick must be held back before the bird auto-turns 180 degrees.")]
    [SerializeField] private float reverseHoldDuration = 1f;
    [Tooltip("How far back the stick must be pushed (0-1) to count as 'holding reverse'.")]
    [SerializeField] private float reverseInputThreshold = 0.5f;

    [SerializeField] private Vector3 moveDirection;
    private Vector3 turnDirection;

    // The bird's sprite is a flat quad tilted to lie on the ground plane (its saved rotation),
    // not a 3D model with a nose - so we never touch pitch/roll, only turn it around world up.
    private Quaternion flatRotation;
    private float yawAngle;

    private float reverseHoldTimer;
    private bool hasTurnedAroundThisHold;

    [SerializeField] new Rigidbody rb;

    public InputActionReference fly;

    public InputActionReference turn;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        flatRotation = transform.rotation;
        yawAngle = 0f;
    }

    private void OnEnable()
    {
        fly.action.Enable();
        turn.action.Enable();
    }

    private void OnDisable()
    {
        fly.action.Disable();
        turn.action.Disable();
    }

    private void Update()
    {
        moveDirection = fly.action.ReadValue<Vector3>();
        turnDirection = turn.action.ReadValue<Vector3>();

        Turn(turnDirection);
        CheckReverseHoldTurnaround(moveDirection.z);
    }

    private void FixedUpdate()
    {
        // Rotation is applied here (via the Rigidbody), not in Update, so it stays in
        // lockstep with the physics step instead of fighting the Rigidbody's own solver/interpolation.
        rb.MoveRotation(Quaternion.AngleAxis(yawAngle, Vector3.up) * flatRotation);
        Move(moveDirection);
    }

    // Right stick: x turns the bird left/right around world up, layered on top of its fixed flat tilt.
    private void Turn(Vector3 input)
    {
        yawAngle += input.x * yawMulitiplier * Time.deltaTime;
    }

    // Holding the left stick back for reverseHoldDuration seconds spins the bird around 180 degrees,
    // once per hold (won't fire again until the stick is released and pushed back a second time).
    private void CheckReverseHoldTurnaround(float forwardInput)
    {
        if (forwardInput < -reverseInputThreshold)
        {
            reverseHoldTimer += Time.deltaTime;

            if (reverseHoldTimer >= reverseHoldDuration && !hasTurnedAroundThisHold)
            {
                yawAngle += 180f;
                hasTurnedAroundThisHold = true;
            }
        }
        else
        {
            reverseHoldTimer = 0f;
            hasTurnedAroundThisHold = false;
        }
    }

    // Left stick forward/back moves the bird along the direction it's currently facing (no strafing);
    // triggers move it up/down. Velocity is smoothed toward the target instead of snapped to it.
    private void Move(Vector3 input)
    {
        Vector3 heading = Quaternion.AngleAxis(yawAngle, Vector3.up) * new Vector3(0f, 0f, input.z);
        Vector3 targetVelocity = (heading + Vector3.up * input.y) * thrust * thrustMultiplier;

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, accelSmoothing * Time.deltaTime);
    }

}

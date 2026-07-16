using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FlightController : MonoBehaviour
{

    [SerializeField] public float thrust;
    [SerializeField] public float thrustMultiplier;
    [SerializeField] public float yawMulitiplier;

    [Tooltip("How quickly speed changes catch up to the stick input. Higher = snappier, lower = more gliding/inertia.")]
    [SerializeField] private float accelSmoothing = 5f;

    [Header("Altitude")]
    [Tooltip("How high above its starting altitude the bird can climb.")]
    [SerializeField] private float maxClimbHeight = 400f;

    [Header("Bird Call")]
    [Tooltip("Sound played when the right shoulder button is pressed.")]
    [SerializeField] private AudioClip birdCallClip;
    [Tooltip("Volume multiplier for the bird call. Can go above 1 to boost it louder than the clip's natural level.")]
    [SerializeField] private float birdCallVolume = 2f;
    [Tooltip("Minimum time between bird calls, so it can't be spammed.")]
    [SerializeField] private float birdCallCooldown = 10f;
    [Tooltip("Delay between pressing the button and the call actually playing.")]
    [SerializeField] private float birdCallDelay = 2f;

    [SerializeField] private Vector3 moveDirection;
    private Vector3 turnDirection;

    // The bird's sprite is a flat quad tilted to lie on the ground plane (its saved rotation),
    // not a 3D model with a nose - so we never touch pitch/roll, only turn it around world up.
    private Quaternion flatRotation;
    private float yawAngle;
    private float startHeight;

    private float lastBirdCallTime = -Mathf.Infinity;

    // Built directly in code, same reasoning as PauseManager/FirstPersonToggle: BirdControls.inputactions
    // already has an unused "Bird Sound" action bound to the right shoulder, but referencing it from the
    // scene requires a fileID Unity computes on import, which isn't safe to hand-author.
    private InputAction birdCallAction;

    [SerializeField] Rigidbody rb;

    public InputActionReference fly;

    public InputActionReference turn;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        flatRotation = transform.rotation;
        yawAngle = 0f;
        startHeight = transform.position.y;

        birdCallAction = new InputAction("BirdCall", InputActionType.Button);
        birdCallAction.AddBinding("<Gamepad>/rightShoulder");
    }

    private void OnEnable()
    {
        fly.action.Enable();
        turn.action.Enable();

        birdCallAction.Enable();
        birdCallAction.performed += OnBirdCallPressed;
    }

    private void OnDisable()
    {
        fly.action.Disable();
        turn.action.Disable();

        birdCallAction.performed -= OnBirdCallPressed;
        birdCallAction.Disable();
    }

    // Gated by a cooldown (checked at press time, so spamming the button can't queue up multiple calls).
    // The call itself plays after a delay.
    private void OnBirdCallPressed(InputAction.CallbackContext context)
    {
        if (birdCallClip == null) return;
        if (Time.time - lastBirdCallTime < birdCallCooldown) return;

        lastBirdCallTime = Time.time;

        StartCoroutine(PlayBirdCallAfterDelay());
    }

    // Uses PlayClipAtPoint rather than a dedicated AudioSource, since this is a one-shot effect
    // with no need for a persistent source.
    private IEnumerator PlayBirdCallAfterDelay()
    {
        yield return new WaitForSeconds(birdCallDelay);
        AudioSource.PlayClipAtPoint(birdCallClip, transform.position, birdCallVolume);
    }

    private void Update()
    {
        moveDirection = fly.action.ReadValue<Vector3>();
        turnDirection = turn.action.ReadValue<Vector3>();

        Turn(turnDirection);
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

    // Left stick forward/back moves the bird along the direction it's currently facing (no strafing);
    // triggers move it up/down, capped so it can't climb indefinitely (it has no natural ceiling the
    // way the terrain gives it a natural floor). Velocity is smoothed toward the target, not snapped to it.
    // Backward stick input is a no-op - holding it back neither reverses nor turns the bird around.
    private void Move(Vector3 input)
    {
        Vector3 heading = Quaternion.AngleAxis(yawAngle, Vector3.up) * new Vector3(0f, 0f, Mathf.Max(0f, input.z));
        Vector3 targetVelocity = (heading + Vector3.up * input.y) * thrust * thrustMultiplier;

        if (transform.position.y >= startHeight + maxClimbHeight && targetVelocity.y > 0f)
            targetVelocity.y = 0f;

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, accelSmoothing * Time.deltaTime);
    }

}

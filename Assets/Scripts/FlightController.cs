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

    [Header("Reverse Hold Turnaround")]
    [Tooltip("How long the left stick must be held back before the bird auto-turns 180 degrees.")]
    [SerializeField] private float reverseHoldDuration = 0.75f;
    [Tooltip("How far back the stick must be pushed (0-1) to count as 'holding reverse'.")]
    [SerializeField] private float reverseInputThreshold = 0.5f;
    [Tooltip("Minimum time between automatic 180 turnarounds, so stick noise can't chain multiple flips back-to-back.")]
    [SerializeField] private float turnaroundCooldown = 15f;

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

    [Header("Background Music Muffle")]
    [Tooltip("The looping background music source - gets muffled briefly whenever the bird call plays.")]
    [SerializeField] private AudioSource backgroundMusicSource;
    [Tooltip("How muffled the music gets while the bird call plays (lower = more muffled).")]
    [SerializeField] private float muffledCutoffFrequency = 500f;
    [Tooltip("Cutoff frequency representing 'normal, unmuffled' music.")]
    [SerializeField] private float normalCutoffFrequency = 22000f;
    [Tooltip("How long the music stays muffled.")]
    [SerializeField] private float muffleDuration = 8f;

    private AudioLowPassFilter backgroundMusicFilter;

    [SerializeField] private Vector3 moveDirection;
    private Vector3 turnDirection;

    // The bird's sprite is a flat quad tilted to lie on the ground plane (its saved rotation),
    // not a 3D model with a nose - so we never touch pitch/roll, only turn it around world up.
    private Quaternion flatRotation;
    private float yawAngle;
    private float startHeight;

    private float reverseHoldTimer;
    private float lastTurnaroundTime = -Mathf.Infinity;
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

        if (backgroundMusicSource != null)
        {
            // Added at runtime rather than hand-placed in the scene, so it's guaranteed to exist
            // and start in a known (fully open, i.e. unmuffled) state.
            backgroundMusicFilter = backgroundMusicSource.GetComponent<AudioLowPassFilter>();
            if (backgroundMusicFilter == null)
                backgroundMusicFilter = backgroundMusicSource.gameObject.AddComponent<AudioLowPassFilter>();

            backgroundMusicFilter.cutoffFrequency = normalCutoffFrequency;
            backgroundMusicFilter.enabled = false;
        }
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
    // The call itself plays after a delay, and briefly muffles the background music.
    private void OnBirdCallPressed(InputAction.CallbackContext context)
    {
        if (birdCallClip == null) return;
        if (Time.time - lastBirdCallTime < birdCallCooldown) return;

        lastBirdCallTime = Time.time;

        StartCoroutine(PlayBirdCallAfterDelay());
        StartCoroutine(MuffleBackgroundMusic());
    }

    // Uses PlayClipAtPoint rather than a dedicated AudioSource, since this is a one-shot effect
    // with no need for a persistent source.
    private IEnumerator PlayBirdCallAfterDelay()
    {
        yield return new WaitForSeconds(birdCallDelay);
        AudioSource.PlayClipAtPoint(birdCallClip, transform.position, birdCallVolume);
    }

    private IEnumerator MuffleBackgroundMusic()
    {
        if (backgroundMusicFilter == null) yield break;

        backgroundMusicFilter.enabled = true;
        backgroundMusicFilter.cutoffFrequency = muffledCutoffFrequency;

        yield return new WaitForSeconds(muffleDuration);

        backgroundMusicFilter.cutoffFrequency = normalCutoffFrequency;
        backgroundMusicFilter.enabled = false;
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

    // Holding the left stick back for reverseHoldDuration seconds spins the bird around 180 degrees.
    // Gated by a hard cooldown (not just "stick released") so stick noise can't re-trigger it early.
    private void CheckReverseHoldTurnaround(float forwardInput)
    {
        if (forwardInput < -reverseInputThreshold)
        {
            reverseHoldTimer += Time.deltaTime;

            bool cooldownElapsed = Time.time - lastTurnaroundTime >= turnaroundCooldown;
            if (reverseHoldTimer >= reverseHoldDuration && cooldownElapsed)
            {
                yawAngle += 180f;
                lastTurnaroundTime = Time.time;
                reverseHoldTimer = 0f;
            }
        }
        else
        {
            reverseHoldTimer = 0f;
        }
    }

    // Left stick forward/back moves the bird along the direction it's currently facing (no strafing);
    // triggers move it up/down, capped so it can't climb indefinitely (it has no natural ceiling the
    // way the terrain gives it a natural floor). Velocity is smoothed toward the target, not snapped to it.
    private void Move(Vector3 input)
    {
        Vector3 heading = Quaternion.AngleAxis(yawAngle, Vector3.up) * new Vector3(0f, 0f, input.z);
        Vector3 targetVelocity = (heading + Vector3.up * input.y) * thrust * thrustMultiplier;

        if (transform.position.y >= startHeight + maxClimbHeight && targetVelocity.y > 0f)
            targetVelocity.y = 0f;

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, accelSmoothing * Time.deltaTime);
    }

}

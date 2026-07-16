using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FishButton { X, Y, A, B }

// Sits on a fish's non-animated "Catch Zone" anchor (never on the animated sprite objects
// themselves - their own swim/jump animation swings position and rotation by hundreds of
// units). The zone colliders and prompt icon are static, sized and centered to cover the
// entire area the animated fish swings through, so the bird triggers them regardless of
// where in its swim/jump cycle the fish currently is.
// Fully self-contained per fish: duplicate/copy-paste the whole anchor hierarchy anywhere in the
// scene (or drag a fresh prefab instance) to add another independent catch spot.
public class CatchableFish : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("All sprite pieces that make up this fish. Auto-resolved from children if left empty, so this works regardless of how many sprite layers exist or how they're nested.")]
    [SerializeField] private SpriteRenderer[] sprites;

    [Header("Catch Prompt")]
    [SerializeField] private SpriteRenderer promptIcon;
    [SerializeField] private Sprite xIcon;
    [SerializeField] private Sprite yIcon;
    [SerializeField] private Sprite aIcon;
    [SerializeField] private Sprite bIcon;
    [Tooltip("How long the player has to press the prompted button once inside the inner zone.")]
    [SerializeField] private float catchWindowSeconds = 3f;

    [Header("Respawn")]
    [Tooltip("How long the fish stays hidden and uncatchable after being caught. Set to 0 for no cooldown.")]
    [SerializeField] private float respawnCooldownSeconds = 0f;

    [Header("Zones")]
    [SerializeField] private Collider outerZoneCollider;
    [SerializeField] private Collider innerZoneCollider;

    private bool birdInOuter;
    private bool birdInInner;
    private bool catchWindowOpen;
    private bool onCooldown;
    private FishButton promptButton;
    private Coroutine catchWindowCoroutine;

    public static event Action OnFishCaught;

    private void Awake()
    {
        if (sprites == null || sprites.Length == 0)
        {
            SpriteRenderer[] found = GetComponentsInChildren<SpriteRenderer>(true);
            var fishSprites = new System.Collections.Generic.List<SpriteRenderer>(found.Length);

            // Excludes the prompt icon specifically - it's a sibling SpriteRenderer under the same
            // anchor, and would otherwise get its visibility fought over by the fish's own show/hide
            // rule instead of being controlled solely by StartCatchWindow/ResolveCatch.
            foreach (SpriteRenderer sr in found)
            {
                if (sr != promptIcon)
                    fishSprites.Add(sr);
            }

            sprites = fishSprites.ToArray();
        }

        if (promptIcon != null)
            promptIcon.enabled = false;

        RecomputeVisibility();
    }

    private void OnEnable()
    {
        FirstPersonToggle.OnFirstPersonChanged += HandleFirstPersonChanged;
    }

    private void OnDisable()
    {
        FirstPersonToggle.OnFirstPersonChanged -= HandleFirstPersonChanged;
    }

    private void HandleFirstPersonChanged(bool isFirstPerson)
    {
        RecomputeVisibility();
    }

    // Called by FishZoneRelay children - never fires unless the collider belongs to the bird.
    public void OnZoneEnter(FishZoneType zone, Collider other)
    {
        if (onCooldown) return;

        if (zone == FishZoneType.Outer)
        {
            birdInOuter = true;
            RecomputeVisibility();
        }
        else
        {
            birdInInner = true;
            StartCatchWindow();
        }
    }

    public void OnZoneExit(FishZoneType zone, Collider other)
    {
        if (zone == FishZoneType.Outer)
        {
            birdInOuter = false;
            RecomputeVisibility();
        }
        else
        {
            birdInInner = false;

            // Leaving the inner zone cancels an unresolved attempt - one attempt per entry,
            // not a continuous re-roll while lingering.
            if (catchWindowOpen)
                ResolveCatch(false);
        }
    }

    private void RecomputeVisibility()
    {
        bool visible = !onCooldown && (FirstPersonToggle.IsFirstPerson || birdInOuter);

        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite != null)
                sprite.enabled = visible;
        }
    }

    private void StartCatchWindow()
    {
        if (catchWindowOpen || onCooldown) return;

        promptButton = (FishButton)UnityEngine.Random.Range(0, 4);

        if (promptIcon != null)
        {
            promptIcon.sprite = promptButton switch
            {
                FishButton.X => xIcon,
                FishButton.Y => yIcon,
                FishButton.A => aIcon,
                _ => bIcon,
            };
            promptIcon.enabled = true;
        }

        catchWindowOpen = true;
        catchWindowCoroutine = StartCoroutine(CatchWindowTimeout());
    }

    private IEnumerator CatchWindowTimeout()
    {
        yield return new WaitForSeconds(catchWindowSeconds);

        if (catchWindowOpen)
            ResolveCatch(false);
    }

    private void Update()
    {
        if (!catchWindowOpen) return;

        if (promptIcon != null && FirstPersonToggle.ActiveCamera != null)
            promptIcon.transform.rotation = FirstPersonToggle.ActiveCamera.transform.rotation;

        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null) return;

        bool pressed = promptButton switch
        {
            FishButton.X => gamepad.buttonWest.wasPressedThisFrame,
            FishButton.Y => gamepad.buttonNorth.wasPressedThisFrame,
            FishButton.A => gamepad.buttonSouth.wasPressedThisFrame,
            FishButton.B => gamepad.buttonEast.wasPressedThisFrame,
            _ => false,
        };

        if (pressed)
            ResolveCatch(true);
    }

    private void ResolveCatch(bool success)
    {
        catchWindowOpen = false;

        if (catchWindowCoroutine != null)
        {
            StopCoroutine(catchWindowCoroutine);
            catchWindowCoroutine = null;
        }

        if (promptIcon != null)
            promptIcon.enabled = false;

        if (!success) return;

        OnFishCaught?.Invoke();

        onCooldown = true;
        birdInOuter = false;
        birdInInner = false;
        RecomputeVisibility();

        if (outerZoneCollider != null) outerZoneCollider.enabled = false;
        if (innerZoneCollider != null) innerZoneCollider.enabled = false;

        StartCoroutine(RespawnAfterDelay());
    }

    // Runs on this anchor's own always-active GameObject - only child sprites/colliders get
    // disabled while caught, never this GameObject itself, since a disabled GameObject stops
    // its own coroutines and this timer would never complete.
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnCooldownSeconds);

        onCooldown = false;

        if (outerZoneCollider != null) outerZoneCollider.enabled = true;
        if (innerZoneCollider != null) innerZoneCollider.enabled = true;

        RecomputeVisibility();
    }
}

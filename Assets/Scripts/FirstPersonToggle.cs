using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonToggle : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera thirdPersonCamera;
    [SerializeField] private Camera firstPersonCamera;

    [Tooltip("Optional - if set, L1 is ignored while the game is paused, and Resume/Pause will know to restore whichever of these two views was active.")]
    [SerializeField] private PauseManager pauseManager;

    [Tooltip("Only visible while first-person view is active.")]
    [SerializeField] private GameObject fpvMagnetismSphere;

    // Built directly in code, same reasoning as PauseManager's Pause action: BirdControls.inputactions
    // already has an unused "First Person View" action bound to the left shoulder, but referencing it
    // from the scene requires a fileID Unity computes on import, which isn't safe to hand-author.
    private InputAction toggleAction;
    private bool isFirstPerson;

    // Static so other scripts (e.g. CatchableFish) can react to camera-mode changes without polling
    // every frame, and can billboard toward whichever camera is actually active - Camera.main isn't
    // reliable here since only one camera in the scene is tagged MainCamera and it isn't always the
    // one currently in use (third/first person are two separate Camera components toggled via .enabled).
    public static event Action<bool> OnFirstPersonChanged;
    public static bool IsFirstPerson { get; private set; }
    public static Camera ActiveCamera { get; private set; }

    private void Awake()
    {
        toggleAction = new InputAction("FirstPersonView", InputActionType.Button);
        toggleAction.AddBinding("<Gamepad>/leftShoulder");

        if (fpvMagnetismSphere != null)
            fpvMagnetismSphere.SetActive(false);

        ActiveCamera = thirdPersonCamera;
    }

    private void OnEnable()
    {
        toggleAction.Enable();
        toggleAction.performed += OnTogglePressed;
    }

    private void OnDisable()
    {
        toggleAction.performed -= OnTogglePressed;
        toggleAction.Disable();
    }

    private void OnTogglePressed(InputAction.CallbackContext context)
    {
        if (pauseManager != null && pauseManager.IsPaused)
            return;

        isFirstPerson = !isFirstPerson;

        thirdPersonCamera.enabled = !isFirstPerson;
        firstPersonCamera.enabled = isFirstPerson;

        if (fpvMagnetismSphere != null)
            fpvMagnetismSphere.SetActive(isFirstPerson);

        if (pauseManager != null)
            pauseManager.SetActiveGameplayCamera(isFirstPerson ? firstPersonCamera : thirdPersonCamera);

        IsFirstPerson = isFirstPerson;
        ActiveCamera = isFirstPerson ? firstPersonCamera : thirdPersonCamera;
        OnFirstPersonChanged?.Invoke(isFirstPerson);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonToggle : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera thirdPersonCamera;
    [SerializeField] private Camera firstPersonCamera;

    [Tooltip("Optional - if set, L1 is ignored while the game is paused, and Resume/Pause will know to restore whichever of these two views was active.")]
    [SerializeField] private PauseManager pauseManager;

    // Built directly in code, same reasoning as PauseManager's Pause action: BirdControls.inputactions
    // already has an unused "First Person View" action bound to the left shoulder, but referencing it
    // from the scene requires a fileID Unity computes on import, which isn't safe to hand-author.
    private InputAction toggleAction;
    private bool isFirstPerson;

    private void Awake()
    {
        toggleAction = new InputAction("FirstPersonView", InputActionType.Button);
        toggleAction.AddBinding("<Gamepad>/leftShoulder");
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

        if (pauseManager != null)
            pauseManager.SetActiveGameplayCamera(isFirstPerson ? firstPersonCamera : thirdPersonCamera);
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("The default gameplay camera (third-person chase view).")]
    [SerializeField] private Camera gameplayCamera;
    [Tooltip("The static top-down camera shown while paused.")]
    [SerializeField] private Camera pauseCamera;

    // Whichever gameplay camera was actually active when we paused (third-person or first-person) -
    // other scripts that switch cameras (e.g. FirstPersonToggle) should call SetActiveGameplayCamera
    // so Resume() puts the player back where they were, not always back to third-person.
    private Camera activeGameplayCamera;

    [Header("Pause Camera Framing")]
    [Tooltip("The bird to look down on while paused.")]
    [SerializeField] private Transform bird;
    [Tooltip("How high above the bird the pause camera sits (roughly 50-75).")]
    [SerializeField] private float heightAboveBird = 65f;
    [Tooltip("Shifts the camera's look point to the right of the bird (world X), so the bird lands left-of-center on screen - in the middle of the left half rather than dead center. Tune this in Play mode while paused until it looks right for your aspect ratio.")]
    [SerializeField] private float horizontalOffset = 50f;

    [Header("Pause Menu UI")]
    [Tooltip("The Canvas (or root UI object) shown only while paused.")]
    [SerializeField] private GameObject pauseMenuUI;

    // Built directly in code (not via the BirdControls asset) so this works regardless of
    // whatever gameplay action maps exist/are enabled - Start should always be able to pause.
    private InputAction pauseAction;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        pauseAction = new InputAction("Pause", InputActionType.Button);
        pauseAction.AddBinding("<Gamepad>/start");
        pauseAction.AddBinding("<Keyboard>/escape");

        activeGameplayCamera = gameplayCamera;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
    }

    // Call this whenever something else (e.g. FirstPersonToggle) switches which camera is showing
    // gameplay, so pausing/resuming restores the right one instead of assuming third-person.
    public void SetActiveGameplayCamera(Camera camera)
    {
        activeGameplayCamera = camera;
    }

    private void OnEnable()
    {
        pauseAction.Enable();
        pauseAction.performed += OnPausePressed;
    }

    private void OnDisable()
    {
        pauseAction.performed -= OnPausePressed;
        pauseAction.Disable();
    }

    private void OnPausePressed(InputAction.CallbackContext context)
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    // Public so the existing UI Pause/Play buttons can call these directly via OnClick.
    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        pauseCamera.transform.position = bird.position + Vector3.up * heightAboveBird + Vector3.right * horizontalOffset;
        pauseCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        activeGameplayCamera.enabled = false;
        pauseCamera.enabled = true;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;

        pauseCamera.enabled = false;
        activeGameplayCamera.enabled = true;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
    }

    // Wired to the pause menu's Reset button. Time.timeScale doesn't reset itself on scene load,
    // so this restores it - otherwise the reloaded scene would come back frozen.
    public void ResetGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Wired to the pause menu's Exit button. Application.Quit() is a no-op in the Editor,
    // so we stop Play Mode directly there instead.
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

using UnityEngine;
using UnityEngine.UI;

// Owns the always-visible top-right HUD: total fish caught and elapsed play time.
// Time is driven by Time.deltaTime, so it naturally stops counting while paused
// (Time.timeScale is 0 during pause, same as everywhere else in this project).
public class FishCatchHUD : MonoBehaviour
{
    [SerializeField] private Text fishCountText;
    [SerializeField] private Text timeText;

    private int fishCaught;
    private float elapsedTime;
    private float nextTimeUpdate;

    public static FishCatchHUD Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        UpdateFishCountText();
        UpdateTimeText();
    }

    private void OnEnable()
    {
        CatchableFish.OnFishCaught += HandleFishCaught;
    }

    private void OnDisable()
    {
        CatchableFish.OnFishCaught -= HandleFishCaught;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // Throttled to once per second - reformatting/assigning Text.text every frame would
        // needlessly allocate a new string 60+ times a second for no visible benefit.
        if (Time.time >= nextTimeUpdate)
        {
            nextTimeUpdate = Time.time + 1f;
            UpdateTimeText();
        }
    }

    private void HandleFishCaught()
    {
        fishCaught++;
        UpdateFishCountText();
    }

    private void UpdateFishCountText()
    {
        if (fishCountText != null)
            fishCountText.text = fishCaught.ToString();
    }

    private void UpdateTimeText()
    {
        if (timeText == null) return;

        int totalSeconds = Mathf.FloorToInt(elapsedTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        timeText.text = $"{minutes:00}:{seconds:00}";
    }
}

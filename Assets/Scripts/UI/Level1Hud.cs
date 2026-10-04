using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD Level 1. HP pemain sengaja tetap penuh sampai sistem damage pemain dibuat.
/// Camera gestures drive Level 1 after the menu calibration; direct scene
/// testing without a receiver keeps a mouse-click fallback.
/// </summary>
[DisallowMultipleComponent]
public class Level1Hud : MonoBehaviour
{
    [SerializeField] private Level1Manager manager;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Image playerFill;
    [SerializeField] private TMP_Text playerValue;
    [SerializeField, Min(1)] private int playerMaxHealth = 5;

    [SerializeField] private GameObject enemyBarRoot;
    [SerializeField] private Image enemyFill;
    [SerializeField] private TMP_Text enemyName;
    [SerializeField] private TMP_Text enemyValue;

    [SerializeField] private GameObject squeezePrompt;
    [SerializeField] private TMP_Text squeezeText;

    [Header("Hand Camera")]
    [SerializeField] private bool hideHandCamera;

    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;

    private bool isPaused;
    private float previousTimeScale = 1f;
    private Level1Audio levelAudio;
    private HandCameraPreview handCameraPreview;

    public bool IsPaused => isPaused;

    public bool IsPointerOverControl
    {
        get
        {
            if (pausePanel != null && pausePanel.activeSelf)
                return true;

            if (pauseButton == null)
                return false;

            RectTransform rect = pauseButton.GetComponent<RectTransform>();
            Canvas canvas = pauseButton.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                rect, Input.mousePosition, camera);
        }
    }

    private void Awake()
    {
        if (manager == null)
            manager = GetComponent<Level1Manager>();
        levelAudio = GetComponentInChildren<Level1Audio>();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (hudRoot != null)
            handCameraPreview = new HandCameraPreview(hudRoot.transform,
                squeezeText != null ? squeezeText.font : null,
                new Vector2(-28, -184));
    }

    private void OnEnable()
    {
        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);
    }

    private void OnDisable()
    {
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(TogglePause);
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(Resume);
        Resume();
        AudioListener.pause = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();

        if (manager == null)
            return;

        bool showHud = !manager.IsLevelComplete;
        if (hudRoot != null && hudRoot.activeSelf != showHud)
            hudRoot.SetActive(showHud);
        if (!showHud)
            return;

        if (handCameraPreview != null)
        {
            UDPReceiver receiver = UDPReceiver.Instance;
            bool showCamera = !hideHandCamera &&
                (receiver != null || manager.CameraSessionExpected);
            handCameraPreview.SetVisible(showCamera);
            if (showCamera) handCameraPreview.Refresh(receiver);
        }

        // Level 1 belum menerapkan damage pemain, jadi bar ini selalu penuh.
        if (playerFill != null)
            SetBarFill(playerFill, 1f);
        if (playerValue != null)
            playerValue.text = playerMaxHealth + " / " + playerMaxHealth;

        EnemyHealth enemy = manager.ActiveEnemy;
        bool hasEnemy = enemy != null && !enemy.IsDead;
        if (enemyBarRoot != null && enemyBarRoot.activeSelf != hasEnemy)
            enemyBarRoot.SetActive(hasEnemy);
        if (hasEnemy)
        {
            int max = Mathf.Max(1, enemy.MaxHealth);
            int current = Mathf.Clamp(enemy.CurrentHealth, 0, max);
            if (enemyFill != null)
                SetBarFill(enemyFill, (float)current / max);
            if (enemyValue != null)
                enemyValue.text = current + " / " + max;
            if (enemyName != null)
                enemyName.text = GetEnemyName(enemy);
        }

        bool showPrompt = manager.sedangIstirahat ||
            (manager.siapMenyerang && hasEnemy);
        if (squeezePrompt != null && squeezePrompt.activeSelf != showPrompt)
            squeezePrompt.SetActive(showPrompt);
        if (showPrompt && squeezeText != null)
            squeezeText.text = GetActionPrompt();
    }

    private static void SetBarFill(Image fill, float amount)
    {
        // The HUD uses plain-color Images without sprites. Unity ignores
        // Image.fillAmount for sprite-less Images, so resize the UI rect.
        float fraction = Mathf.Clamp01(amount);
        if (fill.type != Image.Type.Simple)
            fill.type = Image.Type.Simple;

        RectTransform rect = fill.rectTransform;
        if (rect.anchorMax.x != fraction)
            rect.anchorMax = new Vector2(fraction, rect.anchorMax.y);
        fill.enabled = fraction > 0f;
    }

    private string GetActionPrompt()
    {
        if (manager.IsAttacking)
            return "ATTACK IN PROGRESS";

        UDPReceiver receiver = UDPReceiver.Instance;
        if (receiver == null)
        {
            if (manager.CameraSessionExpected)
                return "CAMERA DISCONNECTED";
            return manager.sedangIstirahat
                ? "CLICK TO CONTINUE (TEST)" : "CLICK TO ATTACK (TEST)";
        }
        if (receiver.ConnectionError != null || !receiver.HasFreshData)
            return "CAMERA DISCONNECTED";
        if (!receiver.IsCalibrationCompleted)
            return "CALIBRATION REQUIRED";
        if (receiver.IsGripStrongEnough())
            return "OPEN HAND TO RESET";
        if (receiver.CurrentPrediction == "fist")
            return "MAKE A CLEAR FIST";
        if (!receiver.IsPalm)
            return "SHOW OPEN HAND";
        return manager.sedangIstirahat
            ? "SQUEEZE TO CONTINUE" : "SQUEEZE TO ATTACK";
    }

    private string GetEnemyName(EnemyHealth enemy)
    {
        if (enemy == manager.enemyVampireArena3)
            return "VAMPIRE";
        if (enemy == manager.enemy2A)
            return "SKELETON 1 / 2";
        if (enemy == manager.enemy2B)
            return "SKELETON 2 / 2";
        return "SKELETON";
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            Resume();
            return;
        }

        if (manager != null && manager.IsLevelComplete)
            return;

        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        levelAudio?.PlayUiClick();
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (!isPaused)
            return;

        isPaused = false;
        Time.timeScale = previousTimeScale;
        AudioListener.pause = false;
        levelAudio?.PlayUiClick();
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }
}

using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum ControlMode { PlayerShoots, PlayerDefends }

    public BallController ball;
    public GoalkeeperAnimatorBase keeper;
    public PenaltyKickInput input;
    public KickerAnimatorBase kicker;
    public GoalkeeperInput goalkeeperInput;
    public CameraEffects cameraEffects;
    public ParticleSystem confetti;
    public GameAudio gameAudio;

    public Camera kickerCamera;
    public AudioListener kickerListener;
    public Camera goalkeeperCamera;
    public AudioListener goalkeeperListener;
    public CameraEffects goalkeeperCameraEffects;

    public Transform goalCenterPoint;
    public float aiGoalHalfWidth = 3.0f;
    public float aiGoalHeight = 2.2f;

    [Range(0f, 1f)]
    public float keeperDifficulty = 0.5f;

    // HappyGoal's real shootout alternates who shoots and who defends each
    // turn - this prototype only has the two roles built, not the turn
    // sequencing itself (that will come from Flutter), so a key swaps
    // between them here for testing both sides.
    public ControlMode mode = ControlMode.PlayerShoots;
    public KeyCode toggleModeKey = KeyCode.G;

    int goals;
    int attempts;

    void OnEnable()
    {
        ball.OnShotFired += HandleShotFired;
        ball.OnGoalScored += HandleGoal;
        ball.OnShotBlocked += HandleBlocked;
        ball.OnShotMissed += HandleMissed;
    }

    void OnDisable()
    {
        ball.OnShotFired -= HandleShotFired;
        ball.OnGoalScored -= HandleGoal;
        ball.OnShotBlocked -= HandleBlocked;
        ball.OnShotMissed -= HandleMissed;
    }

    void Start()
    {
        NextAttempt();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            mode = mode == ControlMode.PlayerShoots ? ControlMode.PlayerDefends : ControlMode.PlayerShoots;
            Debug.Log("Mode: " + mode);
            ApplyCameraForMode();
            StopAllCoroutines();
            if (goalkeeperInput != null) goalkeeperInput.CancelWindow();
            NextAttempt();
        }
    }

    void ApplyCameraForMode()
    {
        bool defending = mode == ControlMode.PlayerDefends;
        if (kickerCamera != null) kickerCamera.enabled = !defending;
        if (kickerListener != null) kickerListener.enabled = !defending;
        if (goalkeeperCamera != null) goalkeeperCamera.enabled = defending;
        if (goalkeeperListener != null) goalkeeperListener.enabled = defending;
    }

    CameraEffects ActiveCameraEffects => mode == ControlMode.PlayerDefends ? goalkeeperCameraEffects : cameraEffects;

    void HandleShotFired(Vector3 targetPoint)
    {
        if (gameAudio != null) gameAudio.PlayKick();

        if (mode == ControlMode.PlayerShoots)
        {
            bool saved = keeper.ReactToShot(targetPoint.x, keeperDifficulty);
            if (saved) ball.ScheduleBlock();
        }
        // In PlayerDefends mode the reaction window is opened earlier, right
        // as the kick animation starts (see ApproachThenAiShoot) - by the
        // time the ball actually launches there isn't enough flight time
        // left before it reaches the keeper for a fresh reaction to matter.
    }

    void HandleGoal()
    {
        goals++;
        Debug.Log($"BUT ! Score: {goals}/{attempts}");

        if (gameAudio != null) gameAudio.PlayGoal();
        if (ActiveCameraEffects != null) ActiveCameraEffects.PunchZoom();
        if (confetti != null) confetti.Play();
        if (mode == ControlMode.PlayerShoots && kicker != null) StartCoroutine(kicker.PlayCelebration());

        Invoke(nameof(NextAttempt), 2f);
    }

    void HandleBlocked()
    {
        Debug.Log($"Arrete par le gardien. Score: {goals}/{attempts}");
        if (gameAudio != null) gameAudio.PlaySave();
        if (ActiveCameraEffects != null) ActiveCameraEffects.Shake();
        Invoke(nameof(NextAttempt), 1.5f);
    }

    void HandleMissed()
    {
        Debug.Log($"Tir manque. Score: {goals}/{attempts}");
        Invoke(nameof(NextAttempt), 1.5f);
    }

    void NextAttempt()
    {
        attempts++;
        ball.ResetBall();
        keeper.ResetKeeper();
        input.InputEnabled = false;

        if (kicker == null)
        {
            input.InputEnabled = true;
            return;
        }

        kicker.ResetToStart();
        StartCoroutine(mode == ControlMode.PlayerShoots ? ApproachThenEnableInput() : ApproachThenAiShoot());
    }

    IEnumerator ApproachThenEnableInput()
    {
        yield return kicker.ApproachRoutine();
        input.InputEnabled = true;
    }

    IEnumerator ApproachThenAiShoot()
    {
        yield return kicker.ApproachRoutine();
        yield return new WaitForSeconds(0.3f);

        float targetX = Random.Range(-aiGoalHalfWidth, aiGoalHalfWidth);
        float targetY = Mathf.Lerp(0.2f, aiGoalHeight * 0.9f, Random.Range(0.15f, 0.95f));
        Vector3 center = goalCenterPoint != null ? goalCenterPoint.position : ball.transform.position;
        Vector3 targetPoint = new Vector3(center.x + targetX, targetY, center.z);

        float power = Random.Range(0.75f, 1.05f);
        float curve = Random.Range(-0.35f, 0.35f);

        // Open the goalkeeper's reaction window now, before the ball even
        // launches, so the player gets the strike animation's wind-up as
        // extra lead time on top of the ball's own flight time.
        if (mode == ControlMode.PlayerDefends && goalkeeperInput != null)
        {
            goalkeeperInput.BeginReactionWindow(targetPoint.x);
        }

        yield return kicker.PlayKick(() => ball.Shoot(targetPoint, power, curve));
    }
}

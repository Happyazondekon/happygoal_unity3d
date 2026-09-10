using UnityEngine;

// The human-controlled defensive half of HappyGoal's turn-by-turn penalty
// shootout: while GameManager is in PlayerDefends mode, this opens a
// reaction window the moment the AI kicker's shot is fired. A single swipe
// (or, more reliably in the Editor where mouse focus can be flaky, the
// left/right arrow keys) picks the dive; nothing before the window closes
// leaves the keeper rooted in the middle.
//
// The goalkeeper camera faces the kicker, i.e. the opposite way round from
// the kicker camera - so screen-right for this camera is world -X, not +X.
// Every direction below is flipped to account for that.
public class GoalkeeperInput : MonoBehaviour
{
    public BallController ball;
    public GoalkeeperAnimatorBase keeper;

    // When set (real match, via MatchController), the dive direction chosen
    // here is reported to MatchController (ShotResolver decides the save,
    // not the radius check below) instead of being resolved locally. See
    // Resolve().
    public GameManager gameManager;
    public MatchController matchController;

    // Must resolve before the fastest possible AI shot reaches the goal
    // line, or the ball scores before a late input can ever call
    // ScheduleBlock() - see the comment in GameManager.ApproachThenAiShoot.
    public float reactionWindow = 1.0f;
    public float maxDiveX = 3.0f;
    public float saveRadius = 0.6f;
    public float minSwipePixels = 30f;
    public float swipePixelsForFullReach = 220f;

    bool windowOpen;
    bool resolved;
    float windowTimer;
    float pendingTargetX;
    Vector2 dragStart;
    bool dragging;

    public void BeginReactionWindow(float shotTargetX)
    {
        pendingTargetX = shotTargetX;
        windowOpen = true;
        resolved = false;
        windowTimer = 0f;
        dragging = false;
    }

    public void CancelWindow()
    {
        windowOpen = false;
        dragging = false;
    }

    void Update()
    {
        if (!windowOpen) return;

        windowTimer += Time.deltaTime;

        // Reliable fallback / primary control for Editor testing - mouse
        // focus in the Game view can be flaky, keys never are.
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            Resolve(maxDiveX, "key-left");
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            Resolve(-maxDiveX, "key-right");
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            Resolve(0f, "key-center");
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragStart = Input.mousePosition;
            dragging = true;
        }
        else if (Input.GetMouseButtonUp(0) && dragging)
        {
            dragging = false;
            float dx = ((Vector2)Input.mousePosition - dragStart).x;
            if (Mathf.Abs(dx) >= minSwipePixels)
            {
                float normalized = Mathf.Clamp(dx / swipePixelsForFullReach, -1f, 1f);
                Resolve(-normalized * maxDiveX, "swipe (" + dx + "px)");
            }
            else
            {
                Debug.Log($"Goalkeeper: swipe too short ({dx}px, needs {minSwipePixels}px) - ignored, still waiting.");
            }
        }

        if (windowOpen && windowTimer > reactionWindow)
        {
            // Too slow - the keeper stays rooted in the middle.
            Resolve(0f, "timeout");
        }
    }

    void Resolve(float chosenX, string reason)
    {
        if (resolved) return;
        resolved = true;
        windowOpen = false;
        dragging = false;

        if (keeper != null) keeper.PlayDirectedDive(chosenX);

        if (gameManager != null && gameManager.flutterControlled)
        {
            int diveZone = chosenX < -maxDiveX * 0.3f ? ShotDirection.Left
                : (chosenX > maxDiveX * 0.3f ? ShotDirection.Right : ShotDirection.Center);
            if (matchController != null) matchController.ReportGoalkeeperChoice(diveZone);
            return;
        }

        bool saved = Mathf.Abs(chosenX - pendingTargetX) <= saveRadius;
        Debug.Log($"Goalkeeper dove to X={chosenX:F2} ({reason}) - shot was at X={pendingTargetX:F2} - {(saved ? "SAVED" : "not saved")}");
        if (saved && ball != null) ball.ScheduleBlock();
    }
}

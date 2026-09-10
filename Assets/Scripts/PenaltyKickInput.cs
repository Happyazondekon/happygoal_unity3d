using System.Collections.Generic;
using UnityEngine;

public class PenaltyKickInput : MonoBehaviour
{
    public BallController ball;
    public KickerAnimatorBase kicker;
    public Transform goalCenterPoint;

    // Live white aim line shown while dragging - see UpdateAimPreview/
    // DrawAimLine. Built by PrototypeSceneBuilder, disabled by default.
    public LineRenderer aimLine;

    // When set (real match, via MatchController.BeginPlayerShotWindow), a
    // captured swipe is reported to MatchController instead of shooting
    // directly - MatchController (via ShotResolver) is what decides the
    // goal/save outcome, this only captures the player's aim/power/effect.
    // See HandleSwipe below.
    public GameManager gameManager;
    public MatchController matchController;

    public float goalHalfWidth = 3.4f;
    public float goalHeight = 2.2f;
    public float minSwipePixels = 40f;
    public float maxSwipePixels = 650f;

    public float groundLevelY = -0.15f;
    // How close to the actual crossbar a full "aim high" swipe reaches by
    // default - just under it, so aiming for the top corner is reliable
    // rather than sailing over on its own.
    public float maxAimHeightFraction = 0.94f;

    // Only past this fraction of max swipe length does extra power start
    // costing accuracy - sideways drift plus an upward lift, so a normal
    // confident swipe stays accurate and only a truly maxed-out one risks
    // going wide or over, same trade-off as any football game's "hold too
    // long and you blaze it over" penalty.
    public float overpowerThreshold = 0.88f;
    public float overpowerDrift = 0.5f;
    public float overpowerLift = 0.3f;

    // ---- Gesture-based effect detection: the swipe's shape decides the
    // effect now, instead of a button picked before the swipe (see the plan
    // discussion) - curl the path for Curve, swipe hard and nearly straight
    // for Knuckle, swipe mostly straight up for Lob, anything else Normal.
    // Curve is checked first (an obviously curled swipe wins over a fast
    // one), a real playtest pass will likely want to retune these.
    public float curveDetectionThreshold = 0.35f;
    public float knucklePowerThreshold = 0.85f;
    public float knuckleMaxCurviness = 0.1f;
    public float lobVerticalRatio = 2.2f;

    public bool InputEnabled = true;

    Vector2 dragStart;
    bool dragging;
    readonly List<Vector2> _pathPoints = new List<Vector2>();

    // Everything the final shot needs, computed from a swipe delta - shared
    // by the live aim-line preview (every frame while dragging) and the
    // actual release, so the line the player sees truly is where it goes.
    struct Aim
    {
        public Vector3 targetPoint;
        public float power01;
        public float curviness; // signed, roughly -1..1
        public float targetX;
    }

    void Update()
    {
        if (!InputEnabled)
        {
            if (aimLine != null) aimLine.enabled = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            dragStart = Input.mousePosition;
            dragging = true;
            _pathPoints.Clear();
            _pathPoints.Add(dragStart);
        }
        else if (Input.GetMouseButton(0) && dragging)
        {
            Vector2 current = Input.mousePosition;
            // A wider dedup distance than a first pass used (was 2px) - real
            // touch input is jitterier than a desktop mouse, and closely-
            // spaced samples were feeding single-frame noise into
            // SignedCurviness, misreading plain diagonal aims as curved.
            if (_pathPoints.Count == 0 || Vector2.Distance(_pathPoints[_pathPoints.Count - 1], current) > 6f)
            {
                _pathPoints.Add(current);
            }
            UpdateAimPreview(current);
        }
        else if (Input.GetMouseButtonUp(0) && dragging)
        {
            dragging = false;
            if (aimLine != null) aimLine.enabled = false;
            Vector2 delta = (Vector2)Input.mousePosition - dragStart;
            HandleSwipe(delta);
        }
    }

    void UpdateAimPreview(Vector2 current)
    {
        if (aimLine == null || ball == null) return;

        Vector2 delta = current - dragStart;
        if (delta.magnitude < minSwipePixels)
        {
            aimLine.enabled = false;
            return;
        }

        var aim = ComputeAim(delta);
        DrawAimLine(aim.targetPoint, aim.curviness);
    }

    void DrawAimLine(Vector3 targetPoint, float curviness)
    {
        aimLine.enabled = true;
        int segments = Mathf.Max(aimLine.positionCount, 2);
        Vector3 start = ball.transform.position;

        // Bows the line sideways in the middle, proportional to how curled
        // the swipe currently is - a straight swipe gets a straight line, a
        // curled one visibly arcs, matching what the shot will actually do.
        Vector3 flat = targetPoint - start;
        Vector3 side = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
        Vector3 bow = side * curviness * 1.4f;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 point = Vector3.Lerp(start, targetPoint, t) + bow * Mathf.Sin(t * Mathf.PI);
            aimLine.SetPosition(i, point);
        }
    }

    Aim ComputeAim(Vector2 delta)
    {
        float length = delta.magnitude;
        float clampedLength = Mathf.Clamp(length, minSwipePixels, maxSwipePixels);
        float power01 = Mathf.InverseLerp(minSwipePixels, maxSwipePixels, clampedLength);

        float normalizedX = Mathf.Clamp(delta.x / maxSwipePixels, -1f, 1f);
        float normalizedY = Mathf.Clamp(delta.y / maxSwipePixels, -1f, 1f);

        // Precise aim lands exactly where aimed - free-aim anywhere across
        // the goal width/height, not one of a few fixed zones.
        float targetX = normalizedX * goalHalfWidth;
        float heightT = (normalizedY + 1f) / 2f;
        float easedHeightT = Mathf.Pow(heightT, 1.8f);
        float targetY = Mathf.Lerp(groundLevelY, goalHeight * maxAimHeightFraction, easedHeightT);

        // Only a maxed-out swipe risks losing control: sideways drift plus
        // an upward lift that can genuinely put it wide or over.
        float overpower = Mathf.Clamp01((power01 - overpowerThreshold) / (1f - overpowerThreshold));
        if (overpower > 0f)
        {
            targetX += Random.Range(-1f, 1f) * overpower * overpowerDrift;
            targetY += overpower * overpowerLift;
        }

        Vector3 center = goalCenterPoint != null ? goalCenterPoint.position : Vector3.zero;
        Vector3 targetPoint = new Vector3(center.x + targetX, targetY, center.z);

        float curviness = SignedCurviness(dragStart, dragStart + delta);

        return new Aim { targetPoint = targetPoint, power01 = power01, curviness = curviness, targetX = targetX };
    }

    // How far the actual finger path bowed away from a straight line
    // between the swipe's start and end, signed by which side it bowed to,
    // roughly clamped to -1..1. Needs a handful of sampled points
    // (_pathPoints) to mean anything - a straight fast flick naturally stays
    // near 0.
    //
    // Averages the middle half of the sampled points rather than taking the
    // single biggest deviation - a first pass used the max, which let one
    // noisy touch sample (touchscreens are jitterier than a desktop mouse)
    // spike the whole reading and misclassify an ordinary diagonal aim (e.g.
    // toward a top corner) as a curled swipe. A deliberately curled gesture
    // still bows consistently across most of the path, so it still scores
    // high under an average; a single jitter spike gets diluted out. The
    // first/last quarter of points (most likely to catch start/release
    // jitter) are dropped entirely.
    float SignedCurviness(Vector2 start, Vector2 end)
    {
        float straightDist = Vector2.Distance(start, end);
        if (straightDist < 1f || _pathPoints.Count < 5) return 0f;

        Vector2 dir = (end - start).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);

        int from = _pathPoints.Count / 4;
        int to = _pathPoints.Count - _pathPoints.Count / 4;
        if (to <= from) { from = 0; to = _pathPoints.Count; }

        float sum = 0f;
        int count = 0;
        for (int i = from; i < to; i++)
        {
            sum += Vector2.Dot(_pathPoints[i] - start, normal);
            count++;
        }
        float averageSigned = count > 0 ? sum / count : 0f;
        return Mathf.Clamp(averageSigned / (straightDist * 0.5f), -1f, 1f);
    }

    string DetectEffect(Vector2 start, Vector2 end, float power01, float curviness)
    {
        float absCurviness = Mathf.Abs(curviness);
        if (absCurviness > curveDetectionThreshold) return ShotEffect.Curve;

        if (power01 > knucklePowerThreshold && absCurviness < knuckleMaxCurviness) return ShotEffect.Knuckle;

        Vector2 delta = end - start;
        float verticalRatio = Mathf.Abs(delta.y) / Mathf.Max(Mathf.Abs(delta.x), 1f);
        if (verticalRatio > lobVerticalRatio) return ShotEffect.Lob;

        return ShotEffect.Normal;
    }

    void HandleSwipe(Vector2 delta)
    {
        float length = delta.magnitude;
        if (length < minSwipePixels) return;

        var aim = ComputeAim(delta);
        string effect = DetectEffect(dragStart, dragStart + delta, aim.power01, aim.curviness);
        int power100 = Mathf.Clamp(Mathf.RoundToInt(aim.power01 * 100f), 5, 100);

        // Real match: don't decide anything here - MatchController (via
        // ShotResolver) owns the goal/save outcome. Report the exact
        // continuous aim point and detected effect it computed here, and
        // MatchController plays out the actual kick once it has decided the
        // result - using this same targetX, so the shot lands exactly where
        // the aim line showed.
        if (gameManager != null && gameManager.flutterControlled)
        {
            InputEnabled = false;
            if (matchController != null) matchController.ReportShotAttempt(aim.targetX, power100, effect);
            return;
        }

        float shotPower = 0.55f + aim.power01 * 0.65f;
        float curveForBall = aim.curviness * 0.6f;

        InputEnabled = false;
        if (kicker != null)
        {
            StartCoroutine(kicker.PlayKick(() => ball.Shoot(aim.targetPoint, shotPower, curveForBall)));
        }
        else
        {
            ball.Shoot(aim.targetPoint, shotPower, curveForBall);
        }
    }
}

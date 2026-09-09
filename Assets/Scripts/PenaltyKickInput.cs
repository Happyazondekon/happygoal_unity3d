using UnityEngine;

public class PenaltyKickInput : MonoBehaviour
{
    public BallController ball;
    public KickerAnimatorBase kicker;
    public Transform goalCenterPoint;

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

    public bool InputEnabled = true;

    Vector2 dragStart;
    bool dragging;

    void Update()
    {
        if (!InputEnabled) return;

        if (Input.GetMouseButtonDown(0))
        {
            dragStart = Input.mousePosition;
            dragging = true;
        }
        else if (Input.GetMouseButtonUp(0) && dragging)
        {
            dragging = false;
            Vector2 delta = (Vector2)Input.mousePosition - dragStart;
            HandleSwipe(delta);
        }
    }

    void HandleSwipe(Vector2 delta)
    {
        float length = delta.magnitude;
        if (length < minSwipePixels) return;

        float clampedLength = Mathf.Clamp(length, minSwipePixels, maxSwipePixels);
        float power01 = Mathf.InverseLerp(minSwipePixels, maxSwipePixels, clampedLength);

        float normalizedX = Mathf.Clamp(delta.x / maxSwipePixels, -1f, 1f);
        // -1 (swipe down) aims along the ground, +1 (swipe up) aims at the
        // crossbar - power still comes from the swipe's full length above,
        // independent of this direction, so a low shot can still be hit hard.
        float normalizedY = Mathf.Clamp(delta.y / maxSwipePixels, -1f, 1f);

        // Precise aim lands exactly where aimed - a corner swipe reliably
        // hits the corner, a top swipe reliably lands just under the bar.
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

        Vector3 center = goalCenterPoint.position;
        Vector3 targetPoint = new Vector3(center.x + targetX, targetY, center.z);

        float power = 0.55f + power01 * 0.65f;
        float curve = normalizedX * 0.6f;

        InputEnabled = false;
        if (kicker != null)
        {
            StartCoroutine(kicker.PlayKick(() => ball.Shoot(targetPoint, power, curve)));
        }
        else
        {
            ball.Shoot(targetPoint, power, curve);
        }
    }
}

using System;
using System.Collections;
using UnityEngine;

public class KickerAnimator : KickerAnimatorBase
{
    public Transform hips;
    public Transform kickingLeg;
    public Transform plantLeg;
    public Transform rightArm;
    public Transform leftArm;

    public Vector3 runStartOffset = new Vector3(-1.3f, 0f, -2.4f);
    public float runDuration = 0.6f;
    public float runStrideSpeed = 9f;
    public float runStrideAmount = 28f;

    public float idleSwaySpeed = 1.2f;
    public float idleSwayAmount = 2f;

    Vector3 kickPosition;
    bool animating;

    void Awake()
    {
        kickPosition = transform.position;
        ResetToStart();
    }

    void Update()
    {
        if (animating || hips == null) return;
        float sway = Mathf.Sin(Time.time * idleSwaySpeed) * idleSwayAmount;
        hips.localRotation = Quaternion.Euler(0f, 0f, sway * 0.3f);
    }

    public override void ResetToStart()
    {
        StopAllCoroutines();
        animating = false;
        transform.position = kickPosition + transform.rotation * runStartOffset;
        ResetLimbs();
    }

    public override IEnumerator ApproachRoutine()
    {
        animating = true;
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < runDuration)
        {
            float t = elapsed / runDuration;
            transform.position = Vector3.Lerp(start, kickPosition, t);

            float stride = Mathf.Sin(elapsed * runStrideSpeed);
            if (plantLeg != null) plantLeg.localRotation = Quaternion.Euler(stride * runStrideAmount, 0, 0);
            if (kickingLeg != null) kickingLeg.localRotation = Quaternion.Euler(-stride * runStrideAmount, 0, 0);
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(-stride * runStrideAmount * 0.7f, 0, 0);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(stride * runStrideAmount * 0.7f, 0, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = kickPosition;
        ResetLimbs();
        animating = false;
    }

    public override IEnumerator PlayKick(Action onContact)
    {
        animating = true;

        const float windUp = 0.18f;
        const float strike = 0.1f;
        const float recover = 0.28f;

        Quaternion rest = Quaternion.identity;
        Quaternion back = Quaternion.Euler(-45f, 0f, 0f);
        Quaternion forward = Quaternion.Euler(70f, 0f, 0f);

        yield return AnimateLocalRotation(kickingLeg, rest, back, windUp);
        yield return AnimateLocalRotation(kickingLeg, back, forward, strike);
        onContact?.Invoke();
        yield return AnimateLocalRotation(kickingLeg, forward, rest, recover);

        animating = false;
    }

    public override IEnumerator PlayCelebration()
    {
        animating = true;
        Vector3 basePos = transform.position;
        Quaternion armsUp = Quaternion.Euler(160f, 0f, 0f);

        if (leftArm != null) leftArm.localRotation = armsUp;
        if (rightArm != null) rightArm.localRotation = armsUp;

        float duration = 1.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float hop = Mathf.Abs(Mathf.Sin(elapsed * 9f)) * 0.14f;
            transform.position = basePos + Vector3.up * hop;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = basePos;
        ResetLimbs();
        animating = false;
    }

    void ResetLimbs()
    {
        if (plantLeg != null) plantLeg.localRotation = Quaternion.identity;
        if (kickingLeg != null) kickingLeg.localRotation = Quaternion.identity;
        if (leftArm != null) leftArm.localRotation = Quaternion.identity;
        if (rightArm != null) rightArm.localRotation = Quaternion.identity;
        if (hips != null) hips.localRotation = Quaternion.identity;
    }

    IEnumerator AnimateLocalRotation(Transform t, Quaternion from, Quaternion to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            t.localRotation = Quaternion.Slerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        t.localRotation = to;
    }
}

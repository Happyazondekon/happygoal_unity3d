using System;
using System.Collections;
using UnityEngine;

// Drives a real Mixamo skinned character through its Animator instead of
// rotating primitive limb transforms by hand. The AnimatorController is
// expected to have a single "Kick" state holding the "Strike Foward Jog"
// clip, which already contains the run-up baked in - so ApproachRoutine is
// a no-op here, unlike the procedural KickerAnimator.
public class MixamoKickerAnimator : KickerAnimatorBase
{
    public Animator animator;

    [Range(0f, 1f)]
    public float contactNormalizedTime = 0.6f;

    Vector3 homePosition;
    Quaternion homeRotation;
    bool homeCaptured;

    public override void ResetToStart()
    {
        StopAllCoroutines();
        if (!homeCaptured)
        {
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeCaptured = true;
        }

        transform.position = homePosition;
        transform.rotation = homeRotation;

        if (animator == null) return;
        animator.Play("Kick", 0, 0f);
        animator.speed = 0f;
        animator.Update(0f);
    }

    public override IEnumerator ApproachRoutine()
    {
        // Baked into the "Kick" clip itself for the Mixamo rig.
        yield break;
    }

    public override IEnumerator PlayKick(Action onContact)
    {
        if (animator == null)
        {
            onContact?.Invoke();
            yield break;
        }

        animator.speed = 1f;
        animator.Play("Kick", 0, 0f);
        animator.Update(0f);
        bool fired = false;

        while (true)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!fired && state.IsName("Kick") && state.normalizedTime >= contactNormalizedTime)
            {
                fired = true;
                onContact?.Invoke();
            }
            if (state.IsName("Kick") && state.normalizedTime >= 1f) break;
            yield return null;
        }

        animator.speed = 0f;
    }

    public override IEnumerator PlayCelebration()
    {
        // No dedicated celebration clip yet - a simple hop stands in until one is added.
        Vector3 basePos = transform.position;
        float duration = 1.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float hop = Mathf.Abs(Mathf.Sin(elapsed * 9f)) * 0.14f;
            transform.position = basePos + Vector3.up * hop;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = basePos;
    }
}

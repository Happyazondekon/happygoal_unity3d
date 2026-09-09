using System.Collections;
using UnityEngine;

// Drives a real Mixamo goalkeeper character. The AnimatorController has four
// states, each backed by its own dedicated "With Skin" Mixamo download on the
// same textured character: "Idle" (loops, default/ready stance), "Catch"
// (center saves), "DiveRight" and "DiveLeft".
//
// Horizontal reach (X) is fully scripted - see PlayDiveClip - because how far
// a Mixamo clip's own root motion carries the character sideways varies too
// much clip to clip to build the save reach on. Vertical motion (Y) is the
// opposite: this character's root sits at the feet/ground already, so
// manually dropping it to fake a fall just buries it in the pitch. Instead
// OnAnimatorMove takes ONLY the clip's own Y root-motion delta each frame,
// so however much (or little) the dive clip actually drops the body is what
// plays - no guessed offset, and it can never go underground.
public class MixamoGoalkeeperAnimator : GoalkeeperAnimatorBase
{
    public Animator animator;
    public float reactionDelay = 0.15f;
    public float maxReachX = 3.0f;
    public float saveRadius = 0.55f;
    public float centerThreshold = 0.6f;
    public float diveSpeed = 6f;

    Vector3 homePosition;
    Quaternion homeRotation;

    void Awake()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
    }

    void OnAnimatorMove()
    {
        if (animator == null) return;
        transform.position += new Vector3(0f, animator.deltaPosition.y, 0f);
    }

    // Root motion is off for X/Z, but pin rotation every frame in case a
    // clip's baked pose still carries some drift into the root over time.
    void LateUpdate()
    {
        transform.rotation = homeRotation;
    }

    public override void ResetKeeper()
    {
        StopAllCoroutines();
        transform.position = homePosition;
        transform.rotation = homeRotation;
        if (animator != null)
        {
            animator.speed = 1f;
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
        }
    }

    public override bool ReactToShot(float targetX, float difficulty)
    {
        float difficulty01 = Mathf.Clamp01(difficulty);
        float guessError = Random.Range(-1f, 1f) * (1f - difficulty01) * maxReachX;
        float diveTargetX = Mathf.Clamp(targetX + guessError, -maxReachX, maxReachX);
        bool saved = Mathf.Abs(diveTargetX - targetX) <= saveRadius;

        PlayDirectedDive(diveTargetX);
        return saved;
    }

    public override void PlayDirectedDive(float diveTargetX)
    {
        StopAllCoroutines();
        StartCoroutine(PlayDiveClip(diveTargetX));
    }

    public override void SetLateralPosition(float worldX)
    {
        StopAllCoroutines();
        if (animator != null)
        {
            animator.Play("Idle", 0, 0f);
            animator.speed = 0f;
        }
        transform.position = new Vector3(worldX, transform.position.y, homePosition.z);
    }

    IEnumerator PlayDiveClip(float diveTargetX)
    {
        yield return new WaitForSeconds(reactionDelay);

        if (animator == null) yield break;

        float offset = diveTargetX - homePosition.x;

        if (Mathf.Abs(offset) < centerThreshold)
        {
            animator.Play("Catch", 0, 0f);
            yield break;
        }

        animator.Play(offset > 0f ? "DiveRight" : "DiveLeft", 0, 0f);

        float startX = transform.position.x;
        float duration = Mathf.Max(Mathf.Abs(offset) / diveSpeed, 0.2f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Mathf.Lerp(startX, diveTargetX, elapsed / duration);
            transform.position = new Vector3(x, transform.position.y, homePosition.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = new Vector3(diveTargetX, transform.position.y, homePosition.z);
    }
}

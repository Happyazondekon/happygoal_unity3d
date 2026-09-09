using System.Collections;
using UnityEngine;

public class GoalkeeperController : GoalkeeperAnimatorBase
{
    public float diveSpeed = 8f;
    public float reactionDelay = 0.15f;
    public float maxReachX = 3.0f;
    public float saveRadius = 0.55f;

    public Transform hips;
    public Transform leftShoulderPivot;
    public Transform rightShoulderPivot;

    Vector3 homePosition;

    void Awake()
    {
        homePosition = transform.position;
    }

    public override void ResetKeeper()
    {
        StopAllCoroutines();
        transform.position = homePosition;
        if (hips != null) hips.localRotation = Quaternion.identity;
        if (leftShoulderPivot != null) leftShoulderPivot.localRotation = Quaternion.identity;
        if (rightShoulderPivot != null) rightShoulderPivot.localRotation = Quaternion.identity;
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
        StartCoroutine(DiveRoutine(diveTargetX));
    }

    public override void SetLateralPosition(float worldX)
    {
        StopAllCoroutines();
        transform.position = new Vector3(worldX, homePosition.y, homePosition.z);
    }

    IEnumerator DiveRoutine(float diveTargetX)
    {
        yield return new WaitForSeconds(reactionDelay);

        Vector3 dest = new Vector3(diveTargetX, homePosition.y, homePosition.z);
        Vector3 start = transform.position;
        float duration = Mathf.Max(Vector3.Distance(start, dest) / diveSpeed, 0.15f);

        float side = Mathf.Sign(diveTargetX - homePosition.x);
        if (Mathf.Approximately(diveTargetX, homePosition.x)) side = 0f;

        // Local +X may point opposite world +X depending on which way this
        // character faces (e.g. the keeper is turned 180 deg to face the kicker),
        // so convert the world-space side into a local-space sign before using it.
        float facingSign = Mathf.Sign(Vector3.Dot(transform.right, Vector3.right));
        if (facingSign == 0f) facingSign = 1f;
        float localSide = side * facingSign;

        Transform reachArm = localSide >= 0 ? rightShoulderPivot : leftShoulderPivot;
        Quaternion hipsFrom = hips != null ? hips.localRotation : Quaternion.identity;
        Quaternion hipsTo = Quaternion.Euler(0f, 0f, -localSide * 45f);
        Quaternion armFrom = reachArm != null ? reachArm.localRotation : Quaternion.identity;
        Quaternion armTo = Quaternion.Euler(0f, 0f, -localSide * 110f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(start, dest, t);
            if (hips != null) hips.localRotation = Quaternion.Slerp(hipsFrom, hipsTo, t);
            if (reachArm != null) reachArm.localRotation = Quaternion.Slerp(armFrom, armTo, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = dest;
    }
}

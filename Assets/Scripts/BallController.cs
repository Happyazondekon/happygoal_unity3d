using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    public float baseSpeed = 14f;
    public float maxCurveForce = 6f;
    public float missZThreshold = 14f;
    public float maxFlightTime = 4f;
    public float keeperBlockZ = 10.3f;
    public NetRipple netRipple;

    public Action<Vector3> OnShotFired;
    public Action OnGoalScored;
    public Action OnShotBlocked;
    public Action OnShotMissed;

    Rigidbody rb;
    Vector3 startPosition;
    bool inFlight;
    float flightTimer;
    bool blockScheduled;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        // Small fast-moving ball vs. thin colliders (net, posts, keeper) -
        // discrete detection can tunnel straight through between physics
        // steps without this.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Update()
    {
        if (!inFlight) return;

        flightTimer += Time.deltaTime;

        if (blockScheduled && transform.position.z >= keeperBlockZ)
        {
            blockScheduled = false;
            inFlight = false;
            Deflect();
            OnShotBlocked?.Invoke();
            return;
        }

        if (transform.position.z > missZThreshold || flightTimer > maxFlightTime)
        {
            inFlight = false;
            OnShotMissed?.Invoke();
        }
    }

    // Called right when the shot is fired if the goalkeeper's roll decided
    // this one gets saved. The save is decided analytically up front (see
    // GoalkeeperAnimatorBase.ReactToShot) rather than by waiting for the
    // ball's collider to actually touch the keeper's mesh - that physical
    // contact is too easy to miss/mistime against a fast ball and an
    // animated character, especially once real skinned Mixamo rigs are
    // involved instead of simple primitives.
    public void ScheduleBlock()
    {
        blockScheduled = true;
    }

    // Parries the ball away instead of just freezing it in the air - reads
    // as the keeper punching/deflecting it rather than the ball mysteriously
    // stopping mid-flight.
    void Deflect()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 deflected = new Vector3(
            -v.x * 0.4f + UnityEngine.Random.Range(-1.5f, 1.5f),
            Mathf.Abs(v.y) * 0.4f + 2f,
            -Mathf.Abs(v.z) * 0.35f);
        rb.linearVelocity = deflected;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 5f;
    }

    public void ResetBall()
    {
        StopAllCoroutines();
        inFlight = false;
        flightTimer = 0f;
        blockScheduled = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
    }

    public void Shoot(Vector3 targetPoint, float power, float curve)
    {
        if (inFlight) return;
        inFlight = true;
        flightTimer = 0f;
        OnShotFired?.Invoke(targetPoint);

        Vector3 toTarget = targetPoint - transform.position;
        float speed = baseSpeed * Mathf.Clamp(power, 0.3f, 1.2f);

        Vector3 flatDelta = new Vector3(toTarget.x, 0f, toTarget.z);
        float horizontalDistance = Mathf.Max(flatDelta.magnitude, 0.01f);
        Vector3 flatDir = flatDelta / horizontalDistance;
        float heightDiff = toTarget.y;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float timeToTarget = horizontalDistance / speed;
        float vy = (heightDiff / Mathf.Max(timeToTarget, 0.01f)) + 0.5f * gravity * timeToTarget;

        rb.linearVelocity = flatDir * speed + Vector3.up * vy;

        if (Mathf.Abs(curve) > 0.01f)
        {
            StartCoroutine(ApplyCurve(curve));
        }
    }

    IEnumerator ApplyCurve(float curve)
    {
        float elapsed = 0f;
        while (elapsed < 1.1f && inFlight)
        {
            rb.AddForce(Vector3.right * curve * maxCurveForce, ForceMode.Acceleration);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!inFlight) return;
        if (other.CompareTag("GoalLine"))
        {
            inFlight = false;
            if (netRipple != null) netRipple.Impact(transform.position, rb.linearVelocity, rb.linearVelocity.magnitude * 0.05f);
            OnGoalScored?.Invoke();
        }
    }
}

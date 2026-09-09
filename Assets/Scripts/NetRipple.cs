using UnityEngine;

// Cheap mass-spring vertex displacement for a net mesh that isn't a
// SkinnedMeshRenderer (so Unity's built-in Cloth component doesn't apply
// directly). Only runs its per-vertex update while something is actually
// moving, settling back to the rest shape and going idle afterwards so it
// doesn't cost anything between impacts.
[RequireComponent(typeof(MeshFilter))]
public class NetRipple : MonoBehaviour
{
    public float stiffness = 140f;
    public float damping = 7f;
    public float impactRadius = 0.9f;
    public float maxDisplacement = 0.35f;
    public float activeDuration = 3f;

    Mesh mesh;
    Vector3[] baseVertices;
    Vector3[] currentVertices;
    Vector3[] velocities;
    bool active;
    float activeTimer;

    void Awake()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        baseVertices = mesh.vertices;
        currentVertices = (Vector3[])baseVertices.Clone();
        velocities = new Vector3[baseVertices.Length];
    }

    public void Impact(Vector3 worldPoint, Vector3 worldDir, float force)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 localDir = transform.InverseTransformDirection(worldDir.normalized);

        for (int i = 0; i < baseVertices.Length; i++)
        {
            float dist = Vector3.Distance(baseVertices[i], localPoint);
            if (dist < impactRadius)
            {
                float falloff = 1f - (dist / impactRadius);
                velocities[i] += localDir * force * falloff;
            }
        }

        active = true;
        activeTimer = 0f;
    }

    void Update()
    {
        if (!active) return;

        float dt = Mathf.Min(Time.deltaTime, 0.02f);
        bool anyMoving = false;

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 disp = currentVertices[i] - baseVertices[i];
            Vector3 accel = -disp * stiffness - velocities[i] * damping;
            velocities[i] += accel * dt;
            currentVertices[i] += velocities[i] * dt;

            Vector3 newDisp = currentVertices[i] - baseVertices[i];
            if (newDisp.magnitude > maxDisplacement)
            {
                currentVertices[i] = baseVertices[i] + newDisp.normalized * maxDisplacement;
            }

            if (velocities[i].sqrMagnitude > 0.0005f || newDisp.sqrMagnitude > 0.0005f)
            {
                anyMoving = true;
            }
        }

        mesh.vertices = currentVertices;
        mesh.RecalculateBounds();

        activeTimer += dt;
        if (!anyMoving || activeTimer > activeDuration)
        {
            currentVertices = (Vector3[])baseVertices.Clone();
            System.Array.Clear(velocities, 0, velocities.Length);
            mesh.vertices = currentVertices;
            mesh.RecalculateBounds();
            active = false;
        }
    }
}

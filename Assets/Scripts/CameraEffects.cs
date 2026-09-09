using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraEffects : MonoBehaviour
{
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.08f;
    public float zoomFov = 40f;
    public float zoomDuration = 0.6f;

    Camera cam;
    Vector3 basePosition;
    float baseFov;

    void Awake()
    {
        cam = GetComponent<Camera>();
        basePosition = transform.localPosition;
        baseFov = cam.fieldOfView;
    }

    public void Shake()
    {
        StopAllCoroutines();
        StartCoroutine(ShakeRoutine());
    }

    public void PunchZoom()
    {
        StopAllCoroutines();
        StartCoroutine(ZoomRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float damp = 1f - elapsed / shakeDuration;
            Vector2 offset = Random.insideUnitCircle * shakeMagnitude * damp;
            transform.localPosition = basePosition + new Vector3(offset.x, offset.y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = basePosition;
    }

    IEnumerator ZoomRoutine()
    {
        float half = zoomDuration / 2f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            cam.fieldOfView = Mathf.Lerp(baseFov, zoomFov, elapsed / half);
            elapsed += Time.deltaTime;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            cam.fieldOfView = Mathf.Lerp(zoomFov, baseFov, elapsed / half);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cam.fieldOfView = baseFov;
    }
}

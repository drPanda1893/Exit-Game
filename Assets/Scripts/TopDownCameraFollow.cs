using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] public float height = 11f;
    /// <summary>90 = von oben, 60 = isometrisch, 45 = stark geneigt</summary>
    [SerializeField] public float pitchAngle = 90f;
    /// <summary>
    /// > 0: Third-Person-Modus – Kamera hängt hinter dem Charakter.
    /// = 0: klassischer Top-Down-Modus.
    /// </summary>
    [SerializeField] public float behindDistance = 0f;
    /// <summary>
    /// Wenn gesetzt: Kamera bleibt fix an dieser Weltposition und schaut zum Spieler hin.
    /// Vector3.zero = deaktiviert (normaler Follow-Modus).
    /// </summary>
    [SerializeField] public Vector3 fixedWorldPosition = Vector3.zero;
    [SerializeField] private float smoothSpeed = 6f;

    private bool IsFixed => fixedWorldPosition != Vector3.zero;

    public void SetTarget(Transform t) => target = t;

    private void Start()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (IsFixed)
            transform.position = fixedWorldPosition;
    }

    private void LateUpdate()
    {
        if (IsFixed)
        {
            // Fest stehen, zum Spieler hinschauen
            transform.position = fixedWorldPosition;
            if (target != null)
                transform.rotation = Quaternion.LookRotation(
                    (target.position + Vector3.up * 0.8f) - fixedWorldPosition);
            return;
        }

        if (target == null) return;

        Vector3 desired;
        Quaternion desiredRot;

        if (behindDistance > 0f)
        {
            float rad = pitchAngle * Mathf.Deg2Rad;
            Vector3 back = target.forward * -behindDistance;
            Vector3 up   = Vector3.up * height;
            desired    = target.position + back + up;
            desiredRot = Quaternion.Euler(pitchAngle, target.eulerAngles.y, 0f);
        }
        else
        {
            float rad = pitchAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(0f,
                height * Mathf.Sin(rad),
               -height * Mathf.Cos(rad));
            desired    = target.position + offset;
            desiredRot = Quaternion.Euler(pitchAngle, 0f, 0f);
        }

        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, smoothSpeed * Time.deltaTime);
    }
}

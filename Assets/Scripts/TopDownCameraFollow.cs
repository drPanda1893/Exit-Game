using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] public float height = 11f;
    /// <summary>
    /// 90 = senkrecht von oben, 60 = isometrisch geneigt, 45 = stark geneigt
    /// </summary>
    [SerializeField] public float pitchAngle = 90f;
    [SerializeField] private float smoothSpeed = 6f;

    public void SetTarget(Transform t) => target = t;

    private void Start()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float rad = pitchAngle * Mathf.Deg2Rad;
        // Bei pitchAngle=90: offset=(0,h,0) | Bei pitchAngle=60: offset=(0,h*0.87,-h*0.5)
        Vector3 offset = new Vector3(0f,
            height * Mathf.Sin(rad),
           -height * Mathf.Cos(rad));

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
    }
}

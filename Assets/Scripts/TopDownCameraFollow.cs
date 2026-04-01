using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] public float height = 11f;
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

        Vector3 desired = new Vector3(target.position.x, target.position.y + height, target.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}

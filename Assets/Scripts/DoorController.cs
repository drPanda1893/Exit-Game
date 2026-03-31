using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    public float openDistance = 1.4f;
    public float openDuration = 1.2f;
    private bool isOpen = false;

    public void OpenDoor()
    {
        if (!isOpen)
            StartCoroutine(SlideDoor());
    }

    private IEnumerator SlideDoor()
    {
        isOpen = true;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.back * openDistance;
        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / openDuration);
            yield return null;
        }
        transform.position = endPos;
    }
}

using UnityEngine;

/// <summary>
/// Dreht das GameObject jeden Frame so, dass es zur Hauptkamera zeigt –
/// und zwar mit derselben Up-Richtung wie die Kamera. Damit ist ein TMP-Text
/// von jedem Winkel lesbar (nicht gespiegelt, nicht kopfueber).
///
/// Verwendung: an das Text-GameObject haengen. Ein lokaler Y-Versatz auf der
/// Decke reicht aus, damit die Schrift knapp ueber der Decke schwebt.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CameraFacingText : MonoBehaviour
{
    [Tooltip("Wenn leer, wird Camera.main verwendet.")]
    [SerializeField] private Camera targetCamera;

    void LateUpdate()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        // Schrift schaut in dieselbe Richtung wie die Kamera (forward),
        // mit derselben Up-Achse. Ergebnis: Text liest sich pixelgenau wie die UI.
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
    }
}

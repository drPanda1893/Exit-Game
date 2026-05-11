using UnityEngine;

/// <summary>
/// Trigger-Zone an der staubigen Wand.
/// Meldet nur ob der Spieler in der Nähe ist – Logik liegt in Level2_DustWall.
/// </summary>
public class DustyWallSpot : MonoBehaviour
{
    public bool PlayerNearby { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) PlayerNearby = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) PlayerNearby = false;
    }
}

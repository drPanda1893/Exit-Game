using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-Down Player Controller mit physikbasierter Bewegung über Unity 6's
/// <see cref="Rigidbody.linearVelocity"/>-API.
///
/// <para>
/// Verantwortlichkeiten:
/// <list type="bullet">
///   <item>Liest WASD- und Pfeiltasten-Eingaben über das neue
///         <see cref="UnityEngine.InputSystem"/>.</item>
///   <item>Berechnet eine normalisierte XZ-Bewegungsrichtung und dreht den Charakter
///         via Slerp in diese Richtung (im <see cref="Update"/>-Tick).</item>
///   <item>Schreibt die Geschwindigkeit pro Physik-Tick auf den Rigidbody
///         (im <see cref="FixedUpdate"/>-Tick), während die Y-Komponente für
///         Gravitation erhalten bleibt — der Charakter „fliegt" nicht.</item>
///   <item>Optionaler Bridge zu einem <see cref="CharacterAnimator"/>, der Idle/Walk
///         States schaltet, sobald sich der Charakter bewegt.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Voraussetzungen:</b> ein <see cref="Rigidbody"/> wird via
/// <see cref="RequireComponent"/> garantiert. Ist beim Hinzufügen dieses Skripts
/// noch keiner vorhanden, legt Unity automatisch einen an.
/// </para>
///
/// <para>
/// <b>Wichtig:</b> Diese Komponente gehört NUR auf die Spielfigur. NPCs wie
/// Big Yahu oder Helios dürfen <see cref="PlayerController"/> nicht tragen —
/// sonst spawnt Unity dort wegen <see cref="RequireComponent"/> einen Rigidbody
/// und die Konsole loggt „Creating missing Rigidbody component for PlayerController".
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// Bewegungsgeschwindigkeit in Unity-Einheiten pro Sekunde.
    /// Wird in <see cref="FixedUpdate"/> mit der normalisierten Bewegungsrichtung
    /// multipliziert und auf <see cref="Rigidbody.linearVelocity"/> geschrieben.
    /// </summary>
    /// <remarks>
    /// Default 6 ist auf die Top-Down-Räume (Level 1, Level 2) abgestimmt;
    /// größere Räume können höhere Werte (8–12) verlangen.
    /// </remarks>
    [SerializeField] private float moveSpeed = 6f;

    /// <summary>
    /// Schrittweite der Slerp-basierten Drehung des Charakters in Bewegungsrichtung.
    /// Wird in <see cref="Update"/> mit <see cref="Time.deltaTime"/> multipliziert.
    /// </summary>
    /// <remarks>
    /// Höhere Werte = schnappiger; niedrigere Werte = sanftere Animation.
    /// Default 20 entspricht etwa 1/3 Sekunde für eine 180°-Drehung bei 60 FPS.
    /// </remarks>
    [SerializeField] private float rotationSpeed = 20f;

    /// <summary>
    /// Cache der optionalen <see cref="CharacterAnimator"/>-Bridge.
    /// Triggert <c>SetMoving(bool)</c>, sobald sich der Bewegungszustand ändert.
    /// Bleibt <c>null</c>, wenn keine Animator-Komponente am GameObject hängt.
    /// </summary>
    private CharacterAnimator characterAnimator;

    /// <summary>
    /// Cache des Rigidbodys. Garantiert nicht-null durch <see cref="RequireComponent"/>.
    /// Wird in <see cref="Start"/> einmalig gesetzt.
    /// </summary>
    private Rigidbody rb;

    /// <summary>
    /// Aktuelle, normalisierte Bewegungsrichtung in der XZ-Ebene (Y immer 0).
    /// Wird in <see cref="Update"/> aus den Tastatureingaben gefüllt und in
    /// <see cref="FixedUpdate"/> auf die Velocity übertragen.
    /// </summary>
    private Vector3 moveDirection;

    /// <summary>
    /// Initialisiert Komponenten-Caches und konfiguriert den Rigidbody so,
    /// dass der Charakter weder umkippt noch fliegt.
    /// </summary>
    /// <remarks>
    /// Setzt:
    /// <list type="bullet">
    ///   <item><c>FreezeRotation</c> (X/Y/Z) — Rotation steuern wir manuell via Slerp.</item>
    ///   <item><c>useGravity = true</c> — Charakter bleibt am Boden.</item>
    ///   <item><c>isKinematic = false</c> — Velocity-Schreibvorgänge werden wirksam.</item>
    ///   <item><c>interpolation = Interpolate</c> — flüssige Visualisierung
    ///         zwischen Physik-Ticks.</item>
    /// </list>
    /// </remarks>
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        characterAnimator = GetComponent<CharacterAnimator>();

        rb.constraints   = rb.constraints | RigidbodyConstraints.FreezeRotation;
        rb.useGravity    = true;
        rb.isKinematic   = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>
    /// Per-Frame-Logik: liest WASD und Pfeiltasten, befüllt
    /// <see cref="moveDirection"/>, dreht den Charakter in Bewegungsrichtung
    /// und meldet den Bewegungszustand an den <see cref="CharacterAnimator"/>.
    /// </summary>
    /// <remarks>
    /// Reine Visualisierung und Eingabeauswertung — keine Physik-Schreibvorgänge.
    /// Diese finden ausschließlich in <see cref="FixedUpdate"/> statt.
    /// </remarks>
    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        float h = 0f, v = 0f;
        if (keyboard.leftArrowKey.isPressed  || keyboard.aKey.isPressed) h = -1f;
        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) h =  1f;
        if (keyboard.upArrowKey.isPressed    || keyboard.wKey.isPressed) v =  1f;
        if (keyboard.downArrowKey.isPressed  || keyboard.sKey.isPressed) v = -1f;

        moveDirection = new Vector3(h, 0f, v);
        bool isMoving = moveDirection.sqrMagnitude > 0f;

        if (isMoving)
        {
            moveDirection.Normalize();
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDirection),
                rotationSpeed * Time.deltaTime);
        }

        characterAnimator?.SetMoving(isMoving);
    }

    /// <summary>
    /// Per-Physik-Tick-Logik: schreibt die Geschwindigkeit auf den Rigidbody.
    /// </summary>
    /// <remarks>
    /// XZ-Komponenten kommen aus <c>moveDirection × moveSpeed</c>.
    /// Die Y-Komponente wird aus dem aktuellen Velocity-Vektor übernommen,
    /// damit Gravitation weiterhin greift und der Charakter nicht „fliegt".
    /// <para>
    /// <c>angularVelocity</c> wird auf Null gesetzt, um durch Kollisionen
    /// erzeugten Drehimpuls zu unterdrücken — die Drehung wird ausschließlich
    /// in <see cref="Update"/> via Slerp gesteuert.
    /// </para>
    /// </remarks>
    void FixedUpdate()
    {
        Vector3 horizontal = moveDirection * moveSpeed;
        Vector3 current    = rb.linearVelocity;
        rb.linearVelocity  = new Vector3(horizontal.x, current.y, horizontal.z);
        rb.angularVelocity = Vector3.zero;
    }
}

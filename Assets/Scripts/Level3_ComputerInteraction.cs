using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// Trigger-Zone um den PC in der Bibliothek.
/// Initial inaktiv – wird aktiviert, sobald der Spieler die Bibel ausgewählt hat
/// (über HeliosInteraction → ActivateComputer()).
///
/// Ablauf:
///   1. Bibel gewählt → ActivateComputer() schaltet Monitor + Spot ein.
///   2. Spieler betritt Trigger-Zone → "[E] Computer benutzen" Hinweis.
///   3. E gedrückt → Level3_ColorCodeUI wird geöffnet.
///   4. Richtige Farbsequenz → Szene wechselt direkt zu Level 4
///      (das Computer-Minigame ist die "Innensicht" des PCs).
/// </summary>
[RequireComponent(typeof(Collider))]
public class Level3_ComputerInteraction : MonoBehaviour
{
    [Header("Verbundene Objekte")]
    public Level3_ColorCodeUI codeUI;
    public GameObject hintGO;          // "[E] Computer benutzen"-Hinweis
    public GameObject monitorScreen;   // Monitor-Quad (Renderer mit emissivem Material)
    public Light      monitorLight;    // Optionales Spotlight am PC
    public Material   monitorOffMat;   // Dunkles Material wenn aus
    public Material   monitorOnMat;    // Leuchtendes Material wenn an

    [Header("Ziel-Szene nach korrektem Code")]
    [Tooltip("Wird via SceneManager.LoadScene geladen, sobald der Farbcode stimmt.")]
    public string nextScene = "Level4";

    [Tooltip("Sekunden Pause nach erfolgreichem Code, bevor die Szene wechselt.")]
    public float transitionDelay = 0.6f;

    [Header("State")]
    public bool isActive  = false;     // wird per ActivateComputer() gesetzt
    public bool isSolved  = false;

    private bool inRange = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (hintGO != null) hintGO.SetActive(false);
        ApplyMonitorState();
        if (codeUI != null) codeUI.OnCodeAccepted += OnPuzzleSolved;
    }

    void OnDestroy()
    {
        if (codeUI != null) codeUI.OnCodeAccepted -= OnPuzzleSolved;
    }

    void Update()
    {
        if (!isActive || isSolved || !inRange) return;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            codeUI?.Show();
            if (hintGO != null) hintGO.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        inRange = true;
        if (isActive && !isSolved && hintGO != null) hintGO.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        inRange = false;
        if (hintGO != null) hintGO.SetActive(false);
        codeUI?.Hide();
    }

    /// <summary>
    /// Schaltet den PC scharf: Monitor leuchtet, Spotlicht an, Trigger funktioniert.
    /// Wird von HeliosInteraction nach der Bibelwahl aufgerufen.
    /// </summary>
    public void ActivateComputer()
    {
        if (isActive) return;
        isActive = true;
        ApplyMonitorState();
        if (inRange && hintGO != null) hintGO.SetActive(true);
    }

    /// <summary>
    /// Direkt nach der Bibel-Wahl: PC scharf schalten UND das Login-Terminal
    /// als Popup öffnen. Kleine Verzögerung, damit die Buch-Auswahl-UI vorher
    /// sauber ausblenden kann. Schließt der Spieler das Popup, lässt es sich
    /// später am PC erneut über die [E]-Interaktion öffnen.
    /// </summary>
    public void OpenLoginScreen(float delay = 1.7f)
    {
        ActivateComputer();
        // Den Arduino-Farbsensor NICHT hier schon scharf schalten:
        // das Sektor-Terminal wird erst in Show() lazy erzeugt – frueh empfangene
        // COLOR:GREEN-Messages wuerden sonst in den Legacy-Pfad fallen und verloren gehen.
        if (isSolved) return;
        StartCoroutine(ShowLoginAfter(delay));
    }

    private IEnumerator ShowLoginAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (!isSolved) codeUI?.Show();
        if (hintGO != null) hintGO.SetActive(false);
    }

    private void ApplyMonitorState()
    {
        if (monitorScreen != null)
        {
            var rend = monitorScreen.GetComponent<Renderer>();
            if (rend != null)
            {
                if (isActive && monitorOnMat  != null) rend.sharedMaterial = monitorOnMat;
                if (!isActive && monitorOffMat != null) rend.sharedMaterial = monitorOffMat;
            }
        }
        if (monitorLight != null) monitorLight.enabled = isActive;
    }

    private void OnPuzzleSolved()
    {
        isSolved = true;
        inRange  = false;
        if (hintGO != null) hintGO.SetActive(false);
        StartCoroutine(LoadNextScene());
    }

    private IEnumerator LoadNextScene()
    {
        yield return new WaitForSeconds(transitionDelay);

        // Statt direkt Level 4 zu laden, zuerst den Schock-Cinematic abspielen.
        // Der Cinematic pausiert die Hintergrundmusik, zeigt die Text-Karte,
        // spielt das Wärter-Video und laedt anschliessend Level 4.
        Level3to4Cinematic.Play();
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Orchestriert Level 5 als 3D-Szene mit Schuppen.
///
/// Ablauf:
///   1. Player spawnt vor dem geschlossenen Schuppen (Outside).
///   2. Trigger an der Schuppentür: [E] → Breadboard-Overlay öffnet sich.
///   3. Breadboard gelöst → Tür öffnet sich (3D-Animation: deaktivieren + Drehung),
///      Bunsenbrenner-Pickup im Inneren wird aktiv.
///   4. Player betritt Schuppen, [E] am Bunsenbrenner → Pickup-Dialog.
///   5. Exit-Marker (außerhalb) wird aktiv. Player läuft hin → Szene wechselt zu Level 6.
///
/// Wird vom BuildLevel5Workshop im Editor vollständig verkabelt.
/// </summary>
public class Level5_SceneFlow : MonoBehaviour
{
    public enum State { Outside, Solving, LeverReady, Pickup, ReturnToExit, Done }

    [Header("Türinteraktion (am Schuppen)")]
    public DustyWallSpot doorSpot;
    public GameObject    doorPrompt;       // "[E] Schaltung reparieren"
    public GameObject    doorObject;       // 3D-Tür-Box (wird deaktiviert + animiert)

    [Header("Breadboard-Overlay")]
    public GameObject       breadboardCanvas;   // Canvas-Wurzel, initial inaktiv
    public Level5_Breadboard breadboard;        // Puzzle-Script

    [Header("Hebel (nach Puzzle-Lösung)")]
    public DustyWallSpot leverSpot;
    public GameObject    leverPrompt;    // "[E] Hebel umlegen"
    public GameObject    leverHandle;    // LeverPivot – dreht sich nach vorne

    [Header("Bunsenbrenner-Pickup im Schuppen")]
    public DustyWallSpot brennerSpot;
    public GameObject    brennerPrompt;    // "[E] Bunsenbrenner aufnehmen"
    public GameObject    brennerObject;    // 3D-Brenner auf Werkbank
    public Light         brennerGlow;      // optionales Licht, signalisiert "aktiv"

    [Header("Ausgang nach Pickup")]
    public DustyWallSpot exitSpot;         // Trigger draußen vor dem Schuppen
    public GameObject    exitMarker;       // visuelles Markierung (z.B. leuchtendes Quad)
    public GameObject    exitPrompt;       // "Zurück zum Hof" Texthinweis (Auto-Trigger, kein E nötig)

    [Header("Szene danach")]
    public string nextScene = "Level6";

    public State CurrentState { get; private set; } = State.Outside;

    void Start()
    {
        if (doorPrompt    != null) doorPrompt.SetActive(false);
        if (leverPrompt   != null) leverPrompt.SetActive(false);
        if (brennerPrompt != null) brennerPrompt.SetActive(false);
        if (exitPrompt    != null) exitPrompt.SetActive(false);
        if (exitMarker    != null) exitMarker.SetActive(false);
        if (brennerGlow   != null) brennerGlow.enabled = false;
        if (breadboardCanvas != null) breadboardCanvas.SetActive(false);

        if (breadboard != null)
            breadboard.OnPuzzleSolved += OnBreadboardSolved;

        BigYahuDialogSystem.Instance?.ShowDialog(new[]
        {
            "Big Yahu: Da steht ja der Schuppen – und die Tür ist verriegelt.",
            "Big Yahu: An der Tür hängt eine Steuerplatine. Reparier sie!",
        });
    }

    void OnDestroy()
    {
        if (breadboard != null)
            breadboard.OnPuzzleSolved -= OnBreadboardSolved;
    }

    void Update()
    {
        var kb = Keyboard.current;

        switch (CurrentState)
        {
            case State.Outside:
                bool nearDoor = doorSpot != null && doorSpot.PlayerNearby;
                if (doorPrompt != null) doorPrompt.SetActive(nearDoor);
                if (nearDoor && kb != null && kb.eKey.wasPressedThisFrame)
                    OpenBreadboard();
                break;

            case State.LeverReady:
                bool nearLever = leverSpot != null && leverSpot.PlayerNearby;
                if (leverPrompt != null) leverPrompt.SetActive(nearLever);
                if (nearLever && kb != null && kb.eKey.wasPressedThisFrame)
                    StartCoroutine(LeverPulledRoutine());
                break;

            case State.Pickup:
                bool nearBrenner = brennerSpot != null && brennerSpot.PlayerNearby;
                if (brennerPrompt != null) brennerPrompt.SetActive(nearBrenner);
                if (nearBrenner && kb != null && kb.eKey.wasPressedThisFrame)
                    StartCoroutine(PickupBrenner());
                break;

            case State.ReturnToExit:
                if (exitSpot != null && exitSpot.PlayerNearby)
                    StartCoroutine(LeaveScene());
                break;
        }
    }

    // ── Phase 1 → 2: Tür-Interaktion öffnet das Puzzle ───────────────────

    void OpenBreadboard()
    {
        CurrentState = State.Solving;
        if (doorPrompt       != null) doorPrompt.SetActive(false);

        // Ohne EventSystem werden Maus-Klicks von Buttons nie verarbeitet.
        // Falls die Szene ohne eines gebaut wurde, hier nachholen.
        EnsureEventSystem();

        if (breadboardCanvas != null) breadboardCanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    // ── Phase 2 → 3: Puzzle gelöst → Schuppentür öffnen ──────────────────

    void OnBreadboardSolved()
    {
        StartCoroutine(PuzzleSolvedRoutine());
    }

    // Puzzle gelöst → kurze Pause, Canvas weg, Hebel aktivieren
    IEnumerator PuzzleSolvedRoutine()
    {
        yield return new WaitForSeconds(1.2f);
        if (breadboardCanvas != null) breadboardCanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        CurrentState = State.LeverReady;
        BigYahuDialogSystem.Instance?.ShowDialog(new[]
        {
            "Big Yahu: Schaltkreis repariert! Jetzt den Hebel links an der Tür umlegen!"
        });
    }

    // Hebel gedrückt → animieren, dann Tür aufmachen
    IEnumerator LeverPulledRoutine()
    {
        CurrentState = State.Pickup;
        if (leverPrompt != null) leverPrompt.SetActive(false);

        if (leverHandle != null)
        {
            float t = 0f;
            Quaternion start = leverHandle.transform.localRotation;
            Quaternion end   = start * Quaternion.Euler(-90f, 0f, 0f);
            while (t < 1f)
            {
                t += Time.deltaTime * 2.5f;
                leverHandle.transform.localRotation =
                    Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.3f);

        if (doorObject != null)
        {
            float t = 0f;
            Quaternion start = doorObject.transform.localRotation;
            Quaternion end   = start * Quaternion.Euler(0f, -95f, 0f);
            while (t < 1f)
            {
                t += Time.deltaTime * 1.2f;
                doorObject.transform.localRotation =
                    Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            foreach (var c in doorObject.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        if (brennerGlow != null) brennerGlow.enabled = true;

        BigYahuDialogSystem.Instance?.ShowDialog(new[]
        {
            "Big Yahu: Die Tür schwingt auf! Der Bunsenbrenner liegt auf der Werkbank.",
            "Big Yahu: Schnapp ihn dir und nichts wie raus."
        });
    }

    // ── Phase 3 → 4: Brenner aufnehmen ───────────────────────────────────

    IEnumerator PickupBrenner()
    {
        CurrentState = State.ReturnToExit;
        if (brennerPrompt != null) brennerPrompt.SetActive(false);

        // Visuelles Pickup: kurzes Aufleuchten, dann verschwindet er
        if (brennerObject != null)
        {
            float t = 0f;
            Vector3 origScale = brennerObject.transform.localScale;
            while (t < 1f)
            {
                t += Time.deltaTime * 2f;
                brennerObject.transform.localScale = Vector3.Lerp(origScale, Vector3.zero, t);
                yield return null;
            }
            brennerObject.SetActive(false);
        }

        if (brennerGlow != null) brennerGlow.enabled = false;

        // Exit-Marker aktivieren
        if (exitMarker != null) exitMarker.SetActive(true);
        if (exitPrompt != null) exitPrompt.SetActive(true);

        BigYahuDialogSystem.Instance?.ShowDialog(new[]
        {
            "Big Yahu: Bunsenbrenner in der Tasche!",
            "Big Yahu: Raus hier – das Tor wartet."
        });
    }

    // ── Phase 4 → 5: Spieler verlässt den Schuppen ───────────────────────

    bool leaving;
    IEnumerator LeaveScene()
    {
        if (leaving) yield break;
        leaving = true;
        CurrentState = State.Done;
        if (exitPrompt != null) exitPrompt.SetActive(false);

        yield return new WaitForSeconds(0.4f);

        if (GameManager.Instance != null)
            GameManager.Instance.CompleteCurrentLevel();
        else if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}

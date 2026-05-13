using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Events;

namespace EscapeTheMatrix.Sektor03
{
    /// <summary>
    /// Controller für das Bibliothek-Terminal (Level 3).
    /// Verbindet das UXML/USS-UI mit der Spiel-Logik.
    ///
    /// Setup in Unity:
    ///   1. Empty GameObject erstellen
    ///   2. UIDocument-Komponente hinzufügen
    ///   3. Source Asset = Sektor03Terminal.uxml
    ///   4. PanelSettings zuweisen
    ///   5. Dieses Script auf das gleiche GameObject ziehen
    ///   6. UIDocument-Feld im Inspector zuweisen
    ///   7. Korrekte Sequenz im Inspector setzen
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class Sektor03TerminalController : MonoBehaviour
    {
        // ============ KONFIGURATION ============
        [Header("Konfiguration")]
        [Tooltip("Die korrekte Farbsequenz die der Spieler eingeben muss.")]
        [SerializeField] private ColorCode[] correctSequence = new ColorCode[]
        {
            ColorCode.RED,
            ColorCode.BLUE,
            ColorCode.YELLOW
        };

        [Tooltip("Maximale Anzahl Versuche. 0 = unbegrenzt.")]
        [SerializeField] private int maxAttempts = 0;

        // ============ EVENTS ============
        [Header("Events")]
        [Tooltip("Wird ausgelöst wenn die Sequenz korrekt eingegeben wurde.")]
        public UnityEvent OnAuthenticationSuccess;

        [Tooltip("Wird ausgelöst wenn die Sequenz falsch ist.")]
        public UnityEvent OnAuthenticationFailure;

        [Tooltip("Wird ausgelöst wenn die maximale Versuchszahl erreicht ist.")]
        public UnityEvent OnAttemptsExhausted;

        // ============ COLOR ENUM ============
        public enum ColorCode { RED, ORANGE, YELLOW, GREEN, BLUE, PURPLE }

        // Hex-Farben aus dem Design-Mockup
        private static readonly Dictionary<ColorCode, Color> ColorMap = new Dictionary<ColorCode, Color>
        {
            { ColorCode.RED,    new Color32(0xE6, 0x39, 0x46, 0xFF) },
            { ColorCode.ORANGE, new Color32(0xF4, 0xA2, 0x61, 0xFF) },
            { ColorCode.YELLOW, new Color32(0xF4, 0xD3, 0x5E, 0xFF) },
            { ColorCode.GREEN,  new Color32(0x4A, 0xDE, 0x80, 0xFF) },
            { ColorCode.BLUE,   new Color32(0x3A, 0x86, 0xFF, 0xFF) },
            { ColorCode.PURPLE, new Color32(0x9D, 0x4E, 0xDD, 0xFF) }
        };

        // ============ STATE ============
        private readonly ColorCode?[] sequence = new ColorCode?[3];
        private int attemptsLeft;

        // ============ UI REFERENZEN ============
        private UIDocument document;
        private VisualElement screen;
        private VisualElement[] slots = new VisualElement[3];
        private VisualElement[] fills = new VisualElement[3];
        private Button submitBtn;
        private Button resetBtn;
        private ScrollView logView;
        private Label timeLabel;
        private Label attemptsLabel;
        private VisualElement cursor;
        private VisualElement okOverlay;
        private VisualElement errOverlay;

        // ============ LIFECYCLE ============
        private void Awake()
        {
            document = GetComponent<UIDocument>();
            attemptsLeft = maxAttempts;
        }

        private void OnEnable()
        {
            BindUI();
            StartCoroutine(BootSequence());
            StartCoroutine(BlinkCursor());
            StartCoroutine(ClockTicker());
        }

        private void OnDisable()
        {
            UnbindUI();
        }

        // ============ UI BINDING ============
        private void BindUI()
        {
            var root = document.rootVisualElement;

            screen   = root.Q<VisualElement>("screen");
            slots[0] = root.Q<VisualElement>("slot-0");
            slots[1] = root.Q<VisualElement>("slot-1");
            slots[2] = root.Q<VisualElement>("slot-2");
            fills[0] = root.Q<VisualElement>("fill-0");
            fills[1] = root.Q<VisualElement>("fill-1");
            fills[2] = root.Q<VisualElement>("fill-2");

            submitBtn     = root.Q<Button>("submit-btn");
            resetBtn      = root.Q<Button>("reset-btn");
            logView       = root.Q<ScrollView>("log");
            timeLabel     = root.Q<Label>("time");
            attemptsLabel = root.Q<Label>("attempts");
            cursor        = root.Q<VisualElement>("cursor");
            okOverlay     = root.Q<VisualElement>("ok-overlay");
            errOverlay    = root.Q<VisualElement>("err-overlay");

            // Color-Buttons binden
            BindColorButton(root, "btn-red",    ColorCode.RED);
            BindColorButton(root, "btn-orange", ColorCode.ORANGE);
            BindColorButton(root, "btn-yellow", ColorCode.YELLOW);
            BindColorButton(root, "btn-green",  ColorCode.GREEN);
            BindColorButton(root, "btn-blue",   ColorCode.BLUE);
            BindColorButton(root, "btn-purple", ColorCode.PURPLE);

            submitBtn.clicked += OnSubmit;
            resetBtn.clicked  += OnReset;
            submitBtn.SetEnabled(false);

            UpdateAttemptsLabel();
        }

        private void UnbindUI()
        {
            if (submitBtn != null) submitBtn.clicked -= OnSubmit;
            if (resetBtn  != null) resetBtn.clicked  -= OnReset;
        }

        private void BindColorButton(VisualElement root, string name, ColorCode color)
        {
            var btn = root.Q<Button>(name);
            if (btn != null) btn.clicked += () => AddColor(color);
        }

        // ============ PUBLIC API ============
        /// <summary>
        /// Fügt eine Farbe zur Sequenz hinzu. Kann z.B. vom Arduino-Color-Sensor
        /// aufgerufen werden statt vom UI-Button.
        /// </summary>
        public void AddColor(ColorCode color)
        {
            int idx = NextEmptySlot();
            if (idx == -1) return;

            sequence[idx] = color;
            slots[idx].RemoveFromClassList("empty");
            slots[idx].AddToClassList("filled");
            fills[idx].style.backgroundColor = ColorMap[color];

            Log($"SLOT_0{idx + 1} = {color}", LogType.Ok);

            if (IsSequenceComplete())
            {
                submitBtn.SetEnabled(true);
                Log("SEQUENZ_BEREIT — AUTHENTICATE drücken", LogType.Warn);
            }
        }

        /// <summary>Setzt die Sequenz zurück.</summary>
        public void ResetSequence()
        {
            OnReset();
        }

        /// <summary>Sendet die aktuelle Sequenz zur Prüfung.</summary>
        public void SubmitSequence()
        {
            OnSubmit();
        }

        // ============ HANDLERS ============
        private void OnReset()
        {
            for (int i = 0; i < 3; i++)
            {
                sequence[i] = null;
                slots[i].RemoveFromClassList("filled");
                slots[i].AddToClassList("empty");
                fills[i].style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0));
            }
            submitBtn.SetEnabled(false);
            Log("SEQUENZ_RESET", LogType.Warn);
        }

        private void OnSubmit()
        {
            if (!IsSequenceComplete()) return;

            bool correct = true;
            for (int i = 0; i < 3; i++)
            {
                if (sequence[i] != correctSequence[i])
                {
                    correct = false;
                    break;
                }
            }

            if (correct)
            {
                Log("AUTHENTIFIZIERUNG ERFOLGREICH", LogType.Ok);
                StartCoroutine(ShowOverlay(okOverlay, 2.5f));
                OnAuthenticationSuccess?.Invoke();
            }
            else
            {
                if (maxAttempts > 0)
                {
                    attemptsLeft--;
                    UpdateAttemptsLabel();
                }

                Log("FALSCHE_SEQUENZ — ZUGANG VERWEIGERT", LogType.Err);
                StartCoroutine(ShowOverlay(errOverlay, 1.8f));
                StartCoroutine(ShakeScreen());
                OnAuthenticationFailure?.Invoke();
                StartCoroutine(ResetAfter(1.9f));

                if (maxAttempts > 0 && attemptsLeft <= 0)
                {
                    Log("MAXIMALE_VERSUCHE_ERREICHT — SYSTEM_LOCKED", LogType.Err);
                    submitBtn.SetEnabled(false);
                    OnAttemptsExhausted?.Invoke();
                }
            }
        }

        // ============ HELPERS ============
        private int NextEmptySlot()
        {
            for (int i = 0; i < sequence.Length; i++)
                if (sequence[i] == null) return i;
            return -1;
        }

        private bool IsSequenceComplete()
        {
            for (int i = 0; i < sequence.Length; i++)
                if (sequence[i] == null) return false;
            return true;
        }

        private void UpdateAttemptsLabel()
        {
            if (attemptsLabel == null) return;
            attemptsLabel.text = maxAttempts == 0 ? "∞" : attemptsLeft.ToString();
        }

        // ============ LOG ============
        private enum LogType { Dim, Ok, Warn, Err }

        private void Log(string msg, LogType type = LogType.Dim)
        {
            if (logView == null) return;

            string time = System.DateTime.Now.ToString("HH:mm:ss");
            string symbol = type switch
            {
                LogType.Ok   => ">",
                LogType.Warn => "!",
                LogType.Err  => "x",
                _            => ">"
            };

            var line = new Label($"[{time}] {symbol} {msg}");
            line.AddToClassList("log-line");
            switch (type)
            {
                case LogType.Ok:   line.AddToClassList("ok");   break;
                case LogType.Warn: line.AddToClassList("warn"); break;
                case LogType.Err:  line.AddToClassList("err");  break;
            }
            logView.Add(line);

            // Scroll nach unten
            logView.schedule.Execute(() =>
            {
                logView.scrollOffset = new Vector2(0, float.MaxValue);
            }).ExecuteLater(50);
        }

        // ============ COROUTINES ============
        private IEnumerator BootSequence()
        {
            yield return new WaitForSeconds(0.3f);
            Log("BOOT_SEQUENCE OK", LogType.Ok);
            yield return new WaitForSeconds(0.4f);
            Log("SECTOR_03 LOADED", LogType.Ok);
            yield return new WaitForSeconds(0.3f);
            Log("TCS230_SENSOR ONLINE", LogType.Ok);
            yield return new WaitForSeconds(0.4f);
            Log("WAITING_FOR_INPUT...", LogType.Warn);
        }

        private IEnumerator BlinkCursor()
        {
            while (true)
            {
                if (cursor != null)
                    cursor.style.opacity = 1f;
                yield return new WaitForSeconds(0.5f);
                if (cursor != null)
                    cursor.style.opacity = 0f;
                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator ClockTicker()
        {
            while (true)
            {
                if (timeLabel != null)
                    timeLabel.text = System.DateTime.Now.ToString("HH:mm:ss");
                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator ShowOverlay(VisualElement overlay, float duration)
        {
            overlay.RemoveFromClassList("hidden");
            yield return new WaitForSeconds(duration);
            overlay.AddToClassList("hidden");
        }

        private IEnumerator ShakeScreen()
        {
            if (screen == null) yield break;
            float t = 0f;
            float duration = 0.4f;
            while (t < duration)
            {
                float x = Mathf.Sin(t * 60f) * 12f * (1f - t / duration);
                screen.style.translate = new StyleTranslate(new Translate(x, 0));
                t += Time.deltaTime;
                yield return null;
            }
            screen.style.translate = new StyleTranslate(new Translate(0, 0));
        }

        private IEnumerator ResetAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            OnReset();
        }
    }
}

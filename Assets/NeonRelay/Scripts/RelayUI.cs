using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NeonRelay
{
    // Every screen uses Unity uGUI: Canvas, Text, Image, Button, Slider and InputField.
    public sealed class RelayUI : MonoBehaviour
    {
        RelayGame game; Canvas canvas; RectTransform page; Font font;
        Text score, status, charge, distance, saveLabel;
        Image chargeBar; InputField pilot; string screen, optionsBack;
        readonly Color ink = new Color(.035f, .065f, .105f, .98f);
        readonly Color muted = new Color(.52f, .65f, .70f);
        readonly Color white = new Color(.90f, .95f, .94f);
        Color cyan => game.World.Cyan;
        public string Screen => screen;

        public void Initialize(RelayGame owner)
        {
            game = owner; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Neon Relay / Unity UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (!FindFirstObjectByType<EventSystem>())
                new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        Image Block(Transform parent, float x, float y, float w, float h, Color color)
        { var r = Rect("Panel", parent, x, y, w, h); var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
        Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color color, bool bold = false)
        {
            var r = Rect(value, parent, x, y, w, h); var t = r.gameObject.AddComponent<Text>(); t.font = font;
            t.text = value; t.fontSize = size; t.color = color; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.supportRichText = false; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate; return t;
        }
        Button Button(string value, float x, float y, float w, Action action, bool primary = false)
        {
            var r = Rect(value, page, x, y, w, 55); var image = r.gameObject.AddComponent<Image>();
            image.color = primary ? cyan : new Color(.09f, .16f, .20f);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            var colors = b.colors; colors.highlightedColor = new Color(.70f, .95f, .88f); colors.pressedColor = new Color(.4f, .75f, .7f); b.colors = colors;
            var t = Label(r, value, 20, 0, w - 40, 55, 18, primary ? ink : white, true); t.alignment = TextAnchor.MiddleLeft;
            b.onClick.AddListener(() => { game.Audio.Cue(2); action(); }); return b;
        }
        void NewPage(string name, bool panel = true)
        {
            screen = name;
            if (page) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            // A centered reference frame keeps every menu inside the viewport at all aspect ratios.
            page = Rect(name, canvas.transform, 0, 0, 1600, 900);
            page.anchorMin = page.anchorMax = page.pivot = new Vector2(.5f, .5f); page.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float scale = Mathf.Min(((RectTransform)canvas.transform).rect.width / 1600f, ((RectTransform)canvas.transform).rect.height / 900f);
            page.localScale = Vector3.one * Mathf.Min(1, scale);
            if (panel)
            {
                Block(page, 0, 0, 700, 900, ink); Block(page, 699, 0, 1, 900, new Color(.18f, .34f, .36f));
                Label(page, "NR  /  ORBITAL COURIER NETWORK", 64, 42, 590, 28, 16, cyan, true);
                Label(page, "SECTOR 07     /     NIGHT SHIFT", 64, 842, 560, 22, 14, muted);
                Label(page, "CONNECTION SECURE   +", 1200, 845, 350, 25, 14, cyan);
            }
        }
        void Heading(string kicker, string title, string subtitle)
        {
            Label(page, kicker, 64, 117, 570, 28, 15, cyan, true);
            Label(page, title, 60, 158, 600, 110, 61, white, true);
            Label(page, subtitle, 64, 275, 555, 88, 20, muted);
        }
        void FieldCard()
        {
            Block(page, 1020, 130, 500, 220, new Color(.035f, .075f, .12f, .85f));
            Block(page, 1020, 130, 4, 220, cyan);
            Label(page, "LIVE TRANSMISSION    /    07", 1050, 155, 440, 24, 15, cyan, true);
            Label(page, "THE CITY NEVER\nGOES OFFLINE.", 1050, 195, 440, 90, 34, white, true);
            Label(page, "Keep moving. Keep the signal alive.", 1050, 304, 435, 26, 18, muted);
        }
        public void ShowHome()
        {
            NewPage("Home");
            Label(page, "HIGH ALTITUDE. HIGH STAKES.", 64, 132, 570, 25, 16, cyan, true);
            Label(page, "NEON\nRELAY", 57, 177, 610, 230, 100, white, true);
            Label(page, "An endless run above a sleeping planet.\nDeliver the signal. Outrun your best.", 64, 415, 570, 70, 21, muted);
            Button("BEGIN TRANSMISSION     >", 64, 516, 560, ShowModes, true);
            Button("PILOT LEADERBOARD", 64, 585, 270, ShowLeaderboard);
            Button("OPTIONS", 350, 585, 274, () => ShowOptions("Home"));
            Button("CREDITS", 64, 654, 270, ShowCredits);
            Button("EXIT", 350, 654, 274, Quit);
            Label(page, "SPACE  Jump    S / DOWN  Slide\nHOLD W / UP  Wall run    ESC  Pause", 64, 746, 570, 60, 17, muted);
            FieldCard();
        }
        public void ShowModes()
        {
            NewPage("Modes"); Heading("01 / FLIGHT CONFIGURATION", "Choose your pace.", "Every run starts with a guided route.\nYour mission: travel farther and collect data cells.");
            string[] details = { "8 m/s  /  More recovery sections", "10 m/s  /  Standard hazard density", "12 m/s  /  Expert reaction windows" };
            for (int i = 0; i < 3; i++)
            {
                int m = i; Button(RelayGame.Modes[i] + "     >", 64, 380 + 116 * i, 560, () => game.StartRun(m), i == 0);
                Label(page, details[i], 80, 442 + 116 * i, 530, 29, 17, muted);
            }
            Button("<  BACK", 64, 751, 240, ShowHome);
            Block(page, 1000, 120, 520, 390, new Color(.035f, .075f, .12f, .92f));
            Label(page, "YOUR FLIGHT MANUAL", 1035, 150, 450, 30, 18, cyan, true);
            Label(page, "01   SPACE / JUMP", 1035, 205, 450, 30, 23, white, true);
            Label(page, "Clear red junctions and rooftop gaps.", 1035, 244, 450, 30, 18, muted);
            Label(page, "02   S / SLIDE", 1035, 300, 450, 30, 23, white, true);
            Label(page, "Tap before red overhead laser gates.", 1035, 339, 450, 30, 18, muted);
            Label(page, "03   HOLD W / WALL RUN", 1035, 395, 450, 30, 23, game.World.Gold, true);
            Label(page, "Amber panels carry you across long gaps.", 1035, 434, 450, 50, 18, muted);
        }
        public void ShowHUD()
        {
            NewPage("HUD", false);
            Block(page, 36, 30, 1528, 102, new Color(.025f, .055f, .085f, .92f));
            Label(page, "NR / " + RelayGame.Modes[game.Mode], 62, 48, 270, 25, 16, cyan, true);
            status = Label(page, "TRANSMITTING", 62, 83, 300, 30, 21, white, true);
            Label(page, "SIGNAL SCORE", 420, 48, 220, 24, 14, muted);
            score = Label(page, "000000", 418, 76, 240, 45, 32, white, true);
            distance = Label(page, "0 m  /  0 CELLS", 720, 78, 360, 30, 20, white);
            Button("II  PAUSE", 1320, 53, 210, game.Pause);
            Block(page, 36, 794, 1528, 74, new Color(.025f, .055f, .085f, .92f));
            Label(page, "SPACE  Jump     S  Slide     HOLD W  Wall run", 62, 819, 730, 30, 19, muted);
            charge = Label(page, "MAG CHARGE", 1110, 807, 220, 23, 14, game.World.Gold, true);
            Block(page, 1110, 840, 420, 5, new Color(.22f, .26f, .25f));
            chargeBar = Block(page, 1110, 840, 420, 5, game.World.Gold);
        }
        void Update()
        {
            if (screen != "HUD") return;
            score.text = game.Score.ToString("D6"); distance.text = Mathf.FloorToInt(game.Distance) + " m  /  " + game.Cells + " DATA CELLS";
            status.text = game.WallRunning ? "MAGNETIC LINK" : game.Sliding ? "LOW PROFILE" : !game.Grounded ? "AIRBORNE" : "TRANSMITTING";
            chargeBar.rectTransform.sizeDelta = new Vector2(420 * Mathf.Clamp01(game.WallCharge / 1.15f), 5);
            charge.text = game.WallRunning ? "MAG LINK ACTIVE" : "MAG CHARGE";
        }
        public void ShowPause()
        {
            NewPage("Pause"); Heading("TRANSMISSION ON HOLD", "Take a breath.", "Your run is suspended. Resume when you're ready.");
            Button("RESUME RUN     >", 64, 391, 560, game.Resume, true);
            Button("OPTIONS", 64, 460, 560, () => ShowOptions("Pause"));
            Button("RESTART RUN", 64, 529, 560, () => game.StartRun(game.Mode));
            Button("ABANDON RUN", 64, 598, 560, game.ReturnHome);
            Label(page, "ESC to resume  /  " + game.Score + " points", 64, 725, 560, 40, 20, muted);
        }
        public void ShowOptions(string back)
        {
            optionsBack = back; NewPage("Options"); Heading("SYSTEM PREFERENCES", "Tune your signal.", "Audio settings are saved between sessions.");
            Volume("MUSIC", 392, game.Audio.MusicVolume, game.Audio.SetMusic);
            Volume("SOUND EFFECTS", 526, game.Audio.EffectsVolume, game.Audio.SetEffects);
            Button("<  BACK", 64, 720, 560, BackFromOptions, true);
        }
        void Volume(string name, float y, float initial, Action<float> change)
        {
            var label = Label(page, name + "   " + Mathf.RoundToInt(initial * 100) + "%", 64, y, 550, 30, 19, white, true);
            var r = Rect(name, page, 64, y + 49, 560, 35);
            var hit = r.gameObject.AddComponent<Image>(); hit.color = new Color(.085f, .14f, .18f);
            var slider = r.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
            var fillArea = Rect("Fill area", r, 10, 13, 540, 9);
            var fill = Block(fillArea, 0, 0, 540, 9, cyan); slider.fillRect = fill.rectTransform;
            var handleArea = Rect("Handle area", r, 10, 0, 540, 35);
            var handle = Block(handleArea, 0, 0, 16, 35, white); handle.raycastTarget = true;
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.value = initial;
            slider.onValueChanged.AddListener(v => { change(v); label.text = name + "   " + Mathf.RoundToInt(v * 100) + "%"; });
        }
        void BackFromOptions() { PlayerPrefs.Save(); if (optionsBack == "Pause") ShowPause(); else ShowHome(); }
        public void ShowResults()
        {
            NewPage("Results"); Heading(game.Failure, "End of the line.", "One more rooftop. One better run.\nYour next transmission is waiting.");
            Label(page, game.Score.ToString("N0"), 60, 358, 580, 95, 76, cyan, true);
            Label(page, Mathf.FloorToInt(game.Distance) + " METERS   /   " + game.Cells + " CELLS   /   " + RelayGame.Modes[game.Mode], 64, 463, 570, 30, 17, muted);
            var field = Rect("Pilot callsign", page, 64, 523, 340, 55); field.gameObject.AddComponent<Image>().color = new Color(.13f, .21f, .25f);
            pilot = field.gameObject.AddComponent<InputField>(); pilot.characterLimit = 16;
            var text = Label(field, "", 15, 0, 310, 55, 21, white); text.alignment = TextAnchor.MiddleLeft;
            pilot.textComponent = text;
            var placeholder = Label(field, "YOUR CALLSIGN", 15, 0, 310, 55, 19, muted); placeholder.alignment = TextAnchor.MiddleLeft;
            pilot.placeholder = placeholder;
            var save = Button("SAVE SCORE", 420, 523, 204, () => { if (game.SubmitScore(pilot.text)) { saveLabel.text = "SAVED"; pilot.interactable = false; } });
            saveLabel = save.GetComponentInChildren<Text>();
            Button("RUN AGAIN     >", 64, 612, 560, () => game.StartRun(game.Mode), true);
            Button("LEADERBOARD", 64, 681, 270, ShowLeaderboard);
            Button("MAIN MENU", 350, 681, 274, game.ReturnHome);
            Label(page, "Save your callsign before leaving this screen.", 64, 768, 565, 30, 16, muted);
        }
        public void ShowLeaderboard()
        {
            NewPage("Leaderboard"); Heading("PILOT RECORDS / TOP 10", "The long runners.", "Distance x 10 + data cells x 100.\nRecords persist on this computer.");
            Label(page, "#    CALLSIGN", 64, 365, 350, 26, 16, cyan, true);
            Label(page, "MODE / SCORE", 425, 365, 220, 26, 16, cyan, true);
            if (game.Records.entries.Count == 0) Label(page, "No transmissions yet. Make the first run.", 64, 425, 560, 80, 22, muted);
            for (int i = 0; i < game.Records.entries.Count; i++)
            {
                var r = game.Records.entries[i]; float y = 406 + i * 32;
                Label(page, (i + 1).ToString("D2") + "   " + r.pilot, 64, y, 355, 30, 18, white);
                Label(page, RelayGame.Modes[Mathf.Clamp(r.mode, 0, 2)] + " / " + r.score, 425, y, 230, 30, 16, muted);
            }
            Button("<  MAIN MENU", 64, 754, 560, game.ReturnHome, true);
        }
        public void ShowCredits()
        {
            NewPage("Credits"); Heading("THE PEOPLE BEHIND THE SIGNAL", "Transmission crew.", "A rooftop parkour prototype with an orbital twist.");
            Label(page, "ORIGINAL GAME CONCEPT", 64, 384, 560, 25, 15, cyan, true);
            Label(page, "Hayden Livermore", 64, 421, 560, 36, 27, white, true);
            Label(page, "IMPLEMENTATION & ORIGINAL ASSETS", 64, 486, 560, 25, 15, cyan, true);
            Label(page, "Built with OpenAI Codex and Unity 6.\nCode-generated art, particles, music and SFX.\nNo third-party art or audio packs.", 64, 525, 560, 100, 20, muted);
            Label(page, "ENGINE & INTERFACE", 64, 648, 560, 25, 15, cyan, true);
            Label(page, "Unity Engine / Unity UI / Input System", 64, 683, 570, 35, 19, muted);
            Button("<  BACK", 64, 754, 560, ShowHome);
        }
        public void Escape()
        {
            if (screen == "HUD") game.Pause();
            else if (screen == "Pause") game.Resume();
            else if (screen == "Options") BackFromOptions();
            else if (screen == "Modes" || screen == "Credits") ShowHome();
            else if (screen == "Leaderboard") game.ReturnHome();
        }
        static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

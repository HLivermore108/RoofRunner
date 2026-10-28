using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonRelay
{
    public enum RunState { Menu, Running, Paused, Finished }
    [Serializable] public class Record { public string pilot; public int score; public int mode; }
    [Serializable] public class RecordBook { public List<Record> entries = new List<Record>(); }

    // One simulation owns movement and collision, so thin obstacles cannot be tunneled at low frame rates.
    public sealed class RelayGame : MonoBehaviour
    {
        public static readonly string[] Modes = { "CADET", "COURIER", "PHANTOM" };
        public static readonly float[] Speeds = { 8f, 10f, 12f };
        public RunState State { get; private set; }
        public int Mode { get; private set; }
        public float Distance => Mathf.Max(0, Position.x - 3);
        public int Score => Mathf.FloorToInt(Distance * 10) + Cells * 100;
        public int Cells { get; private set; }
        public Vector2 Position { get; private set; }
        public float VerticalSpeed { get; private set; }
        public bool Grounded { get; private set; }
        public bool Sliding { get; private set; }
        public bool WallRunning { get; private set; }
        public float WallCharge { get; private set; } = 1.15f;
        public float Height => Sliding ? .55f : 1.45f;
        public string Failure { get; private set; }
        public RecordBook Records { get; private set; }
        public RelayWorld World { get; private set; }
        public RelayUI UI { get; private set; }
        public RelayAudio Audio { get; private set; }
        public bool Automated;
        float jumpBuffer, coyote, slideTime, dustTimer;
        bool submitted;

        void Awake()
        {
            Application.targetFrameRate = 120;
            try { Records = JsonUtility.FromJson<RecordBook>(PlayerPrefs.GetString("NeonRelay.Records", "{}")); }
            catch { Records = new RecordBook(); }
            if (Records == null) Records = new RecordBook();
            if (Records.entries == null) Records.entries = new List<Record>();
            Records.entries.RemoveAll(r => r == null);
            World = gameObject.AddComponent<RelayWorld>(); World.Initialize(this);
            Audio = gameObject.AddComponent<RelayAudio>(); Audio.Initialize();
            UI = gameObject.AddComponent<RelayUI>(); UI.Initialize(this);
            ReturnHome();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-neonSmoke") >= 0)
                gameObject.AddComponent<RelaySmoke>();
        }

        void Update()
        {
            if (Automated) return;
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) { UI.Escape(); return; }
            if (State != RunState.Running) return;
            bool jump = k != null && k.spaceKey.wasPressedThisFrame;
            bool slide = k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame);
            bool wall = k != null && (k.wKey.isPressed || k.upArrowKey.isPressed);
            Advance(Mathf.Min(Time.deltaTime, .1f), jump, slide, wall);
        }

        public void StartRun(int mode)
        {
            Mode = Mathf.Clamp(mode, 0, 2); Position = new Vector2(3, 0); VerticalSpeed = 0;
            Cells = 0; Grounded = true; Sliding = WallRunning = false; WallCharge = 1.15f;
            jumpBuffer = coyote = slideTime = dustTimer = 0; submitted = false;
            World.ResetCourse(Mode); State = RunState.Running; UI.ShowHUD(); Audio.Cue(0);
        }

        public void Advance(float delta, bool jump, bool slide, bool holdWall)
        {
            if (State != RunState.Running) return;
            if (jump) jumpBuffer = .14f;
            if (slide && Grounded) { slideTime = .78f; Audio.Cue(1); }
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta * 120));
            float dt = delta / steps;
            for (int i = 0; i < steps && State == RunState.Running; i++) Simulate(dt, holdWall);
        }

        void Simulate(float dt, bool holdWall)
        {
            World.Extend(Position.x);
            jumpBuffer -= dt; slideTime -= dt;
            Sliding = slideTime > 0 && Grounded;
            coyote = Grounded ? .10f : coyote - dt;
            bool onWall = World.AtWall(Position.x, Position.y);
            WallRunning = holdWall && onWall && !Grounded && WallCharge > 0;
            if (jumpBuffer > 0 && (coyote > 0 || WallRunning))
            {
                VerticalSpeed = 10; Grounded = false; coyote = 0; jumpBuffer = 0; slideTime = 0; Sliding = false;
                WallRunning = false; Audio.Cue(0); World.Burst(Position, World.Cyan, 9);
            }
            Vector2 previous = Position;
            float y = Position.y;
            if (WallRunning && VerticalSpeed <= 0)
            { VerticalSpeed = 0; y = Mathf.Max(.12f, y); WallCharge -= dt; }
            else VerticalSpeed -= 25 * dt;
            y += VerticalSpeed * dt;
            float x = Position.x + Speeds[Mode] * dt;
            Grounded = false;
            if (World.HasRoof(x) && previous.y >= -.04f && y <= 0 && VerticalSpeed <= 0)
            {
                y = 0; Grounded = true; VerticalSpeed = 0; WallCharge = 1.15f;
                if (previous.y > .04f) World.Burst(new Vector2(x, y), World.Cyan, 5);
            }
            Position = new Vector2(x, y);
            Sliding = Grounded && slideTime > 0;
            Rect body = new Rect(x - .28f, y + .04f, .56f, Height - .08f);
            if (World.HitsHazard(body)) { EndRun("SIGNAL LOST / IMPACT"); return; }
            if (y < -5) { EndRun("SIGNAL LOST / OFF GRID"); return; }
            if (World.Collect(body)) { Cells++; Audio.Cue(2); World.Burst(Position + Vector2.up, World.Gold, 12); }
            dustTimer -= dt;
            if (dustTimer <= 0 && (Grounded || WallRunning))
            { dustTimer = Sliding ? .035f : .085f; World.Burst(Position, WallRunning ? World.Gold : World.Cyan, 1); }
        }

        public void Pause() { if (State == RunState.Running) { State = RunState.Paused; UI.ShowPause(); } }
        public void Resume() { if (State == RunState.Paused) { State = RunState.Running; UI.ShowHUD(); } }
        public void ReturnHome() { State = RunState.Menu; World.ResetCourse(0); Position = new Vector2(3, 0); UI.ShowHome(); }
        public void EndRun(string reason)
        {
            if (State != RunState.Running) return;
            State = RunState.Finished; Failure = reason;
            World.Burst(Position + Vector2.up * .7f, World.Coral, 35); Audio.Cue(3); UI.ShowResults();
        }
        public bool SubmitScore(string pilot)
        {
            if (State != RunState.Finished || submitted) return false;
            pilot = (pilot ?? "").Trim(); if (pilot.Length == 0) pilot = "ANONYMOUS";
            if (pilot.Length > 16) pilot = pilot.Substring(0, 16);
            Records.entries.Add(new Record { pilot = pilot.ToUpperInvariant(), score = Score, mode = Mode });
            Records.entries.Sort((a, b) => b.score.CompareTo(a.score));
            if (Records.entries.Count > 10) Records.entries.RemoveRange(10, Records.entries.Count - 10);
            PlayerPrefs.SetString("NeonRelay.Records", JsonUtility.ToJson(Records)); PlayerPrefs.Save();
            submitted = true; return true;
        }
        void OnApplicationFocus(bool focus) { if (!focus && !Automated) Pause(); }
    }
}

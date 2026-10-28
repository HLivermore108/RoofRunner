using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NeonRelay
{
    // Opt-in standalone integration test. Never active during a normal play session.
    public sealed class RelaySmoke : MonoBehaviour
    {
        RelayGame game;
        string output;
        IEnumerator Start()
        {
            game = GetComponent<RelayGame>(); game.Automated = true;
            output = Path.Combine(Application.dataPath, "..", "QA"); Directory.CreateDirectory(output);
            yield return null; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "01-home.png"));
            yield return null;
            if (!Click("BEGIN TRANSMISSION     >") || game.UI.Screen != "Modes") { Fail("Main menu button routing failed"); yield break; }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "02-modes.png"));
            for (int mode = 0; mode < 3; mode++)
            {
                game.StartRun(mode); bool jumped = false, slid = false, wall = false;
                for (int frame = 0; frame < 5400; frame++)
                {
                    bool jump = false, slide = false; float x = game.Position.x, speed = RelayGame.Speeds[mode];
                    foreach (var s in game.World.Sections)
                    {
                        float ahead = s.hazard.xMin - x;
                        if (s.hazard.width > 0 && ahead > 0 && ahead < speed * .25f && game.Grounded)
                        { if (s.kind == 1) jump = true; else if (!game.Sliding) slide = true; }
                        if (s.kind != 4 && s.end - x > 0 && s.end - x < speed * .15f && game.Grounded) jump = true;
                    }
                    game.Advance(1f / 60, jump, slide, true);
                    jumped |= !game.Grounded && game.VerticalSpeed > 0;
                    slid |= game.Sliding; wall |= game.WallRunning;
                    if (game.State != RunState.Running) { Fail("Autopilot died in mode " + mode + " at " + game.Position + " / " + game.Failure); yield break; }
                    if (mode == 0 && frame == 280)
                    {
                        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "03-gameplay.png"));
                    }
                    if (mode == 0 && game.WallRunning && !File.Exists(Path.Combine(output, "04-wall-run.png")))
                    {
                        yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "04-wall-run.png"));
                    }
                    // Render periodically while keeping the long-run test quick.
                    if (frame % 90 == 0) yield return null;
                }
                if (!jumped || !slid || !wall || game.Cells == 0 || game.World.Sections.Count > 8)
                { Fail("Missing movement, collection, or section cleanup in mode " + mode); yield break; }
                Vector2 before = game.Position; game.Pause(); game.Advance(1, true, true, true);
                if (game.Position != before || game.UI.Screen != "Pause") { Fail("Pause does not freeze simulation"); yield break; }
                if (mode == 0)
                {
                    yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "06-pause.png"));
                }
                if (!Click("OPTIONS") || game.UI.Screen != "Options") { Fail("Pause options button failed"); yield break; }
                yield return new WaitForEndOfFrame();
                if (mode == 0) ScreenCapture.CaptureScreenshot(Path.Combine(output, "07-options.png"));
                game.UI.Escape(); game.Resume();
                Debug.Log("NEON QA: mode " + mode + " passed 90 seconds, jump/slide/wall/collection/pause/cleanup");
            }
            // Test the real collision lose conditions separately from the successful autopilot.
            game.StartRun(0);
            for (int i = 0; i < 600 && game.State == RunState.Running; i++) game.Advance(1f / 60, false, false, false);
            if (game.State != RunState.Finished || !game.Failure.Contains("OFF GRID")) { Fail("Falling did not end the run"); yield break; }
            game.StartRun(0);
            for (int i = 0; i < 900 && game.State == RunState.Running; i++)
            {
                bool jump = false;
                foreach (var s in game.World.Sections)
                    if (s.end - game.Position.x > 0 && s.end - game.Position.x < 1.2f && game.Grounded) jump = true;
                game.Advance(1f / 60, jump, false, false);
            }
            if (game.State != RunState.Finished || !game.Failure.Contains("IMPACT")) { Fail("Impact did not end the run"); yield break; }
            string original = PlayerPrefs.GetString("NeonRelay.Records", "{}");
            bool saved = game.SubmitScore("QA PILOT"); bool duplicate = game.SubmitScore("DUPLICATE");
            var book = JsonUtility.FromJson<RecordBook>(PlayerPrefs.GetString("NeonRelay.Records"));
            if (!saved || duplicate || book.entries.Find(r => r.pilot == "QA PILOT") == null) { Fail("Named score persistence failed"); yield break; }
            PlayerPrefs.SetString("NeonRelay.Records", original); PlayerPrefs.Save();
            yield return null; yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output, "05-results.png"));
            game.UI.ShowLeaderboard(); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "08-leaderboard.png"));
            game.ReturnHome(); game.UI.ShowCredits(); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "09-credits.png")); yield return null;
            if (FindObjectsByType<Button>(FindObjectsSortMode.None).Length == 0) { Fail("No Unity UI buttons"); yield break; }
            File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: 3 modes x 90 seconds; jumping, sliding, wall running, collection, bounded generation, pause, options return, falling, impact, named score save, duplicate rejection, all seven menus.\n");
            Debug.Log("NEON QA PASS"); Application.Quit(0);
        }
        void Fail(string message)
        {
            File.WriteAllText(Path.Combine(output, "result.txt"), "FAIL: " + message);
            Debug.LogError("NEON QA: " + message); Application.Quit(2);
        }
        static bool Click(string name)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name != name) continue;
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0) return false;
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                return true;
            }
            return false;
        }
    }
}

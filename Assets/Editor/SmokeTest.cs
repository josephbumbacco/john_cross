using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JohnCross.EditorTools
{
    // Headless play-mode check: host locally, confirm the player spawns, fires, takes damage,
    // dies and respawns. Exit code 0 = pass.
    // Run: -batchmode -executeMethod JohnCross.EditorTools.SmokeTest.Run  (no -quit)
    [InitializeOnLoad]
    public static class SmokeTest
    {
        const string ActiveKey = "JohnCross.SmokeTest.Active";
        const double TimeoutSeconds = 90;

        static int _phase;
        static double _phaseStart;
        static double _deadline;
        static bool _subscribed;

        static SmokeTest()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            Subscribe();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) WaitForIdleThenPlay();
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity");
            SessionState.SetBool(ActiveKey, true);
            Subscribe();
            WaitForIdleThenPlay();
        }

        // Entering play mode is deferred until the Editor is idle: a script reload mid-play
        // wipes Netcode's ILPP message registrations.
        static void WaitForIdleThenPlay()
        {
            _idleSince = -1;
            EditorApplication.update -= EnterWhenIdle;
            EditorApplication.update += EnterWhenIdle;
        }

        static double _idleSince;

        static void EnterWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _idleSince = -1;
                return;
            }
            if (_idleSince < 0) _idleSince = now;
            if (now - _idleSince < 3) return;

            EditorApplication.update -= EnterWhenIdle;
            EditorApplication.EnterPlaymode();
        }

        static void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup > _deadline) Finish(false, $"timed out in phase {_phase}");
            if (!EditorApplication.isPlaying) return;

            double now = EditorApplication.timeSinceStartup;
            var nm = NetworkManager.Singleton;
            var local = PlayerController.Local;

            switch (_phase)
            {
                case 0:
                    nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7777);
                    if (!nm.StartHost()) { Finish(false, "StartHost failed"); return; }
                    Next(now);
                    break;

                case 1:
                    if (local == null) return;
                    var health = local.GetComponent<Health>();
                    if (Health.All.Count != 1 || health.Current.Value != health.Max)
                    { Finish(false, $"bad spawn state: players={Health.All.Count} hp={health.Current.Value}"); return; }
                    Debug.Log($"[SmokeTest] Player spawned at {local.transform.position}, hp {health.Current.Value}");
                    typeof(PlayerController)
                        .GetMethod("ShootRpc", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(local, new object[] { (Vector2)local.transform.position, Vector2.right });
                    Next(now);
                    break;

                case 2:
                    if (Object.FindObjectsByType<Bullet>().Length == 0)
                    {
                        if (now - _phaseStart > 3) Finish(false, "no bullet spawned");
                        return;
                    }
                    Debug.Log("[SmokeTest] Bullet spawned.");
                    var h = local.GetComponent<Health>();
                    h.TakeDamage(20, 999);
                    if (h.Current.Value != 80) { Finish(false, $"expected 80 hp, got {h.Current.Value}"); return; }
                    h.TakeDamage(500, 999);
                    if (h.IsAlive || h.Deaths.Value != 1) { Finish(false, "player did not die"); return; }
                    Debug.Log("[SmokeTest] Damage and death OK.");
                    Next(now);
                    break;

                case 3:
                    var hp = local.GetComponent<Health>();
                    if (!hp.IsAlive)
                    {
                        if (now - _phaseStart > 5) Finish(false, "did not respawn");
                        return;
                    }
                    Debug.Log($"[SmokeTest] Respawned at {local.transform.position} with hp {hp.Current.Value}");
                    Finish(true, "all checks passed");
                    break;
            }
        }

        static void Next(double now)
        {
            _phase++;
            _phaseStart = now;
        }

        static void Finish(bool pass, string message)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(ActiveKey);
            if (pass) Debug.Log($"[SmokeTest] PASS: {message}");
            else Debug.LogError($"[SmokeTest] FAIL: {message}");
            EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}

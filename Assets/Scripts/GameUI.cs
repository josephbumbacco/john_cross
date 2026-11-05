using Unity.Netcode;
using UnityEngine;

namespace JohnCross
{
    // Prototype IMGUI: host/join menu before connecting, HUD and scoreboard while in a match.
    [RequireComponent(typeof(SessionManager))]
    public class GameUI : MonoBehaviour
    {
        SessionManager _sessions;
        string _joinCode = "";
        string _lanAddress = "127.0.0.1";
        GUIStyle _big;

        void Awake()
        {
            _sessions = GetComponent<SessionManager>();
        }

        void OnGUI()
        {
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            GUILayout.BeginArea(new Rect(12, 12, 320, Screen.height - 24));
            if (!nm.IsListening && !_sessions.Busy && _sessions.Session == null)
                DrawMenu();
            else
                DrawHud(nm);
            if (!string.IsNullOrEmpty(_sessions.Status))
                GUILayout.Label(_sessions.Status);
            GUILayout.EndArea();
        }

        void DrawMenu()
        {
            GUILayout.Label("john_cross", _big);

            GUILayout.Label("Online (Relay)");
            if (GUILayout.Button("Host online")) _sessions.HostOnline();
            GUILayout.BeginHorizontal();
            _joinCode = GUILayout.TextField(_joinCode, 12, GUILayout.Width(150));
            if (GUILayout.Button("Join by code")) _sessions.JoinOnline(_joinCode);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("LAN / local test");
            if (GUILayout.Button("Host LAN")) _sessions.HostLan();
            GUILayout.BeginHorizontal();
            _lanAddress = GUILayout.TextField(_lanAddress, 40, GUILayout.Width(150));
            if (GUILayout.Button("Join LAN")) _sessions.JoinLan(_lanAddress);
            GUILayout.EndHorizontal();
        }

        void DrawHud(NetworkManager nm)
        {
            var local = PlayerController.Local;
            if (local != null)
            {
                var health = local.GetComponent<Health>();
                GUILayout.Label(health.IsAlive ? $"HP {health.Current.Value}/{health.Max}" : "Respawning...", _big);
            }

            GUILayout.Label("Scoreboard");
            foreach (var h in Health.All)
            {
                string you = local != null && h.OwnerClientId == local.OwnerClientId ? " (you)" : "";
                GUILayout.Label($"Player {h.OwnerClientId}{you}   K {h.Kills.Value}  D {h.Deaths.Value}");
            }

            if (_sessions.Session != null)
                GUILayout.Label($"Join code: {_sessions.Session.Code}");
            if (GUILayout.Button("Leave", GUILayout.Width(100))) _sessions.Leave();
        }
    }
}

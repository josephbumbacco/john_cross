using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace JohnCross
{
    // Starts games either online (Multiplayer Services session over Relay, joined by code)
    // or directly over LAN / localhost for quick testing without Unity Cloud.
    public class SessionManager : MonoBehaviour
    {
        public const int MaxPlayers = 8;
        public const ushort LanPort = 7777;

        public ISession Session { get; private set; }
        public string Status { get; private set; } = "";
        public bool Busy { get; private set; }

        async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async void HostOnline()
        {
            await Run("Creating session...", async () =>
            {
                await EnsureSignedIn();
                var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
                Session = await MultiplayerService.Instance.CreateSessionAsync(options);
                Status = $"Hosting. Join code: {Session.Code}";
            });
        }

        public async void JoinOnline(string code)
        {
            await Run("Joining session...", async () =>
            {
                await EnsureSignedIn();
                Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                Status = $"Joined session {Session.Code}";
            });
        }

        public void HostLan()
        {
            Transport.SetConnectionData("0.0.0.0", LanPort);
            Status = NetworkManager.Singleton.StartHost() ? $"Hosting LAN on port {LanPort}" : "Failed to start host";
        }

        public void JoinLan(string address)
        {
            Transport.SetConnectionData(string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim(), LanPort);
            Status = NetworkManager.Singleton.StartClient() ? $"Connecting to {address}..." : "Failed to start client";
        }

        public async void Leave()
        {
            if (Session != null)
            {
                try { await Session.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning(e); }
                Session = null;
            }
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            Status = "";
        }

        static UnityTransport Transport => NetworkManager.Singleton.GetComponent<UnityTransport>();

        async Task Run(string pending, Func<Task> action)
        {
            if (Busy) return;
            Busy = true;
            Status = pending;
            try { await action(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Status = $"Error: {e.Message}";
            }
            finally { Busy = false; }
        }
    }
}

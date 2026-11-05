using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace JohnCross
{
    // Server-authoritative health, kills and deaths. On death the player is hidden briefly,
    // then the owner is told to respawn at a random point.
    public class Health : NetworkBehaviour
    {
        public static readonly List<Health> All = new List<Health>();

        [SerializeField] int maxHealth = 100;
        [SerializeField] float respawnDelay = 2f;

        public NetworkVariable<int> Current = new NetworkVariable<int>();
        public NetworkVariable<int> Kills = new NetworkVariable<int>();
        public NetworkVariable<int> Deaths = new NetworkVariable<int>();

        public int Max => maxHealth;
        public bool IsAlive => Current.Value > 0;

        SpriteRenderer _sprite;

        void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsServer) Current.Value = maxHealth;
            Current.OnValueChanged += OnHealthChanged;
            OnHealthChanged(0, Current.Value);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            Current.OnValueChanged -= OnHealthChanged;
        }

        void OnHealthChanged(int previous, int current)
        {
            _sprite.enabled = current > 0;
        }

        // Server only.
        public void TakeDamage(int amount, ulong attackerId)
        {
            if (!IsServer || !IsAlive) return;

            Current.Value = Mathf.Max(0, Current.Value - amount);
            if (Current.Value > 0) return;

            Deaths.Value++;
            foreach (var other in All)
                if (other.OwnerClientId == attackerId) other.Kills.Value++;

            StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(respawnDelay);
            RespawnRpc(Arena.RandomSpawnPoint());
            Current.Value = maxHealth;
        }

        [Rpc(SendTo.Owner)]
        void RespawnRpc(Vector2 position)
        {
            GetComponent<PlayerController>().Teleport(position);
        }
    }
}

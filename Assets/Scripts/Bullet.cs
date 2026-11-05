using Unity.Netcode;
using UnityEngine;

namespace JohnCross
{
    // Server-simulated projectile; position replicates through a server-authoritative NetworkTransform.
    public class Bullet : NetworkBehaviour
    {
        [SerializeField] float speed = 18f;
        [SerializeField] float lifetime = 1.5f;
        [SerializeField] float hitRadius = 0.5f;
        [SerializeField] int damage = 20;

        ulong _shooterId;
        Vector2 _direction;
        float _despawnAt;

        // Server only, before Spawn().
        public void Init(ulong shooterId, Vector2 direction)
        {
            _shooterId = shooterId;
            _direction = direction;
            transform.up = direction;
        }

        public override void OnNetworkSpawn()
        {
            GetComponent<SpriteRenderer>().color = Color.yellow;
            if (IsServer) _despawnAt = Time.time + lifetime;
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned) return;

            Vector2 pos = (Vector2)transform.position + _direction * (speed * Time.fixedDeltaTime);
            transform.position = pos;

            if (Time.time >= _despawnAt || !Arena.Contains(pos))
            {
                NetworkObject.Despawn();
                return;
            }

            foreach (var target in Health.All)
            {
                if (target.OwnerClientId == _shooterId || !target.IsAlive) continue;
                if (((Vector2)target.transform.position - pos).sqrMagnitude > hitRadius * hitRadius) continue;

                target.TakeDamage(damage, _shooterId);
                NetworkObject.Despawn();
                return;
            }
        }
    }
}

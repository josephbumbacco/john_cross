using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JohnCross
{
    // Top-down movement and aiming. The owning client moves its own player (owner-authoritative
    // NetworkTransform) and asks the server to fire.
    [RequireComponent(typeof(NetworkTransform), typeof(Health))]
    public class PlayerController : NetworkBehaviour
    {
        public static PlayerController Local { get; private set; }

        [SerializeField] float moveSpeed = 7f;
        [SerializeField] float fireCooldown = 0.2f;
        [SerializeField] float muzzleOffset = 0.7f;
        [SerializeField] Bullet bulletPrefab;

        InputAction _move;
        InputAction _fire;
        NetworkTransform _netTransform;
        float _nextFireClient;
        float _nextFireServer;

        void Awake()
        {
            _netTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            GetComponent<SpriteRenderer>().color = ColorFor(OwnerClientId);

            if (!IsOwner) return;

            Local = this;
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");
            _fire = new InputAction("Fire", InputActionType.Button);
            _fire.AddBinding("<Mouse>/leftButton");
            _fire.AddBinding("<Gamepad>/rightTrigger");
            _move.Enable();
            _fire.Enable();

            Teleport(Arena.RandomSpawnPoint());
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
            _move?.Dispose();
            _fire?.Dispose();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            Vector2 input = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            Vector2 pos = (Vector2)transform.position + input * (moveSpeed * Time.deltaTime);
            transform.position = Arena.Clamp(pos);

            Vector2 aim = AimDirection();
            if (aim.sqrMagnitude > 0.001f)
                transform.up = aim;

            if (_fire.IsPressed() && Time.time >= _nextFireClient)
            {
                _nextFireClient = Time.time + fireCooldown;
                ShootRpc(transform.position, transform.up);
            }
        }

        Vector2 AimDirection()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.rightStick.ReadValue().sqrMagnitude > 0.1f)
                return pad.rightStick.ReadValue().normalized;

            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return Vector2.zero;
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            return (world - (Vector2)transform.position).normalized;
        }

        [Rpc(SendTo.Server)]
        void ShootRpc(Vector2 origin, Vector2 direction)
        {
            if (Time.time < _nextFireServer || !GetComponent<Health>().IsAlive) return;
            _nextFireServer = Time.time + fireCooldown * 0.9f; // small slack for jitter

            direction = direction.normalized;
            var bullet = Instantiate(bulletPrefab, origin + direction * muzzleOffset, Quaternion.identity);
            bullet.Init(OwnerClientId, direction);
            bullet.NetworkObject.Spawn(destroyWithScene: true);
        }

        // Called on the owner only; the owner is the NetworkTransform authority.
        public void Teleport(Vector2 position)
        {
            _netTransform.Teleport(position, transform.rotation, transform.localScale);
        }

        public static Color ColorFor(ulong clientId)
        {
            float hue = (clientId * 0.618034f) % 1f;
            return Color.HSVToRGB(hue, 0.65f, 1f);
        }
    }
}

using UnityEngine;

namespace JohnCross
{
    // Smoothly follows the local player.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] float smoothTime = 0.12f;

        Vector3 _velocity;

        void LateUpdate()
        {
            var player = PlayerController.Local;
            if (player == null) return;

            var target = new Vector3(player.transform.position.x, player.transform.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, smoothTime);
        }
    }
}

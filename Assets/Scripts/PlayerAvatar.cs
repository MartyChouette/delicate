// PlayerAvatar.cs
using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    
    public class PlayerAvatar : NetworkBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 8f;
        public float maxVelocity = 10f;

        [Header("References")]
        public Rigidbody rb;
        public Camera playerCamera; // Only active on owner.
        public EmotionState emotionState;

        // Cached move input sent from the owner → to server.
        private Vector2 _moveInput;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                // Enable camera only for local player.
                if (playerCamera != null)
                    playerCamera.gameObject.SetActive(true);
            }
            else
            {
                if (playerCamera != null)
                    playerCamera.gameObject.SetActive(false);
            }
        }

        [ServerRpc]
        public void SubmitMoveInputServerRpc(Vector2 move)
        {
            // Normalize to be safe
            if (move.sqrMagnitude > 1f)
                move = move.normalized;

            _moveInput = move;
        }

        /// <summary>
        /// Called locally on owner to rotate the camera; no need to sync this via server.
        /// </summary>
        public void ApplyLookInputLocal(Vector2 lookInput, float sensitivity, float deltaTime)
        {
            if (!IsOwner || playerCamera == null) return;

            Vector3 euler = transform.eulerAngles;
            euler.y += lookInput.x * sensitivity * deltaTime;

            // Pitch is on camera
            Vector3 camEuler = playerCamera.transform.localEulerAngles;
            float pitch = camEuler.x;
            // convert to -180..180:
            if (pitch > 180f) pitch -= 360f;
            pitch -= lookInput.y * sensitivity * deltaTime;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
            camEuler.x = pitch;

            transform.eulerAngles = euler;
            playerCamera.transform.localEulerAngles = camEuler;
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            // Basic WASD-style movement in capsule local space.
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 desiredVel = (forward * _moveInput.y + right * _moveInput.x) * moveSpeed;

            // clamp
            if (desiredVel.magnitude > maxVelocity)
                desiredVel = desiredVel.normalized * maxVelocity;

            Vector3 vel = rb.linearVelocity;
            Vector3 velChange = desiredVel - new Vector3(vel.x, 0f, vel.z);

            rb.AddForce(new Vector3(velChange.x, 0f, velChange.z), ForceMode.VelocityChange);
        }
    }
}

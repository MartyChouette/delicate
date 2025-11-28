using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    /// <summary>
    /// On the owner, raycasts from the camera to see what they're looking at.
    /// Sends a focus point to the server so hands can be attracted toward it.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerFocusTarget : NetworkBehaviour
    {
        public PlayerAvatar avatar;
        public PlayerHandController handController;

        [Header("Focus Settings")]
        public float maxFocusDistance = 5f;
        public LayerMask focusMask; // usually same as grabMask
        public float updateInterval = 0.05f;

        private float _timer;

        private void Awake()
        {
            if (avatar == null)
                avatar = GetComponent<PlayerAvatar>();
            if (handController == null)
                handController = GetComponent<PlayerHandController>();
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (avatar.playerCamera == null || handController == null) return;

            _timer += Time.deltaTime;
            if (_timer < updateInterval)
                return;
            _timer = 0f;

            Ray ray = new Ray(avatar.playerCamera.transform.position, avatar.playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, maxFocusDistance, focusMask, QueryTriggerInteraction.Ignore))
            {
                handController.SetFocusPointServerRpc(hit.point, true);
            }
            else
            {
                handController.SetFocusPointServerRpc(Vector3.zero, false);
            }
        }
    }
}
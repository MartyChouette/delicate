// Magnet.cs
using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    public class Magnet : NetworkBehaviour
    {
        public MagnetWordId wordId;
        public Rigidbody rb;

        [Tooltip("Layers that can receive this magnet (box, player bodies).")]
        public LayerMask stickMask;

        [Tooltip("Force threshold above which collisions can strip this magnet off.")]
        public float stripImpulseThreshold = 10f;

        private bool _isStuck;
        private Transform _stuckTo;
        private Vector3 _localPos;
        private Quaternion _localRot;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            if (_isStuck && _stuckTo != null)
            {
                // Follow parent transform if stuck.
                transform.position = _stuckTo.TransformPoint(_localPos);
                transform.rotation = _stuckTo.rotation * _localRot;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer) return;

            if (!_isStuck)
            {
                // Try to stick to metal surface.
                if (((1 << collision.gameObject.layer) & stickMask.value) != 0)
                {
                    TryStickTo(collision);
                }
            }
            else
            {
                // Already stuck: check if this collision is strong enough to strip
                if (collision.impulse.magnitude > stripImpulseThreshold)
                {
                    Unstick();
                }
            }
        }

        private void TryStickTo(Collision collision)
        {
            // Get contact point & normal
            ContactPoint contact = collision.GetContact(0);
            Transform target = collision.transform;

            // Determine local offset for stable follow
            _stuckTo = target;
            _localPos = target.InverseTransformPoint(contact.point);
            _localRot = Quaternion.identity; // you can align to surface normal if you like

            _isStuck = true;
            rb.isKinematic = true;

            // Notify attached entity if it has MagnetAttachPoint
            var attachPoint = collision.collider.GetComponentInParent<MagnetAttachPoint>();
            if (attachPoint != null)
            {
                attachPoint.RegisterMagnetServer(this);
            }
        }

        public void Unstick()
        {
            if (!_isStuck) return;

            _isStuck = false;
            rb.isKinematic = false;

            if (_stuckTo != null)
            {
                var attachPoint = _stuckTo.GetComponentInParent<MagnetAttachPoint>();
                if (attachPoint != null)
                {
                    attachPoint.UnregisterMagnetServer(this);
                }
            }

            _stuckTo = null;
        }
    }
}

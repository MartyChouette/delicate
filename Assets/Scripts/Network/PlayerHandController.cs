// PlayerHandController.cs
using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    /// <summary>
    /// Controls left/right hand spheres, grabbing and locking, plus magnet tossing
    /// and subtle attraction toward a "focus point" the player is looking at.
    /// </summary>
    public class PlayerHandController : NetworkBehaviour
    {
        [Header("References")]
        public PlayerAvatar avatar;
        public Transform leftHand;
        public Transform rightHand;
        public Rigidbody leftHandRb;
        public Rigidbody rightHandRb;

        [Header("Hand Settings")]
        public float handDistance = 1.2f;
        public float handHeight = 1.2f;
        public float handSpring = 200f;
        public float handDamping = 25f;
        public float grabRange = 2.5f;
        public LayerMask grabMask;

        [Header("Magnet Toss Settings")]
        public float tossForce = 6f;

        [Header("Focus Attraction Settings")]
        [Tooltip("How strongly hands are attracted toward focus point (when in range).")]
        public float focusAttractionStrength = 4f;

        [Tooltip("Max distance from body where focus attraction applies.")]
        public float focusMaxDistance = 4f;

        private bool _leftPressed;
        private bool _rightPressed;
        private bool _leftLocked;
        private bool _rightLocked;

        private FixedJoint _leftJoint;
        private FixedJoint _rightJoint;

        private Magnet _leftHeldMagnet;
        private Magnet _rightHeldMagnet;

        // Focus point is server-side; all hand physics runs on server.
        private bool _hasFocus;
        private Vector3 _focusPoint;

        private void Awake()
        {
            if (avatar == null)
                avatar = GetComponent<PlayerAvatar>();

            if (leftHand != null && leftHandRb == null)
                leftHandRb = leftHand.GetComponent<Rigidbody>();
            if (rightHand != null && rightHandRb == null)
                rightHandRb = rightHand.GetComponent<Rigidbody>();
        }

        // ───────────────────── Input hooks ─────────────────────

        public void SetHandPressed(HandSide side, bool pressed)
        {
            if (side == HandSide.Left)
                _leftPressed = pressed;
            else
                _rightPressed = pressed;

            if (!pressed)
            {
                if (side == HandSide.Left && !_leftLocked)
                    ReleaseHandServerRpc(HandSide.Left);
                if (side == HandSide.Right && !_rightLocked)
                    ReleaseHandServerRpc(HandSide.Right);
            }
            else
            {
                TryGrabClosestServerRpc(side);
            }
        }

        public void ToggleLockHand(HandSide side)
        {
            if (side == HandSide.Left)
            {
                _leftLocked = !_leftLocked;
                if (!_leftLocked && !_leftPressed)
                    ReleaseHandServerRpc(HandSide.Left);
            }
            else
            {
                _rightLocked = !_rightLocked;
                if (!_rightLocked && !_rightPressed)
                    ReleaseHandServerRpc(HandSide.Right);
            }
        }

        public void ToggleLockBothHands()
        {
            _leftLocked = !_leftLocked;
            _rightLocked = _leftLocked;

            if (!_leftLocked && !_leftPressed)
                ReleaseHandServerRpc(HandSide.Left);
            if (!_rightLocked && !_rightPressed)
                ReleaseHandServerRpc(HandSide.Right);
        }
        // Focus from PlayerFocusTarget
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetFocusPointServerRpc(Vector3 point, bool hasFocus)
        {
            _hasFocus = hasFocus;
            _focusPoint = point;
        }

        // ───────────────────── Server-side grabbing ─────────────────────

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TryGrabClosestServerRpc(HandSide side)
        {
            Transform handTf = side == HandSide.Left ? leftHand : rightHand;
            if (handTf == null) return;

            var cam = avatar.playerCamera;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, grabRange, grabMask, QueryTriggerInteraction.Ignore))
            {
                var hitRb = hit.rigidbody;
                if (hitRb == null) return;

                Magnet mag = hitRb.GetComponent<Magnet>();
                if (mag != null)
                {
                    mag.Unstick();
                }

                FixedJoint joint = handTf.GetComponent<FixedJoint>();
                if (joint != null)
                    Destroy(joint);

                joint = handTf.gameObject.AddComponent<FixedJoint>();
                joint.connectedBody = hitRb;
                joint.breakForce = 2000f;
                joint.breakTorque = 2000f;

                if (side == HandSide.Left)
                {
                    _leftJoint = joint;
                    _leftHeldMagnet = mag;
                }
                else
                {
                    _rightJoint = joint;
                    _rightHeldMagnet = mag;
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseHandServerRpc(HandSide side)
        {
            Transform handTf = side == HandSide.Left ? leftHand : rightHand;
            if (handTf == null) return;

            var joint = handTf.GetComponent<FixedJoint>();
            if (joint != null)
                Destroy(joint);

            if (side == HandSide.Left)
            {
                _leftJoint = null;
                _leftHeldMagnet = null;
            }
            else
            {
                _rightJoint = null;
                _rightHeldMagnet = null;
            }
        }

        // ───────────────────── Magnet toss ─────────────────────

        public void RequestTossMagnet()
        {
            if (!IsOwner) return;
            TossMagnetServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void TossMagnetServerRpc()
        {
            Magnet mag = _leftHeldMagnet != null ? _leftHeldMagnet : _rightHeldMagnet;
            if (mag == null) return;

            Transform handTf;
            if (mag == _leftHeldMagnet)
            {
                handTf = leftHand;
                ClearHandJoint(HandSide.Left);
            }
            else
            {
                handTf = rightHand;
                ClearHandJoint(HandSide.Right);
            }

            mag.Unstick();
            mag.rb.linearVelocity = Vector3.zero;
            mag.rb.angularVelocity = Vector3.zero;

            Vector3 forward = avatar.transform.forward;
            Vector3 dir = (forward * 0.8f + Vector3.up * 0.5f).normalized;

            mag.transform.position = handTf.position;
            mag.rb.AddForce(dir * tossForce, ForceMode.VelocityChange);
        }


        private void ClearHandJoint(HandSide side)
        {
            Transform handTf = side == HandSide.Left ? leftHand : rightHand;
            if (handTf == null) return;

            var joint = handTf.GetComponent<FixedJoint>();
            if (joint != null)
                Destroy(joint);

            if (side == HandSide.Left)
            {
                _leftJoint = null;
                _leftHeldMagnet = null;
            }
            else
            {
                _rightJoint = null;
                _rightHeldMagnet = null;
            }
        }

        // ───────────────────── Hand idle spring + focus attraction ─────────────────────

        private void FixedUpdate()
        {
            if (!IsServer) return;

            UpdateHandTarget(leftHand, leftHandRb, true);
            UpdateHandTarget(rightHand, rightHandRb, false);
        }

        private void UpdateHandTarget(Transform hand, Rigidbody handRb, bool isLeft)
        {
            if (hand == null || handRb == null) return;

            var joint = hand.GetComponent<FixedJoint>();
            if (joint != null) return; // attached, don't move with springs

            Vector3 bodyPos = avatar.transform.position + Vector3.up * handHeight;
            Vector3 side = avatar.transform.right * (isLeft ? -1f : 1f);

            // Base forward position
            Vector3 targetPos = bodyPos + side * 0.5f + avatar.transform.forward * handDistance;

            // Focus attraction: bias target toward focus point, if within radius
            if (_hasFocus)
            {
                Vector3 focus = _focusPoint;
                focus.y = bodyPos.y; // keep hands at roughly same height

                float dist = Vector3.Distance(bodyPos, focus);
                if (dist <= focusMaxDistance)
                {
                    float t = Mathf.Clamp01(1f - dist / focusMaxDistance);
                    float weight = t * focusAttractionStrength * Time.fixedDeltaTime;
                    targetPos = Vector3.Lerp(targetPos, focus, weight);
                }
            }

            Vector3 toTarget = targetPos - hand.position;
            Vector3 desiredVel = toTarget * handSpring;
            Vector3 force = desiredVel - handRb.linearVelocity * handDamping;

            handRb.AddForce(force, ForceMode.Acceleration);
        }
    }
}

// BoxEmotionApplier.cs
using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoxEmotionApplier : NetworkBehaviour
    {
        public EmotionState emotionState;
        public MagnetAttachPoint magnetAttach;
        public Rigidbody rb;

        [Header("Abandonment Settings")]
        public float baseDriftStrength = 5f;

        [Header("Denial Settings")]
        public Renderer[] renderersToHide;
        public Collider[] collidersToToggle;

        private float _denialTimer;

        private void Awake()
        {
            if (rb == null)
                rb = GetComponent<Rigidbody>();
            if (emotionState == null)
                emotionState = GetComponent<EmotionState>();
            if (magnetAttach == null)
                magnetAttach = GetComponentInChildren<MagnetAttachPoint>();
        }

        private void FixedUpdate()
        {
            if (!IsServer || emotionState == null) return;

            float dt = Time.fixedDeltaTime;

            // -----------------------------------------
            // 1) ABANDONMENT DRIFT — away from players
            // -----------------------------------------
            float targetAbandon = 0.3f;
            Vector3 driftDir = GetAbandonmentDirection();

            // Magnets attached to the box modify abandonment
            if (magnetAttach != null)
            {
                foreach (var mag in magnetAttach.Magnets)
                {
                    switch (mag.wordId)
                    {
                        case MagnetWordId.Stay:
                        case MagnetWordId.DontLeave:
                            targetAbandon *= 0.3f;
                            break;

                        case MagnetWordId.Safe:
                            targetAbandon *= 0.6f;
                            break;
                    }
                }
            }

            emotionState.LerpAbandonment(targetAbandon, dt);

            float abandon = emotionState.abandonmentIntensity.Value;
            if (emotionState.AbandonmentDef != null && abandon > 0.01f)
            {
                float strength = baseDriftStrength * abandon;
                rb.AddForce(driftDir * strength, ForceMode.Acceleration);
            }


            // -----------------------------------------
            // 2) DENIAL – phasing/hiding
            // -----------------------------------------
            if (emotionState.DenialDef != null)
            {
                float denial = emotionState.denialIntensity.Value;
                if (denial > 0.05f)
                {
                    _denialTimer += dt;
                    float interval = Mathf.Max(0.5f, emotionState.DenialDef.phaseIntervalSeconds / Mathf.Max(denial, 0.01f));

                    if (_denialTimer >= interval)
                    {
                        _denialTimer = 0f;
                        ToggleDenialPhase();
                    }
                }
            }


            // -----------------------------------------
            // 3) ANGER – random physical jolt
            // -----------------------------------------
            if (emotionState.AngerDef != null)
            {
                float anger = emotionState.angerIntensity.Value;
                if (anger > 0.1f)
                {
                    float chance = 0.3f * anger * dt;
                    if (Random.value < chance)
                    {
                        Vector3 impulse = new Vector3(
                            Random.Range(-1f, 1f),
                            0f,
                            Random.Range(-1f, 1f)
                        ).normalized * emotionState.AngerDef.randomImpulseStrength;

                        rb.AddForce(impulse, ForceMode.Impulse);
                    }
                }
            }
        }

        // -------------------------------------------------------------------
        // Helper: drift direction is AWAY from average player position
        // -------------------------------------------------------------------
        private Vector3 GetAbandonmentDirection()
        {
            // New API:
            var players = UnityEngine.Object.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0)
                return Vector3.zero;

            Vector3 avg = Vector3.zero;
            foreach (var p in players)
                avg += p.transform.position;

            avg /= players.Length;

            Vector3 dir = (transform.position - avg);
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.01f)
                dir = transform.forward;

            return dir.normalized;
        }


        // -------------------------------------------------------------------
        // Helper: toggles renderers & colliders for denial "phasing"
        // -------------------------------------------------------------------
        private void ToggleDenialPhase()
        {
            bool currentlyEnabled =
                (collidersToToggle.Length == 0 || collidersToToggle[0].enabled);

            bool newEnabled = !currentlyEnabled;

            foreach (var c in collidersToToggle)
                c.enabled = newEnabled;

            foreach (var r in renderersToHide)
                r.enabled = newEnabled;
        }
    }
}

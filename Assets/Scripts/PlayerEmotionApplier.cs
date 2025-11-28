using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerEmotionApplier : NetworkBehaviour
    {
        public PlayerAvatar avatar;
        public EmotionState emotionState;
        public MagnetAttachPoint magnetAttach;

        [Header("Fear Physics")]
        public float swayTorque = 5f;
        public float stumbleImpulse = 2f;

        [Header("Helpers")]
        public float nearOtherPlayerRadius = 4f;

        private Rigidbody _rb;

        private void Awake()
        {
            if (avatar == null)
                avatar = GetComponent<PlayerAvatar>();
            if (emotionState == null)
                emotionState = GetComponent<EmotionState>();
            if (magnetAttach == null)
                magnetAttach = GetComponentInChildren<MagnetAttachPoint>();

            _rb = avatar.rb;
        }

        private void FixedUpdate()
        {
            if (!IsServer || emotionState == null) return;

            float dt = Time.fixedDeltaTime;

            // 1) Decide target fear based on distance to nearest other player.
            float targetFear = 0.3f; // base
            float nearest = GetNearestOtherPlayerDistance();
            if (nearest > nearOtherPlayerRadius)
                targetFear = 0.8f; // far away = more fear
            else
                targetFear = 0.25f;

            // 2) Magnets on this player can reduce fear.
            if (magnetAttach != null)
            {
                foreach (var mag in magnetAttach.Magnets)
                {
                    switch (mag.wordId)
                    {
                        case MagnetWordId.Warmth:
                            targetFear *= 0.4f; // warmth calms
                            break;
                        case MagnetWordId.Help:
                            targetFear *= 0.7f;
                            break;
                        case MagnetWordId.Sorry:
                            targetFear *= 0.9f;
                            break;
                    }
                }
            }

            emotionState.LerpFear(targetFear, dt);

            // 3) Apply physical effects from fear + anger.

            float fear = emotionState.fearIntensity.Value;
            float anger = emotionState.angerIntensity.Value;

            // Slight body sway from fear.
            if (emotionState.FearDef != null && fear > 0.01f)
            {
                float maxTilt = emotionState.FearDef.maxBodyTiltDeg * Mathf.Deg2Rad;
                Vector3 randomAxis = new Vector3(0f, 0f, Random.Range(-1f, 1f));
                _rb.AddTorque(randomAxis * swayTorque * fear, ForceMode.Acceleration);
            }

            // Occasional stumble impulses.
            if (emotionState.FearDef != null && fear > 0.4f)
            {
                float chance = emotionState.FearDef.stumbleChancePerSecond * fear * dt;
                if (Random.value < chance)
                {
                    Vector3 sideways = avatar.transform.right * Random.Range(-1f, 1f);
                    _rb.AddForce(sideways.normalized * stumbleImpulse, ForceMode.VelocityChange);
                }
            }

            // Simple anger: extra push on velocity (could be tuned more later).
            if (emotionState.AngerDef != null && anger > 0.1f)
            {
                Vector3 randomImpulse = new Vector3(
                    Random.Range(-1f, 1f),
                    0f,
                    Random.Range(-1f, 1f)
                ).normalized * emotionState.AngerDef.randomImpulseStrength * anger * dt;
                _rb.AddForce(randomImpulse, ForceMode.Acceleration);
            }
        }

        private float GetNearestOtherPlayerDistance()
        {
            float nearest = float.MaxValue;
            foreach (var other in FindObjectsOfType<PlayerAvatar>())
            {
                if (other == avatar) continue;
                float d = Vector3.Distance(avatar.transform.position, other.transform.position);
                if (d < nearest) nearest = d;
            }
            return nearest == float.MaxValue ? nearest : nearest;
        }
    }
}

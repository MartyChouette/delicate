// MagnetAttachPoint.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EmotionBank
{
    /// <summary>
    /// Put this on the box + on a child of the player body that should receive magnets.
    /// It tracks magnets and can inform EmotionState.
    /// </summary>
    public class MagnetAttachPoint : NetworkBehaviour
    {
        public EmotionState emotionState;

        private readonly List<Magnet> _magnets = new();

        public IReadOnlyList<Magnet> Magnets => _magnets;

        private void Awake()
        {
            if (emotionState == null)
                emotionState = GetComponentInParent<EmotionState>();
        }

        public void RegisterMagnetServer(Magnet magnet)
        {
            if (!IsServer) return;
            if (!_magnets.Contains(magnet))
                _magnets.Add(magnet);

            // Here you can tell EmotionState about new magnet effects.
            // e.g., EmotionEffects.ApplyMagnetAdded(emotionState, magnet.wordId);
        }

        public void UnregisterMagnetServer(Magnet magnet)
        {
            if (!IsServer) return;
            if (_magnets.Contains(magnet))
                _magnets.Remove(magnet);

            // Here you can tell EmotionState about magnet removal.
            // e.g., EmotionEffects.ApplyMagnetRemoved(emotionState, magnet.wordId);
        }
    }
}
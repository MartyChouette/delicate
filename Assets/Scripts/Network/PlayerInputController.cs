using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmotionBank
{
    public class PlayerInputController : NetworkBehaviour
    {
        public PlayerAvatar avatar;
        public PlayerHandController handController;

        [Header("Look Settings")]
        public float lookSensitivity = 120f;

        private PlayerInput _playerInput;
        private Vector2 _lookInput;

        private double _lastLeftTapTime;
        private double _lastRightTapTime;
        private const double DoubleTapWindow = 0.25;

        private void Awake()
        {
            if (avatar == null)
                avatar = GetComponent<PlayerAvatar>();
            if (handController == null)
                handController = GetComponent<PlayerHandController>();

            _playerInput = GetComponent<PlayerInput>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (_playerInput == null)
                _playerInput = GetComponent<PlayerInput>();

            if (IsOwner)
            {
                _playerInput.enabled = true;

                // avoid double-subscribe
                _playerInput.onActionTriggered -= OnActionTriggered;
                _playerInput.onActionTriggered += OnActionTriggered;

                if (_playerInput.actions != null)
                {
                    _playerInput.actions.Enable();

                    const string mapName = "Gameplay";
                    var map = _playerInput.actions.FindActionMap(mapName, false);
                    if (map != null)
                        _playerInput.SwitchCurrentActionMap(mapName);
                }

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                if (_playerInput != null)
                {
                    _playerInput.onActionTriggered -= OnActionTriggered;
                    _playerInput.enabled = false;

                    if (_playerInput.actions != null)
                        _playerInput.actions.Disable();
                }
            }
        }

        public override void OnDestroy()
        {
            if (_playerInput != null)
                _playerInput.onActionTriggered -= OnActionTriggered;

            base.OnDestroy();
        }

        private void OnActionTriggered(InputAction.CallbackContext ctx)
        {
            if (!IsOwner) return;

            Debug.Log($"[PlayerInput] {ctx.action.name} - {ctx.phase}");

            var actionName = ctx.action.name;

            switch (actionName)
            {
                case "Move":
                    {
                        Vector2 move = ctx.ReadValue<Vector2>();
                        avatar.SubmitMoveInputServerRpc(move);
                        break;
                    }

                case "Look":
                    {
                        _lookInput = ctx.ReadValue<Vector2>();
                        break;
                    }

                case "LeftHand":
                    {
                        if (ctx.started)
                        {
                            double now = Time.timeAsDouble;
                            if (now - _lastLeftTapTime < DoubleTapWindow)
                                handController.ToggleLockHand(HandSide.Left);
                            _lastLeftTapTime = now;

                            handController.SetHandPressed(HandSide.Left, true);
                        }
                        else if (ctx.canceled)
                        {
                            handController.SetHandPressed(HandSide.Left, false);
                        }
                        break;
                    }

                case "RightHand":
                    {
                        if (ctx.started)
                        {
                            double now = Time.timeAsDouble;
                            if (now - _lastRightTapTime < DoubleTapWindow)
                                handController.ToggleLockHand(HandSide.Right);
                            _lastRightTapTime = now;

                            handController.SetHandPressed(HandSide.Right, true);
                        }
                        else if (ctx.canceled)
                        {
                            handController.SetHandPressed(HandSide.Right, false);
                        }
                        break;
                    }

                case "LockBothHands":
                    {
                        if (ctx.performed)
                            handController.ToggleLockBothHands();
                        break;
                    }

                case "Toss":
                    {
                        if (ctx.performed)
                            handController.RequestTossMagnet();
                        break;
                    }
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (avatar != null)
                avatar.ApplyLookInputLocal(_lookInput, lookSensitivity, Time.deltaTime);
        }
    }
}

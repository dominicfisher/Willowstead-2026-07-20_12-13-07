using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Willowstead.Input
{
    /// <summary>
    /// ScriptableObject that acts as a bridge between the Unity Input System and gameplay scripts.
    /// Exposes events that other scripts can subscribe to.
    /// </summary>
    [CreateAssetMenu(fileName = "InputReader", menuName = "Willowstead/Input/InputReader")]
    public class InputReader : ScriptableObject, InputSystem_Actions.IPlayerActions
    {
        // Gameplay Events
        public event Action<Vector2> MoveEvent = delegate { };
        public event Action InteractEvent = delegate { };
        public event Action InteractCanceledEvent = delegate { };
        public event Action AttackEvent = delegate { };
        public event Action AttackCanceledEvent = delegate { };
        public event Action SprintEvent = delegate { };
        public event Action SprintCanceledEvent = delegate { };
        public event Action PreviousEvent = delegate { };
        public event Action NextEvent = delegate { };

        /// <summary>
        /// When true, every gameplay-emitting callback (Move / Sprint / Attack /
        /// Interact / Previous / Next) early-returns BEFORE the event is invoked. Set by the dev console while open so background
        /// WASD, interact-E, hotbar-digits, etc. don't move the character, fire the
        /// farming tool, or open the Inventory/Shop overlay while the developer is
        /// typing a command.
        /// </summary>
        public static bool BlockGameplayInput;

        private InputSystem_Actions _inputActions;

        private void OnEnable()
        {
            EnableGameplayInput();
        }

        private void OnDisable()
        {
            DisableGameplayInput();
        }

        public void EnableGameplayInput()
        {
            if (_inputActions == null)
            {
                _inputActions = new InputSystem_Actions();
                _inputActions.Player.SetCallbacks(this);
            }

            _inputActions.Enable();
        }

        public Vector2 GetMoveInput()
        {
            if (BlockGameplayInput) return Vector2.zero;
            if (_inputActions == null) EnableGameplayInput();
            if (_inputActions != null && _inputActions.Player.enabled)
            {
                return _inputActions.Player.Move.ReadValue<Vector2>();
            }
            return Vector2.zero;
        }

        public void DisableGameplayInput()
        {
            if (_inputActions != null)
            {
                _inputActions.Disable();
            }
            _inputActions = null; // Clean up so it is re-initialized fresh next time
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (BlockGameplayInput) return;
            Vector2 direction = context.ReadValue<Vector2>();
            MoveEvent.Invoke(direction);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            // Not used in this 2D prototype, but required by the interface
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (BlockGameplayInput) return;
            if (context.phase == InputActionPhase.Performed)
            {
                AttackEvent.Invoke();
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                AttackCanceledEvent.Invoke();
            }
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (BlockGameplayInput) return;
            if (context.phase == InputActionPhase.Performed)
            {
                InteractEvent.Invoke();
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                InteractCanceledEvent.Invoke();
            }
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            // Not used in this prototype, but required by the interface
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            // Not used in this prototype, but required by the interface
        }

        public void OnPrevious(InputAction.CallbackContext context)
        {
            if (BlockGameplayInput) return;
            if (context.phase == InputActionPhase.Performed)
            {
                PreviousEvent.Invoke();
            }
        }

        public void OnNext(InputAction.CallbackContext context)
        {
            if (BlockGameplayInput) return;
            if (context.phase == InputActionPhase.Performed)
            {
                NextEvent.Invoke();
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (BlockGameplayInput) return;
            if (context.phase == InputActionPhase.Performed)
            {
                SprintEvent.Invoke();
            }
            else if (context.phase == InputActionPhase.Canceled)
            {
                SprintCanceledEvent.Invoke();
            }
        }
    }
}

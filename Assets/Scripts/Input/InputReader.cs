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
        public event Action SprintEvent = delegate { };
        public event Action SprintCanceledEvent = delegate { };
        public event Action PreviousEvent = delegate { };
        public event Action NextEvent = delegate { };

        private InputSystem_Actions _inputActions;

        private void OnEnable()
        {
            Debug.Log("[InputReader] OnEnable called automatically by Unity.");
            EnableGameplayInput();
        }

        private void OnDisable()
        {
            Debug.Log("[InputReader] OnDisable called.");
            DisableGameplayInput();
        }

        public void EnableGameplayInput()
        {
            if (_inputActions == null)
            {
                Debug.Log("[InputReader] Instantiating InputSystem_Actions wrapper and setting callbacks.");
                _inputActions = new InputSystem_Actions();
                _inputActions.Player.SetCallbacks(this);
            }
            
            _inputActions.Enable();
            Debug.Log("[InputReader] InputSystem_Actions asset enabled.");
        }

        public void DisableGameplayInput()
        {
            if (_inputActions != null)
            {
                _inputActions.Disable();
                Debug.Log("[InputReader] InputSystem_Actions asset disabled.");
            }
            _inputActions = null; // Clean up so it is re-initialized fresh next time
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            Vector2 direction = context.ReadValue<Vector2>();
            
            // Only log on performed/canceled to avoid spamming every frame, but log when values change
            if (context.performed || context.canceled)
            {
                Debug.Log($"[InputReader] OnMove called. Phase: {context.phase}, Value: {direction}");
            }

            MoveEvent.Invoke(direction);
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            // Not used in this 2D prototype, but required by the interface
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            Debug.Log($"[InputReader] OnAttack called. Phase: {context.phase}");
            if (context.phase == InputActionPhase.Performed)
            {
                AttackEvent.Invoke();
            }
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            Debug.Log($"[InputReader] OnInteract called. Phase: {context.phase}");
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
            if (context.phase == InputActionPhase.Performed)
            {
                PreviousEvent.Invoke();
            }
        }

        public void OnNext(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                NextEvent.Invoke();
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
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

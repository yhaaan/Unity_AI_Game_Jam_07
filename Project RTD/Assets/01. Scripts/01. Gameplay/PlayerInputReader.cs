using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private bool jumpPressed;
        private bool jumpReleased;

        public Vector2 Move { get; private set; }

        public bool JumpHeld { get; private set; }

        public void OnMove(InputValue value)
        {
            Move = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
        }

        public void OnJump(InputValue value)
        {
            bool isPressed = value.isPressed;

            if (isPressed && !JumpHeld)
            {
                jumpPressed = true;
            }
            else if (!isPressed && JumpHeld)
            {
                jumpReleased = true;
            }

            JumpHeld = isPressed;
        }

        public bool ConsumeJumpPressed()
        {
            bool wasPressed = jumpPressed;
            jumpPressed = false;
            return wasPressed;
        }

        public bool ConsumeJumpReleased()
        {
            bool wasReleased = jumpReleased;
            jumpReleased = false;
            return wasReleased;
        }

        private void OnDisable()
        {
            Move = Vector2.zero;
            JumpHeld = false;
            jumpPressed = false;
            jumpReleased = false;
        }
    }
}

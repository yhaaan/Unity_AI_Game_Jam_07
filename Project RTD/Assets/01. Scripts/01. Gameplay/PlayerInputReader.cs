using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private bool jumpPressed;

        public Vector2 Move { get; private set; }

        public void OnMove(InputValue value)
        {
            Move = Vector2.ClampMagnitude(value.Get<Vector2>(), 1f);
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed)
            {
                jumpPressed = true;
            }
        }

        public bool ConsumeJumpPressed()
        {
            bool wasPressed = jumpPressed;
            jumpPressed = false;
            return wasPressed;
        }

        private void OnDisable()
        {
            Move = Vector2.zero;
            jumpPressed = false;
        }
    }
}

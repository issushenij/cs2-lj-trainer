using System;
using System.Numerics;
using Raylib_cs;

namespace LJTrainer.Core
{
    public class InputManager
    {
        public static InputManager Instance { get; } = new();

        public bool CursorLocked { get; private set; } = false;
        public float RawDeltaX { get; private set; }
        public float RawDeltaY { get; private set; }
        
        // Mouse Yaw & Pitch in degrees (matched to CS2 standard)
        public float DeltaYawDegrees { get; private set; }
        public float DeltaPitchDegrees { get; private set; }

        public bool KeyA { get; private set; }
        public bool KeyD { get; private set; }
        public bool KeyW { get; private set; }
        public bool KeyS { get; private set; }
        public bool KeyJump { get; private set; }
        public bool KeyJumpPressed { get; private set; }
        public bool KeyDuck { get; private set; }
        public bool KeyRestart { get; private set; }

        public void SetCursorLock(bool locked)
        {
            CursorLocked = locked;
            if (locked)
            {
                Raylib.DisableCursor();
            }
            else
            {
                Raylib.EnableCursor();
            }
        }

        public void ToggleCursorLock()
        {
            SetCursorLock(!CursorLocked);
        }

        public void Update()
        {
            if (CursorLocked)
            {
                Vector2 delta = Raylib.GetMouseDelta();
                RawDeltaX = delta.X;
                RawDeltaY = delta.Y;

                // CS2 Standard m_yaw:
                // Moving mouse RIGHT (delta.X > 0) -> Yaw increases (turns RIGHT)
                // Moving mouse LEFT (delta.X < 0) -> Yaw decreases (turns LEFT)
                DeltaYawDegrees = RawDeltaX * AppConfig.Instance.Sensitivity * AppConfig.Instance.YawFactor;

                // Moving mouse UP (delta.Y < 0) -> Pitch increases (looks UP)
                // Moving mouse DOWN (delta.Y > 0) -> Pitch decreases (looks DOWN)
                DeltaPitchDegrees = -RawDeltaY * AppConfig.Instance.Sensitivity * AppConfig.Instance.YawFactor;
            }
            else
            {
                RawDeltaX = 0;
                RawDeltaY = 0;
                DeltaYawDegrees = 0;
                DeltaPitchDegrees = 0;
            }

            KeyA = Raylib.IsKeyDown(KeyboardKey.A);
            KeyD = Raylib.IsKeyDown(KeyboardKey.D);
            KeyW = Raylib.IsKeyDown(KeyboardKey.W);
            KeyS = Raylib.IsKeyDown(KeyboardKey.S);
            KeyJump = Raylib.IsKeyDown(KeyboardKey.Space);
            KeyJumpPressed = Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.GetMouseWheelMove() < 0 || Raylib.IsMouseButtonPressed(MouseButton.Left);
            KeyDuck = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.C);
            KeyRestart = Raylib.IsKeyPressed(KeyboardKey.R);
        }
    }
}

using System;
using System.Numerics;

namespace LJTrainer.Core
{
    public struct TickSample
    {
        public int TickIndex;
        public float Time;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Speed2D;
        public float Yaw;
        public float MouseDeltaYaw;
        public bool KeyA;
        public bool KeyD;
        public bool KeyW;
        public bool KeyCrouch;
        
        // Calculated telemetry
        public bool IsSync;
        public bool IsOverlap;
        public bool IsDeadAir;
        public bool IsBadAngle;
        public float Gain;
        public float Loss;
        public float TheoreticalMaxGain;
        public float GainEfficiency;
        public float AngRatio;
        public int StrafeIndex;
    }

    public class SourcePhysics
    {
        public static readonly SourcePhysics Instance = new();

        public Vector3 AccelerateGround(Vector3 velocity, Vector3 wishDir, float wishSpeed, float accel, float dt, float maxPreSpeed)
        {
            float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
            
            // Ground friction (Source PM_Friction)
            if (speed > 0.1f)
            {
                float friction = 5.2f;
                float stopSpeed = 100.0f;
                float control = speed < stopSpeed ? stopSpeed : speed;
                float drop = control * friction * dt;
                float newSpeed = MathF.Max(0, speed - drop);
                if (speed > 0)
                {
                    velocity.X *= (newSpeed / speed);
                    velocity.Y *= (newSpeed / speed);
                }
            }

            // Ground Accelerate (Source PM_Accelerate)
            if (wishDir != Vector3.Zero)
            {
                float currentSpeed = Vector3.Dot(velocity, wishDir);
                float addSpeed = wishSpeed - currentSpeed;
                if (addSpeed > 0)
                {
                    float accelSpeed = accel * wishSpeed * dt;
                    if (accelSpeed > addSpeed) accelSpeed = addSpeed;
                    velocity += accelSpeed * wishDir;
                }
            }

            // Cap ground pre-speed
            float finalSpeed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
            if (finalSpeed > maxPreSpeed)
            {
                velocity.X = (velocity.X / finalSpeed) * maxPreSpeed;
                velocity.Y = (velocity.Y / finalSpeed) * maxPreSpeed;
            }

            return velocity;
        }

        public (Vector3 newVelocity, float gain, float loss, float maxGain, float gainEff, bool isBadAngle, float angRatio) 
            AccelerateAir(Vector3 velocity, Vector3 wishDir, float wishSpeed, float accel, float dt, float actualDeltaYawDegrees)
        {
            float oldSpeed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
            float maxPossibleGain = accel * wishSpeed * dt;

            Vector3 vel2D = new(velocity.X, velocity.Y, 0);
            float currentSpeed = Vector3.Dot(vel2D, wishDir);
            float addSpeed = wishSpeed - currentSpeed;

            Vector3 newVel = velocity;
            if (addSpeed > 0 && wishDir != Vector3.Zero)
            {
                float accelSpeed = accel * wishSpeed * dt;
                if (accelSpeed > addSpeed) accelSpeed = addSpeed;
                newVel += accelSpeed * wishDir;
            }

            float newSpeed = MathF.Sqrt(newVel.X * newVel.X + newVel.Y * newVel.Y);
            float deltaSpeed = newSpeed - oldSpeed;

            float gain = deltaSpeed > 0 ? deltaSpeed : 0.0f;
            float loss = deltaSpeed < 0 ? MathF.Abs(deltaSpeed) : 0.0f;
            float gainEff = maxPossibleGain > 0 ? Math.Clamp((gain / maxPossibleGain) * 100.0f, 0.0f, 100.0f) : 0.0f;

            // Optimal Angle calculation:
            float optDeltaYawDeg = 0.0f;
            if (oldSpeed > 30.0f)
            {
                optDeltaYawDeg = (accel * wishSpeed * dt / oldSpeed) * (180.0f / MathF.PI);
            }

            float angRatio = 0.0f;
            bool isBadAngle = false;

            if (optDeltaYawDeg > 0.001f)
            {
                float absActualTurn = MathF.Abs(actualDeltaYawDegrees);
                angRatio = (absActualTurn - optDeltaYawDeg) / optDeltaYawDeg;
                angRatio = Math.Clamp(angRatio, -1.0f, 1.0f);

                if (absActualTurn < optDeltaYawDeg * 0.35f || loss > 0.05f)
                {
                    isBadAngle = true;
                }
            }

            return (newVel, gain, loss, maxPossibleGain, gainEff, isBadAngle, angRatio);
        }

        public static Vector3 GetWishDirection(float viewYawDegrees, bool keyA, bool keyD, bool keyW, bool keyS)
        {
            float forward = 0;
            float side = 0;

            if (keyW) forward += 1.0f;
            if (keyS) forward -= 1.0f;
            if (keyD) side += 1.0f; // Right
            if (keyA) side -= 1.0f; // Left

            if (forward == 0 && side == 0) return Vector3.Zero;

            float mag = MathF.Sqrt(forward * forward + side * side);
            forward /= mag;
            side /= mag;

            float yawRad = viewYawDegrees * (MathF.PI / 180.0f);
            Vector3 fwdVec = new(MathF.Cos(yawRad), MathF.Sin(yawRad), 0);
            Vector3 rightVec = new(MathF.Sin(yawRad), -MathF.Cos(yawRad), 0); // 90° Clockwise (Right)

            Vector3 wishDir = forward * fwdVec + side * rightVec;
            float wishLen = wishDir.Length();
            if (wishLen > 0.0001f)
            {
                wishDir /= wishLen;
            }

            return wishDir;
        }
    }
}

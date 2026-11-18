using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Sample solved positions, not commanded velocity: pushing into a wall is silent.
    internal struct TravelCadence
    {
        private float previousX, remaining;
        private bool initialized, travelling, inWater;

        internal bool Tick(float x, bool active, bool water, float maxDelta)
        {
            float delta = Mathf.Abs(x - previousX);
            previousX = x;
            bool valid = initialized && delta <= maxDelta;
            initialized = true;
            if (!valid || !active || delta < .0001f)
            {
                travelling = false;
                return false;
            }
            float stride = water ? 2.1f : 1.45f;
            if (!travelling || water != inWater) remaining = stride * .45f;
            inWater = water;
            travelling = true;
            remaining -= delta;
            if (remaining > 0) return false;
            remaining += stride;
            return true;
        }
    }
}

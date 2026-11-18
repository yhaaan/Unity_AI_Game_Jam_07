using System;
using UnityEngine;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ProjectRTD.Gameplay.EditModeTests")]

namespace ProjectRTD.Core
{
    internal static class JumpTakeoff
    {
        // Keep one contact from the mono foley recording, not the silence or a second gesture.
        // Prepared once at load, never from input/physics callbacks.
        internal static AudioClip FirstContact(AudioClip original)
        {
            if (!original || original.channels != 1 || original.samples == 0) return null;
            var data = new float[original.samples];
            if (!original.GetData(data, 0)) return null;
            int hz = original.frequency;
            float peak = 0f;
            foreach (float value in data) peak = Mathf.Max(peak, Mathf.Abs(value));
            if (peak < .001f) return null;
            float gate = Mathf.Max(.001f, peak * .065f);
            int onset = Array.FindIndex(data, v => Mathf.Abs(v) >= gate);
            if (onset < 0) return null;
            int start = Mathf.Max(0, onset - hz / 1000);
            int end = Mathf.Min(data.Length, start + Mathf.RoundToInt(hz * .12f));
            int lastContact = onset;
            for (int i = onset; i < end; i++)
            {
                if (Mathf.Abs(data[i]) >= gate * .55f) lastContact = i;
                if (i - lastContact >= hz / 100) { end = i; break; }
            }
            int length = end - start;
            var contact = new float[length];
            for (int i = 0; i < length; i++)
            {
                float attack = Mathf.Clamp01(i / (.001f * hz));
                float release = Mathf.Clamp01((length - 1 - i) / (.009f * hz));
                contact[i] = data[start + i] * attack * release;
            }
            var result = AudioClip.Create("Jump - paw push off", length, 1, hz, false);
            result.SetData(contact, 0);
            return result;
        }
    }
}

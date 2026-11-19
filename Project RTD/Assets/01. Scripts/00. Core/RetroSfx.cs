using System;
using UnityEngine;

namespace ProjectRTD.Core
{
    internal static class RetroSfx
    {
        private const int Rate = 22050;
        // Each note has its own attack/release, including notes in the goal arpeggio.
        private readonly struct Note
        {
            public readonly float seconds, startHz, endHz, noise;
            public Note(float seconds, float startHz, float endHz, float noise = 0f)
            { this.seconds = seconds; this.startHz = startHz; this.endHz = endHz; this.noise = noise; }
        }

        private static Note[] Score(Sfx cue) => cue switch
        {
            Sfx.Confirm => new[] { new Note(.05f, 523, 523), new Note(.10f, 784, 784) },
            Sfx.Jump => new[] { new Note(.15f, 240, 870) },
            Sfx.Land => new[] { new Note(.09f, 146, 52, .36f) },
            Sfx.Splash => new[] { new Note(.08f, 650, 250, .7f), new Note(.08f, 340, 810, .16f) },
            Sfx.Scare => new[] { new Note(.08f, 490, 1030, .12f), new Note(.10f, 950, 690) },
            Sfx.Defeat => new[] { new Note(.08f, 340, 180), new Note(.14f, 180, 64, .33f) },
            Sfx.PlateDown => new[] { new Note(.04f, 620, 230, .45f), new Note(.10f, 164, 94) },
            Sfx.PlateUp => new[] { new Note(.06f, 160, 270, .2f), new Note(.05f, 330, 460) },
            Sfx.Respawn => new[] { new Note(.13f, 500, 130), new Note(.22f, 190, 660) },
            Sfx.Goal => new[] { new Note(.10f, 523, 523), new Note(.10f, 659, 659), new Note(.12f, 784, 784), new Note(.32f, 1047, 1047) },
            Sfx.Summon => new[] { new Note(.06f, 260, 390), new Note(.06f, 330, 495), new Note(.18f, 440, 880) },
            Sfx.StepLeft => new[] { new Note(.06f, 120, 55, .33f) },
            Sfx.StepRight => new[] { new Note(.07f, 128, 64, .28f) },
            Sfx.Paddle => new[] { new Note(.12f, 230, 100, .68f) },
            Sfx.Glide => new[] { new Note(.14f, 260, 520, .8f) },
            _ => throw new ArgumentOutOfRangeException(nameof(cue))
        };

        public static AudioClip Create(Sfx cue)
        {
            if (cue == Sfx.Rustle) return CreateRustle();
            var notes = Score(cue);
            int length = 0;
            foreach (var note in notes) length += Mathf.RoundToInt(note.seconds * Rate);
            var samples = new float[length];
            var random = new System.Random(201 + (int)cue);
            int offset = 0;
            foreach (var note in notes)
            {
                float phase = 0f;
                int n = Mathf.RoundToInt(note.seconds * Rate);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    phase = Mathf.Repeat(phase + Mathf.Lerp(note.startHz, note.endHz, t) / Rate, 1f);
                    float triangle = 1f - 4f * Mathf.Abs(phase - .5f);
                    float wave = Mathf.Lerp(triangle, (float)random.NextDouble() * 2f - 1f, note.noise);
                    float envelope = Mathf.Min(1f, i / 64f) * (1f - t) * (1f - t);
                    samples[offset + i] = .48f * envelope * wave;
                }
                offset += n;
            }
            var clip = AudioClip.Create(cue.ToString(), length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Grass brushing past legs: a soft swish of airy noise with a scatter of tiny leaf ticks on top.
        /// No pitched tone, so it sits under the footsteps instead of competing with them.
        /// </summary>
        private static AudioClip CreateRustle()
        {
            const float seconds = .3f;
            int n = Mathf.RoundToInt(seconds * Rate);
            var samples = new float[n];
            var random = new System.Random(4711);
            float low = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float white = (float)random.NextDouble() * 2f - 1f;
                // band-pass-ish: high-pass by differencing, then soften with a one-pole low-pass
                float high = white - prev; prev = white;
                low += (high - low) * .35f;
                // swell in, long soft tail
                float swish = Mathf.Sin(Mathf.PI * Mathf.Pow(t, .55f)) * (1f - t * .4f);
                samples[i] = low * swish * .55f;
            }
            // leaf ticks: short bright grains scattered through the swish
            for (int g = 0; g < 9; g++)
            {
                int start = (int)(n * (.05f + .8f * (float)random.NextDouble()));
                int len = (int)(Rate * (.006f + .012f * (float)random.NextDouble()));
                float amp = .12f + .18f * (float)random.NextDouble();
                float p = 0f;
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float w = (float)random.NextDouble() * 2f - 1f;
                    float h = w - p; p = w;
                    float env = 1f - i / (float)len;
                    samples[start + i] += h * env * env * amp;
                }
            }
            var clip = AudioClip.Create(nameof(Sfx.Rustle), n, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

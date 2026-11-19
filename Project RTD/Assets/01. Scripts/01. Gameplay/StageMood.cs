using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    // A colour mood per stage, layered over the shared Retro TV look (higher volume priority, so only the
    // parameters set here change: tint, split toning, film warmth, exposure, vignette colour/strength).
    // Everything drawn by the camera takes it, including the curved HUD, so each room feels different.
    [DisallowMultipleComponent]
    public sealed class StageMood : MonoBehaviour
    {
        struct Mood
        {
            public string name;
            public Color filter, shadows, highlights, vignette;
            public float temperature, tint, exposure, balance, vignetteStrength;
        }

        static Color Hex(int rgb) => new((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);

        // Index = stage number - 1. Kept gentle so the HUD and the characters stay readable.
        static readonly Mood[] Moods =
        {
            // 1 High Jump: warm morning light coming into the lab.
            new() { name = "Morning", filter = Hex(0xFFF1DE), shadows = Hex(0x4A6C8C), highlights = Hex(0xFFC27A),
                    temperature = 22f, tint = 0f, exposure = 0.05f, balance = 10f, vignette = Hex(0x2A1606), vignetteStrength = 0.30f },
            // 2 Glide: airy, bright sky-blue breeze.
            new() { name = "Breeze", filter = Hex(0xE6F6FF), shadows = Hex(0x4F7FB0), highlights = Hex(0xD9FFF4),
                    temperature = -12f, tint = -4f, exposure = 0.15f, balance = 25f, vignette = Hex(0x0A1E33), vignetteStrength = 0.24f },
            // 3 Lightweight: soft pastel lavender, dreamy and floaty.
            new() { name = "Pastel", filter = Hex(0xF6E9FF), shadows = Hex(0x7A5FA8), highlights = Hex(0xFFD6EC),
                    temperature = 0f, tint = 12f, exposure = 0.10f, balance = 15f, vignette = Hex(0x26123A), vignetteStrength = 0.28f },
            // 4 Scientist: security alert, red emergency lighting.
            new() { name = "Alert", filter = Hex(0xFFE2DC), shadows = Hex(0x5A1020), highlights = Hex(0xFF9A7A),
                    temperature = 10f, tint = 6f, exposure = -0.10f, balance = -15f, vignette = Hex(0x4A0008), vignetteStrength = 0.40f },
            // 5 Swim: underwater teal.
            new() { name = "Aquarium", filter = Hex(0xDDF8FA), shadows = Hex(0x0E4B5C), highlights = Hex(0x9FF2E6),
                    temperature = -22f, tint = -8f, exposure = 0f, balance = 0f, vignette = Hex(0x02202A), vignetteStrength = 0.36f },
            // 6 Low Ceiling: cramped, dim basement with a sickly green cast.
            new() { name = "Basement", filter = Hex(0xE9F2DD), shadows = Hex(0x23301E), highlights = Hex(0xD6E6A0),
                    temperature = -6f, tint = -14f, exposure = -0.18f, balance = -25f, vignette = Hex(0x0A1006), vignetteStrength = 0.46f },
            // 7 Power: final furnace room, hot orange and gold.
            new() { name = "Furnace", filter = Hex(0xFFE6CC), shadows = Hex(0x5A1E0A), highlights = Hex(0xFFB347),
                    temperature = 30f, tint = 4f, exposure = 0.05f, balance = -5f, vignette = Hex(0x3A0E00), vignetteStrength = 0.40f },
            // 8 Drones: cold steel-blue lockdown.
            new() { name = "Lockdown", filter = Hex(0xDCE8FF), shadows = Hex(0x142A55), highlights = Hex(0xA8D4FF),
                    temperature = -28f, tint = 2f, exposure = -0.08f, balance = -10f, vignette = Hex(0x02081C), vignetteStrength = 0.44f },
            // 9 Final gauntlet: overloaded magenta-violet.
            new() { name = "Overdrive", filter = Hex(0xF6DCFF), shadows = Hex(0x3A0F52), highlights = Hex(0xFF9AD8),
                    temperature = 4f, tint = 22f, exposure = -0.05f, balance = -5f, vignette = Hex(0x1C0228), vignetteStrength = 0.42f },
            // 10 Escape: dawn breaking, gold light with deep blue shadows.
            new() { name = "Dawn", filter = Hex(0xFFF3D6), shadows = Hex(0x1F3A6E), highlights = Hex(0xFFD98A),
                    temperature = 18f, tint = -2f, exposure = 0.1f, balance = 20f, vignette = Hex(0x0A1430), vignetteStrength = 0.34f },
        };

        VolumeProfile profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (scene, _) => Attach(scene);
            Attach(SceneManager.GetActiveScene());
        }

        static void Attach(Scene scene)
        {
            int stage = StageNumber(scene.name);
            if (stage < 1 || stage > Moods.Length) return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<StageMood>()) return;
            var go = new GameObject("Stage Mood");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<StageMood>().Apply(Moods[stage - 1]);
        }

        static int StageNumber(string sceneName) =>
            sceneName.StartsWith("Stage") && int.TryParse(sceneName.Substring(5), out int n) ? n : 0;

        void Apply(Mood mood)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Stage Mood " + mood.name;

            var color = profile.Add<ColorAdjustments>();
            color.colorFilter.Override(mood.filter);
            color.postExposure.Override(mood.exposure);

            var split = profile.Add<SplitToning>();
            split.shadows.Override(mood.shadows);
            split.highlights.Override(mood.highlights);
            split.balance.Override(mood.balance);

            var white = profile.Add<WhiteBalance>();
            white.temperature.Override(mood.temperature);
            white.tint.Override(mood.tint);

            var vignette = profile.Add<Vignette>();
            vignette.color.Override(mood.vignette);
            vignette.intensity.Override(mood.vignetteStrength);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f; // above the Retro TV look (10)
            volume.sharedProfile = profile;
        }

        void OnDestroy()
        {
            if (!profile) return;
            foreach (var component in profile.components) Destroy(component);
            Destroy(profile);
        }
    }
}

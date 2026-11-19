using System;
using System.Collections;
using System.Linq;
using ProjectRTD.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// New-game intro, shown like an old film reel: 지지직 static → 3·2·1 film leader → the fusion tank show
    /// (the starting chimera: axolotl head, bear body, chicken legs) → static → first stage. Any key skips.
    /// The show is rendered far off-screen by its own camera at 16 fps into a texture shown through RTD/OldFilm.
    /// </summary>
    public sealed class FilmFusionIntro : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(0f, 4000f, 0f);
        static readonly Vector3 LeaderAt = new Vector3(60f, 4000f, 0f);
        const float FilmFps = 16f, LeaderStep = .62f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register() => GameStartIntro.Runner = Begin;

        static void Begin(Action then)
        {
            var settings = Resources.Load<FusionIntroSettings>("FusionTank/FusionIntroSettings");
            if (!Application.isPlaying || !settings || !settings.play || !settings.catalog) { then(); return; }
            GameObject go = null;
            try
            {
                GameStartIntro.Running = true;
                go = new GameObject("Film Fusion Intro");
                DontDestroyOnLoad(go);
                var intro = go.AddComponent<FilmFusionIntro>();
                intro.StartCoroutine(intro.Run(settings.catalog, then));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (go) Destroy(go);
                GameStartIntro.Running = false;
                then();
            }
        }

        Camera cam;
        RenderTexture rt;
        Material film;
        RawImage picture;
        Image black;
        AudioSource hiss, rattle;
        AudioClip hissClip, rattleClip;
        Transform leader, hand;
        TextMeshPro digit;
        FusionTankSequence show;
        float filmT, nextFrame;
        bool skip, showDone;

        IEnumerator Run(PartCatalog catalog, Action then)
        {
            BuildScreen();
            BuildLeader();
            BuildSound();
            show = new GameObject("Fusion Tank (intro)").AddComponent<FusionTankSequence>();
            show.transform.SetParent(transform, false);
            show.transform.position = Origin;
            show.UseExternalClock(catalog);

            // 1  static burst, picture already on the leader
            Aim(LeaderAt);
            yield return Static(.55f, 1f, .15f);

            // 2  film leader 3·2·1
            for (int n = 3; n >= 1 && !skip; n--)
            {
                digit.text = n.ToString();
                for (float a = 0f; a < LeaderStep && !skip; a += Dt())
                {
                    hand.localRotation = Quaternion.Euler(0f, 0f, -360f * a / LeaderStep);
                    yield return Frame();
                }
            }

            // 3  the fusion show
            if (!skip)
            {
                yield return Static(.12f, .7f, 0f);
                leader.gameObject.SetActive(false);
                Aim(Origin);
                show.Play(StartingBuild(catalog), () => showDone = true);
                int lastPhase = -1;
                float hold = 0f;
                while (!skip && hold < 1.1f)
                {
                    float dt = Dt();
                    show.Step(dt);
                    int p = show.PhaseIndex;
                    if (p != lastPhase && (p == 3 || p == 5)) { glitch = .16f; Hiss(.5f); }
                    lastPhase = p;
                    if (showDone) hold += dt;
                    yield return Frame();
                }
            }

            // 4  static out, to black, load the game
            yield return Static(.45f, 1f, 1f, true);
            black.color = Color.black;
            hiss.Stop(); rattle.Stop();
            GameStartIntro.Running = false;
            bool loaded = false;
            void OnLoaded(Scene s, LoadSceneMode m) => loaded = true;
            SceneManager.sceneLoaded += OnLoaded;
            then();
            for (float w = 0f; !loaded && w < 5f; w += Time.unscaledDeltaTime) yield return null;
            SceneManager.sceneLoaded -= OnLoaded;
            yield return null;
            for (float a = 0f; a < .3f; a += Time.unscaledDeltaTime) { black.color = new Color(0f, 0f, 0f, 1f - a / .3f); yield return null; }
            Destroy(gameObject);
        }

        static PlayerPartBuild StartingBuild(PartCatalog c)
        {
            AnimalPartDefinition Get(PartSlot s, PartAnimal a)
            {
                var all = c.ForSlot(s).ToArray();
                return all.FirstOrDefault(p => p.Animal == a) ?? (all.Length > 0 ? all[UnityEngine.Random.Range(0, all.Length)] : null);
            }
            return new PlayerPartBuild(Get(PartSlot.Head, PartAnimal.Axolotl), Get(PartSlot.Body, PartAnimal.Bear), Get(PartSlot.Legs, PartAnimal.Chicken));
        }

        // ------------------------------------------------------------------ timing
        float glitch;

        float Dt() => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        /// <summary>end of a display frame: skip input, film clock, re-render the reel at 16 fps</summary>
        IEnumerator Frame()
        {
            float dt = Dt();
            filmT += dt;
            if (filmT > .5f && AnyPress()) skip = true;
            glitch = Mathf.Max(0f, glitch - dt);
            film.SetFloat("_FilmT", filmT);
            if (glitch > 0f) film.SetFloat("_Static", .55f * glitch / .16f);
            if (filmT >= nextFrame) { nextFrame = filmT + 1f / FilmFps; cam.Render(); }
            yield return null;
        }

        IEnumerator Static(float seconds, float peak, float settle, bool fadeOut = false)
        {
            Hiss(peak);
            for (float a = 0f; a < seconds; a += Dt())
            {
                float u = a / seconds;
                float s = fadeOut ? Mathf.SmoothStep(.2f, peak, u) : Mathf.Lerp(peak, settle, u * u);
                film.SetFloat("_Static", s);
                film.SetFloat("_Fade", fadeOut ? 1f - Mathf.Clamp01((u - .6f) / .4f) : 1f);
                hiss.volume = .32f * s * GameVolume.Effects;
                yield return Frame();
            }
            film.SetFloat("_Static", fadeOut ? 1f : 0f);
            hiss.volume = 0f;
            if (!fadeOut && !rattle.isPlaying) rattle.Play();
        }

        static bool AnyPress()
        {
            var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
            return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame) ||
                   (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
        }

        void Aim(Vector3 at) { cam.transform.position = at + new Vector3(0f, 0f, -10f); nextFrame = 0f; }

        // ------------------------------------------------------------------ build
        void BuildScreen()
        {
            rt = new RenderTexture(1280, 720, 16) { name = "Film Reel" };
            cam = new GameObject("Film Camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.orthographic = true; cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0x9a, 0xa6, 0xe0, 0xff);
            cam.targetTexture = rt; cam.enabled = false;
            cam.nearClipPlane = .1f; cam.farClipPlane = 40f;

            var canvas = new GameObject("Film Screen", typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            canvas.gameObject.AddComponent<GraphicRaycaster>();     // swallow clicks on the title UI below

            var bg = new GameObject("Black", typeof(Image)).GetComponent<Image>();
            Stretch(bg.rectTransform, canvas.transform);
            bg.color = Color.black;

            picture = new GameObject("Reel", typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
            Stretch(picture.rectTransform, canvas.transform);
            var fit = picture.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9f;
            picture.texture = rt;
            picture.raycastTarget = false;
            film = new Material(Resources.Load<Shader>("FusionTank/OldFilm"));
            film.SetFloat("_Static", 1f);
            picture.material = film;

            black = new GameObject("Curtain", typeof(Image)).GetComponent<Image>();
            Stretch(black.rectTransform, canvas.transform);
            black.color = Color.clear;
            black.raycastTarget = false;
        }

        static void Stretch(RectTransform r, Transform parent)
        {
            r.SetParent(parent, false);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        static Sprite square, ring;
        static Sprite Square => square ? square : square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
        static Sprite Ring
        {
            get
            {
                if (ring) return ring;
                const int N = 256; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - .94f) * N / 2f / 3f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * a));
                }
                tex.SetPixels32(px); tex.Apply();
                return ring = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N / 2f);
            }
        }

        void BuildLeader()
        {
            leader = new GameObject("Film Leader").transform;
            leader.SetParent(transform, false);
            leader.position = LeaderAt;
            var ink = new Color(.12f, .1f, .09f);
            SpriteRenderer Add(Sprite s, Vector2 pos, Vector2 size, Color c, int order)
            {
                var sr = new GameObject("leader").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(leader, false);
                sr.transform.localPosition = pos;
                sr.transform.localScale = new Vector3(size.x / s.bounds.size.x, size.y / s.bounds.size.y, 1f);
                sr.sprite = s; sr.color = c; sr.sortingOrder = order;
                return sr;
            }
            Add(Square, Vector2.zero, new Vector2(20f, 11f), new Color(.78f, .76f, .72f), 0);
            Add(Square, Vector2.zero, new Vector2(20f, .05f), ink, 1);
            Add(Square, Vector2.zero, new Vector2(.05f, 11f), ink, 1);
            Add(Ring, Vector2.zero, new Vector2(8.6f, 8.6f), ink, 2);
            Add(Ring, Vector2.zero, new Vector2(7.2f, 7.2f), ink, 2);
            hand = new GameObject("hand").transform;
            hand.SetParent(leader, false);
            var h = Add(Square, new Vector2(0f, 2.15f), new Vector2(.09f, 4.3f), ink, 3);
            h.transform.SetParent(hand, false);
            digit = new GameObject("digit").AddComponent<TextMeshPro>();
            digit.transform.SetParent(leader, false);
            digit.alignment = TextAlignmentOptions.Center;
            digit.fontSize = 48f; digit.color = ink; digit.fontStyle = FontStyles.Bold;
            digit.rectTransform.sizeDelta = new Vector2(8f, 6f);
            digit.sortingOrder = 4;
            digit.text = "3";
        }

        // ------------------------------------------------------------------ sound: tape hiss + projector rattle
        void BuildSound()
        {
            const int Rate = 22050;
            var r = new System.Random(7);
            float[] noise = new float[Rate];
            float lp = 0f;
            for (int i = 0; i < noise.Length; i++)
            {
                float w = (float)r.NextDouble() * 2f - 1f;
                lp += (w - lp) * .6f;
                noise[i] = lp * .8f + (r.NextDouble() < .004 ? (float)r.NextDouble() - .5f : 0f) * 1.6f;
            }
            hissClip = AudioClip.Create("static", noise.Length, 1, Rate, false);
            hissClip.SetData(noise, 0);

            float[] clack = new float[Rate];               // 16 shutter clicks a second + crackle
            for (int i = 0; i < clack.Length; i++)
            {
                float ph = i % (Rate / 16) / (float)Rate;
                float click = Mathf.Exp(-ph * 900f) * ((float)r.NextDouble() * 2f - 1f) * .35f;
                float pop = r.NextDouble() < .0008 ? ((float)r.NextDouble() - .5f) * 1.2f : 0f;
                clack[i] = click + pop + ((float)r.NextDouble() - .5f) * .03f;
            }
            rattleClip = AudioClip.Create("projector", clack.Length, 1, Rate, false);
            rattleClip.SetData(clack, 0);

            hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = hissClip; hiss.loop = true; hiss.playOnAwake = false; hiss.volume = 0f;
            rattle = gameObject.AddComponent<AudioSource>();
            rattle.clip = rattleClip; rattle.loop = true; rattle.playOnAwake = false; rattle.volume = .18f * GameVolume.Effects;
        }

        void Hiss(float level)
        {
            hiss.volume = .32f * level * GameVolume.Effects;
            if (!hiss.isPlaying) hiss.Play();
        }

        void OnDestroy()
        {
            GameStartIntro.Running = false;
            if (rt) { if (cam) cam.targetTexture = null; rt.Release(); Destroy(rt); }
            if (film) Destroy(film);
            if (hissClip) Destroy(hissClip);
            if (rattleClip) Destroy(rattleClip);
        }
    }
}

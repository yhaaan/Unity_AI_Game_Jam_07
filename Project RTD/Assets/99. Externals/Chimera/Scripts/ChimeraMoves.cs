using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>모든 키메라가 공유하는 박자 시계. Bpm만 바꾸면 전체 빠르기가 바뀝니다.</summary>
    public static class ChimeraBeat
    {
        public static float Bpm = 112f;
        /// <summary>0이면 부드럽게. 12로 두면 시간을 초당 12장으로 끊어 손그림 카툰처럼 보입니다.</summary>
        public static float StepsPerSecond = 0f;
        static float beat;
        static int lastFrame = -1;

        public static float Beat
        {
            get
            {
                if (Time.frameCount != lastFrame)
                {
                    beat += Time.deltaTime * Bpm / 60f;
                    lastFrame = Time.frameCount;
                }
                if (StepsPerSecond <= 0) return beat;
                float bs = Bpm / 60f / StepsPerSecond;
                return Mathf.Floor(beat / bs) * bs;
            }
        }

        /// <summary>Time.time에 같은 끊기를 적용한 시각.</summary>
        public static float Now
        {
            get { return StepsPerSecond <= 0 ? Time.time : Mathf.Floor(Time.time * StepsPerSecond) / StepsPerSecond; }
        }
    }

    public enum ChimeraState { Groove, Idle, Run, Jump, Skid, Hurt, Scare }

    public struct LegPose { public float Lr, Ly, Rr, Ry, Lk, Rk, lift, lean, dx, sq, foot; } // Lk·Rk: 무릎을 더 굽히는 정도
    public struct BodyPose { public float r, dx, dy, sx, sy, wing, fin; }
    public struct HeadPose { public float r, dx, dy, sx, sy, px, py, browR, browY, wink, eyePop, mouth; } // eyePop: 눈이 커지는 정도(0=그대로)
    public struct Flick { public float r, sx, sy; }

    /// <summary>
    /// 부위별(동물별) 그루브 동작표. 각도는 "도", 거리는 원본 512 캔버스의 픽셀,
    /// 회전은 화면에서 시계 방향이 +, y는 아래가 + 입니다(웹 버전 chimera-anim.js와 동일).
    /// </summary>
    public static class ChimeraMoves
    {
        const float TAU = Mathf.PI * 2f;

        // ── 리듬 도구 ──
        internal static float Fr(float b) { return b - Mathf.Floor(b); }
        public static float Hit(float b, float k = 5f) { return Mathf.Exp(-k * Fr(b)); }          // 박자에 '쿵'
        internal static float W(float b, float f = 1f, float ph = 0f) { return Mathf.Sin(TAU * (b * f + ph)); } // 부드러운 흔들림
        internal static float Arc(float b) { return Mathf.Sin(Mathf.PI * Fr(b)); }                          // 한 박자 0→1→0
        static bool Odd(float b) { return (Mathf.FloorToInt(b) & 1) == 1; }
        internal static float Snap(float v, float k = 4f) { return (float)System.Math.Tanh(k * v); }        // 딱딱 끊기는 전환
        internal static float Bar(float b) { return Fr(b / 4f) * 4f; }                                      // 4박 마디 안 위치

        // 한 박자씩 번갈아 다리 들기 (L=앞다리, R=뒷다리)
        static LegPose Alt(float b, float r, float y, float f = 1f)
        {
            float bb = b * f, u = Arc(bb);
            var p = new LegPose();
            if (Odd(bb)) { p.Rr = r * u; p.Ry = -y * u; }
            else { p.Lr = r * u; p.Ly = -y * u; }
            return p;
        }

        // ── 다리 ──
        public static LegPose Legs(string id, float b)
        {
            LegPose p; float u, k, q;
            switch (id)
            {
                case "pig": // 두 배 빠른 종종걸음
                    p = Alt(b, 20, 7, 2); p.lift = 2 * Hit(b * 2); p.lean = 3 * W(b, 1); return p;
                case "chicken": // 뒤로 탁탁 차는 스트럿
                    p = Alt(b, -24, 16); p.lift = 4 * Hit(b, 7); p.lean = 2 * W(b, .5f); return p;
                case "duck": // 뒤뚱뒤뚱
                    p = Alt(b, 8, 9); p.lean = 7 * W(b, .5f); p.dx = 9 * W(b, .5f); return p;
                case "rabbit": // 매 박자 깡충
                    u = Arc(b); p = new LegPose { Lr = 20 * u, Rr = -20 * u, lift = 36 * u, sq = Hit(b, 7) }; return p;
                case "kangaroo": // 두 박자에 한 번 크게 점프
                    u = Arc(b / 2);
                    p = new LegPose { Lr = 24 * u, Ly = -8 * u, Rr = 24 * u, Ry = -8 * u, lift = 62 * u, sq = Hit(b / 2, 9), lean = 4 * u };
                    return p;
                case "horse": // 앞발로 땅 긁다가 마디 끝에 뒷발차기
                    k = Bar(b) >= 3 ? Arc(b) : 0; q = Mathf.Max(0, W(b, 2));
                    p = new LegPose { Lr = 26 * q * (1 - k), Ly = -10 * q * (1 - k), Rr = -42 * k, Ry = -6 * k, lean = -6 * k, lift = 3 * Hit(b) };
                    return p;
                case "cow": // 절반 속도로 묵직하게 쿵쿵
                    p = Alt(b, 10, 15, .5f); p.sq = .6f * Hit(b * .5f, 9); p.lean = 3 * W(b, .25f); p.dx = 2.5f * Hit(b * .5f, 6) * W(b, 4); return p;
                case "unicorn": // 무릎 높이 드는 프랜스
                    p = Alt(b, 32, 18); p.lift = 9 * Arc(b); p.foot = -18 * Arc(b); return p;
                case "dragon": // 발 구르기
                    p = Alt(b + .5f, 12, 22); p.sq = .8f * Hit(b, 8); p.dx = 2 * Hit(b, 8) * W(b, 5); return p;
                case "phoenix": // 살짝 떠서 파닥파닥
                    p = new LegPose { Lr = 12 * W(b, 2), Rr = 12 * W(b, 2, .5f), lift = 16 + 7 * W(b, .5f), foot = 14 * W(b, 2, .25f), lean = 2 * W(b, .5f) };
                    return p;
                case "gumiho": // 미끄러지듯 글라이드
                    p = new LegPose { Lr = 9 * W(b, .5f), Rr = 9 * W(b, .5f, .25f), dx = 15 * W(b, .25f), lean = -4 * W(b, .25f), lift = 2 * Hit(b) };
                    return p;
                case "monkey": // 오다리 바운스
                    p = new LegPose { Lr = 10 * Hit(b, 3), Rr = -10 * Hit(b, 3), lift = 12 * Arc(b * 2), sq = .4f * Hit(b, 8) };
                    return p;
                case "lion": // 느릿한 허세 스웨거
                    p = Alt(b, 13, 8, .5f); p.lean = 5 * W(b, .25f); p.dx = 7 * W(b, .25f); return p;
                case "bear": // 스모 선수처럼 좌우로 쿵, 쿵
                    p = Alt(b, 9, 18, .5f); p.sq = .9f * Hit(b * .5f, 8); p.lean = 6 * W(b, .25f); p.dx = 5 * W(b, .25f); return p;
                case "axolotl": // 물장구 패들
                    p = new LegPose { Lr = 17 * W(b, 2), Rr = 17 * W(b, 2, .5f), lean = 3 * W(b, 1), lift = 3 + 2 * W(b, 1) };
                    return p;
                default:
                    p = Alt(b, 14, 10); p.lift = 3 * Hit(b); p.lean = 2 * W(b, .5f); return p;
            }
        }

        // ── 몸통 ──
        public static BodyPose Body(string id, float b)
        {
            var p = new BodyPose { sx = 1, sy = 1 };
            float h = Hit(b);
            switch (id)
            {
                case "sheep": // 젤리처럼 출렁
                    { float j = Hit(b, 2.5f) * W(b, 3); p.sx = 1 + .08f * j; p.sy = 1 - .08f * j; p.dy = 4 * h; p.r = 2 * W(b, .5f); }
                    break;
                case "pig": // 엉덩이 털기
                    p.r = 4 * W(b, 2); p.dy = 4 * h; p.sx = 1 + .04f * h; p.sy = 1 - .04f * h; break;
                case "chicken": // 치킨 댄스 날갯짓
                    p.r = -3 * Hit(b, 5); p.dy = 3 * h; p.wing = -30 * Mathf.Max(0, W(b, 2)); break;
                case "duck": // 엇박에 날개 퍼덕
                    p.r = 3 * W(b, .5f); p.dy = 3 * h; p.wing = -36 * Hit(b + .5f, 4); break;
                case "cow": // 느린 스웨이
                    p.r = 3 * W(b, .25f); p.dy = 5 * Hit(b * .5f, 6); break;
                case "lion": // 가슴 팝핀
                    p.dx = -6 * Hit(b, 6); p.sy = 1 + .03f * Hit(b, 6); p.r = 2 * W(b, .5f); break;
                case "monkey": // 몸 비틀기
                    p.r = 5 * W(b, .5f); p.dy = 4 * h; break;
                case "kangaroo": // 뒤로 젖혀 타기
                    p.r = 4 + 3 * W(b, .5f); p.dy = 3 * h; break;
                case "axolotl": // 물결 웨이브
                    p.r = 3 * W(b, 1); p.sx = 1 + .03f * W(b, 1); p.sy = 1 - .03f * W(b, 1); p.fin = 12 * W(b, 3); break;
                case "unicorn": // 사뿐사뿐
                    p.r = 2.5f * W(b, .5f); p.dy = -3 * Arc(b); break;
                case "dragon": // 날개 크게 치기
                    p.dy = 5 * h; p.r = 2 * W(b, .5f); p.wing = -44 * Hit(b / 2, 2) + 12; break;
                case "phoenix": // 날개 펄럭
                    p.r = 3 * W(b, .5f); p.dy = -4 * W(b, .5f); p.wing = 22 * W(b, 1); break;
                case "gumiho":
                    p.r = 2 * W(b, .5f); p.dy = 3 * h; break;
                case "bear": // 뱃살이 절반 속도로 묵직하게 출렁
                    { float j = Hit(b * .5f, 2) * W(b, 2.5f); p.sx = 1 + .07f * j; p.sy = 1 - .07f * j; p.dy = 6 * Hit(b * .5f, 5); p.r = 3 * W(b, .25f); }
                    break;
                case "rabbit": // 엉덩이 씰룩
                    p.r = 5 * W(b, 2) * Hit(b, 1.2f); p.dy = 3 * h; p.sx = 1 + .04f * h; p.sy = 1 - .04f * h; break;
                default:
                    p.dy = 4 * h; p.sy = 1 - .04f * h; p.sx = 1 + .03f * h; p.r = 1.5f * W(b, .5f); break;
            }
            return p;
        }

        /// <summary>꼬리 i번째(전체 n개)의 회전.</summary>
        public static float Tail(string id, float b, int i, int n)
        {
            switch (id)
            {
                case "pig": return 28 * W(b, 4);                                         // 파르르
                case "chicken": return 12 * W(b, 2);
                case "duck": return 20 * W(b, 4) * Hit(b, 1.5f);                         // 꽁지 흔들기
                case "cow": return 34 * Hit(b, 3) * W(b, 2) + 10 * W(b, .5f);            // 꼬리 채찍
                case "lion": return 22 * W(b, 1) + 10 * W(b, 2, .2f);                    // 올가미
                case "monkey": return 12 * W(b, .5f) + 5 * W(b, 2);                     // 크게 휘두르기
                case "kangaroo": return 16 * Hit(b, 5) - 6 + 5 * W(b, .5f);              // 바닥 탁
                case "axolotl": return 16 * W(b, 1, .25f);
                case "unicorn": return 18 * W(b, .5f, i * .18f) + 6 * W(b, 2, i * .3f);  // 두 겹이 시차를 두고 흐름
                case "dragon": return 14 * W(b, 1) + 8 * W(b, 2, .3f);                   // 뱀춤
                case "gumiho": return (i - (n - 1) / 2f) * 3.2f * W(b, .5f) + 9 * W(b, 1, i * .11f); // 아홉 꼬리 부채춤
                case "bear": return 14 * W(b, 2);
                case "rabbit": return 26 * W(b, 4);                                      // 솜꼬리 파르르
                case "sheep": case "phoenix": return 0;
                default: return 14 * W(b, 1, i * .1f);
            }
        }

        /// <summary>몸통에 달린 불꽃 i번째의 추가 회전(불사조 꼬리 깃이 차례로 넘실).</summary>
        public static float BodyFire(string id, float b, int i)
        {
            return id == "phoenix" ? 13 * W(b, 1, i * .15f) : 0;
        }

        // ── 머리 ──
        public static HeadPose Head(string id, float b)
        {
            var p = new HeadPose { sx = 1, sy = 1, browY = -2 * Hit(b) };
            float h = Hit(b);
            switch (id)
            {
                case "chicken": // 목을 쭉쭉 내미는 쪼기
                    p.dx = 8 - 20 * Hit(b, 4); p.dy = 2 * h; p.r = 4 * W(b, .5f); break;
                case "duck": // 갸웃갸웃 + 눈썹 씰룩
                    p.r = 10 * Snap(W(b, .5f)); p.dy = 2 * h; p.browY = -4 * Hit(b, 4); p.browR = 9 * W(b, 2); break;
                case "pig": // 깊은 끄덕임 + 눈알 뱅글뱅글
                    p.r = -8 * Hit(b, 3) + 3 * W(b, .5f); p.dy = 3 * h;
                    p.px = 2.5f * Mathf.Cos(TAU * b / 2); p.py = 2.5f * Mathf.Sin(TAU * b / 2); break;
                case "donkey": // 헤드뱅잉
                    p.r = 5 - 15 * Hit(b, 3); p.dy = 4 * h; break;
                case "cow": // 되새김질하듯 둥글게 굴림
                    p.dx = 5 * Mathf.Cos(TAU * b / 2); p.dy = 4 * Mathf.Sin(TAU * b / 2); p.r = 4 * W(b, .5f); p.px = 2 * W(b, .25f); break;
                case "goat": // 박자마다 고개 탁! 꺾기
                    p.r = 14 * Snap(W(b, .5f)); p.dy = 3 * h; p.px = 2 * Snap(W(b, .5f)); break;
                case "lion": // 쿨한 끄덕임, 마디 끝에 갈기 털기
                    { float q = Bar(b); p.r = -4 * Hit(b, 3) + (q >= 3 ? 10 * W(b, 4) * (4 - q) : 0); p.sx = p.sy = 1 + .03f * h; p.py = -1.5f * h; }
                    break;
                case "monkey": // 버블헤드 + 눈동자 좌우로 휙휙
                    p.r = 9 * W(b, 1); p.dx = 5 * W(b, .5f); p.dy = 2 * h; p.px = 1.5f * Snap(W(b, .5f)); break;
                case "kangaroo":
                    p.r = -6 * Hit(b, 4) + 2; p.dy = 3 * h; break;
                case "axolotl": // 헤벌쭉 스웨이 + 말랑 찌그러짐
                    p.r = 8 * W(b, .5f); p.dy = 3 * h; p.sx = 1 + .05f * h; p.sy = 1 - .05f * h; break;
                case "unicorn": // 마디 끝에 머리 넘기며 윙크
                    { float q = Bar(b), f = q >= 3 ? Mathf.Sin(Mathf.PI * (q - 3)) : 0; p.r = 3 * W(b, .5f) + 15 * f; p.dy = 2 * h - 4 * f; p.wink = f; }
                    break;
                case "dragon": // 거친 끄덕임 + 눈썹 꿈틀
                    p.r = 3 - 10 * Hit(b, 6); p.dx = -4 * Hit(b, 6); p.dy = 3 * h; p.browR = 10 * W(b, 2); p.browY = -2 * h; break;
                case "phoenix": // 스윽 미끄러지는 고개
                    p.dx = 6 * W(b, .5f); p.dy = -5 * Arc(b); p.r = 6 * W(b, .5f, .25f); break;
                case "bear": // 느릿하고 묵직한 끄덕임
                    p.r = -10 * Hit(b * .5f, 3) + 3 * W(b, .25f); p.dy = 5 * Hit(b * .5f, 4); p.px = 1.5f * W(b, .25f); break;
                case "rabbit": // 박자마다 고개 까딱
                    p.r = 6 * Snap(W(b, 1)); p.dy = 3 * h; p.px = 2 * Snap(W(b, .5f)); break;
                case "gumiho": // 능청스러운 느린 갸웃
                    p.r = 7 * W(b, .25f) - 4 * Hit(b + .5f, 5); p.dx = 4 * W(b, .25f); p.dy = 2 * h; break;
                default:
                    p.r = -5 * Hit(b, 4) + 2 * W(b, .5f); p.dy = 3 * h; break;
            }
            return p;
        }

        /// <summary>귀 i번째의 회전.</summary>
        public static float Ear(string id, float b, int i)
        {
            switch (id)
            {
                case "pig": return (i == 0 ? 1 : -1) * 20 * Hit(b + .85f, 4);            // 펄럭
                case "donkey": return 22 * W(b, 1, .2f + i * .12f);                      // 한 박자 늦게 따라옴
                case "monkey": return 7 * W(b, 2, i * .5f);                             // 쫑긋쫑긋
                case "bear": return (i == 0 ? 1 : -1) * 10 * Hit(b + .8f, 5);
                case "rabbit": return 20 * W(b, 1, i * .25f) + (i == 0 ? -8 : 8) * Hit(b, 4); // 긴 귀가 번갈아 펄럭
                case "kangaroo": return Odd(b) == (i == 1) ? (i == 1 ? 22 : -22) * Arc(b) : 0; // 한쪽씩 번갈아
                default: return 5 * Hit(b + .9f, 4) * (i == 0 ? 1 : -1);
            }
        }

        // ── 상태 동작: 아이들 / 달리기 / 점프 ──
        // 춤(Groove)은 부위마다 다르지만, 이 셋은 모든 조합이 같은 규칙으로 움직입니다.
        // 옛날 고무호스 카툰처럼: 크게 찌그러지고 늘어나며, 동작 전에 반대로 움츠리고(예비 동작),
        // 멈출 때 살짝 넘쳤다 돌아오고, 귀·꼬리·머리는 한 박자 늦게 따라옵니다.

        /// <summary>춤(Groove)에 얹는 박자 바운스: 박자 직전에 쭉 늘어났다가 박자에 '쿵' 찌그러짐.</summary>
        public static float GrooveBounce(float b) { float f = Fr(b); return .5f * Hit(b, 5) - .3f * f * f * f; }
        public const float JumpSeconds = 0.9f, JumpLoopSeconds = 1.5f, JumpHeight = 130f;
        public const float SkidSeconds = 0.7f, SkidLoopSeconds = 2.4f;
        public const float HurtSeconds = 0.6f, HurtLoopSeconds = 1.6f;
        public const float ScareSeconds = 0.9f, ScareLoopSeconds = 1.9f;

        // 놀람 곡선: ant=움찔 웅크림, hold=쭈욱 늘어난 정도, shiver=바들바들, land=털썩
        struct ScareCurve { public float ant, hold, shiver, land; }
        static ScareCurve Sc(float p)
        {
            var c = new ScareCurve();
            c.ant = p < .12f ? Mathf.Sin(Mathf.PI / 2 * p / .12f) : 0;
            c.hold = p < .12f ? 0 : BackOut(Mathf.Clamp01((p - .12f) / .12f)) * Mathf.Clamp01((.85f - p) / .12f);
            c.shiver = Mathf.Sin(p * 110) * c.hold;
            c.land = p > .85f ? Mathf.Exp(-(p - .85f) * 14) * Mathf.Cos((p - .85f) * 30) : 0;
            return c;
        }

        // 피격 곡선: back=뒤로 밀린 정도, imp=맞는 순간의 충격, jud=부르르 떨림, rec=돌아올 때 앞으로 넘침
        struct HurtCurve { public float back, imp, jud, rec; }
        static HurtCurve Hc(float p)
        {
            float e1 = Mathf.Clamp01(p / .12f);
            return new HurtCurve
            {
                back = e1 * Mathf.Pow(1 - p, 1.5f),
                imp = Mathf.Exp(-p * 14) * e1,
                jud = Mathf.Sin(p * 70) * (1 - p) * (1 - p),
                rec = Mathf.Sin(Mathf.PI * Mathf.Clamp01((p - .6f) / .4f)),
            };
        }

        // 급정지 곡선: env=자세를 잡았다 푸는 정도, k=남은 속도, jud=끼기긱 떨림
        static void Sk(float p, out float env, out float k, out float jud)
        {
            env = Mathf.Clamp01(p / .1f) * Mathf.Clamp01((1 - p) / .25f);
            k = (1 - p) * (1 - p);
            jud = Mathf.Sin(p * 90) * k;
        }

        struct JumpCurve { public float u, air, v, s, down; }
        static JumpCurve Jc(float p)
        {
            bool inAir = p > .2f && p < .75f;
            var j = new JumpCurve { u = Mathf.Clamp01((p - .2f) / .55f) };
            j.air = inAir ? Mathf.Sin(Mathf.PI * j.u) : 0;
            j.v = inAir ? 1 - 2 * j.u : 0; // 올라갈 때 +, 내려올 때 -
            j.s = p < .2f ? Mathf.Sin(Mathf.PI / 2 * p / .2f)
                : inAir ? -.15f - .9f * (1 - j.u)
                : 1.2f * Mathf.Exp(-(p - .75f) * 9) * Mathf.Cos((p - .75f) * 30);
            j.down = Mathf.Max(0, j.s);
            return j;
        }

        /// <summary>Idle·Run은 b(박자), Jump·Skid·Hurt·Scare는 p(0→1 진행도)를 씁니다.</summary>
        public static void State(ChimeraState st, float b, float p, out LegPose L, out BodyPose B, out HeadPose H)
        {
            L = new LegPose(); B = new BodyPose { sx = 1, sy = 1 }; H = new HeadPose { sx = 1, sy = 1, browY = -2 * Hit(b) };
            if (st == ChimeraState.Idle)
            {   // 박자마다 무릎을 굽히듯 까딱까딱, 머리는 늦게 따라옴
                float br = W(b, 1), hl = W(b, 1, -.15f);
                L.sq = .9f * br;
                B.sx = 1 + .05f * br; B.sy = 1 - .06f * br; B.dy = 3 * br; B.r = 1.5f * W(b, .5f);
                B.wing = 6 * W(b, 1, -.2f); B.fin = 8 * W(b, 1, -.2f);
                H.dy = 5 * hl; H.r = 3 * W(b, .5f, -.1f); H.sx = 1 + .03f * hl; H.sy = 1 - .03f * hl; H.px = 1.5f * Snap(W(b, .125f), 3);
            }
            else if (st == ChimeraState.Run)
            {   // 다리를 바퀴처럼 크게 휘젓고, 몸은 앞으로 쭉 늘어남
                float c = b * 2;
                L.Lr = 48 * W(c); L.Ly = -8 * Mathf.Max(0, W(c, 1, .25f)); L.Lk = -28 * Mathf.Max(0, W(c, 1, .25f));
                L.Rr = 48 * W(c, 1, .5f); L.Ry = -8 * Mathf.Max(0, W(c, 1, .75f)); L.Rk = -28 * Mathf.Max(0, W(c, 1, .75f));
                L.lift = 14 * Arc(c * 2); L.lean = -11 + 2 * W(c); L.sq = .5f * Hit(c * 2, 5);
                B.sx = 1.08f + .06f * W(c, 2); B.sy = .94f - .06f * W(c, 2); B.dy = 3 * W(c, 2, .25f); B.r = 3 * W(c);
                B.wing = -24 + 18 * W(c, 2); B.fin = 14 * W(c, 2);
                H.dx = -9; H.dy = 6 * W(c, 2, -.2f); H.r = 5 + 4 * W(c, 2, -.3f); H.px = -1.5f;
            }
            else if (st == ChimeraState.Skid)
            {   // 급정지: 두 발을 앞으로 내밀고 뒤로 젖힌 채 '끼기긱' 떨면서 미끄러지다 멈춤
                float env, k, jud; Sk(p, out env, out k, out jud);
                L.Lr = 30 * env; L.Rr = 30 * env; L.lean = 14 * env + 1.5f * jud; L.dx = 4 * jud; L.sq = .9f * env + .3f * jud;
                B.r = 6 * env; B.dy = 4 * env; B.sx = 1 + .05f * env; B.sy = 1 - .05f * env; B.wing = -30 * k * env; B.fin = -12 * k;
                H.r = -16 * k * env + 8 * env * (1 - k); H.dx = -10 * k * env; H.dy = 3 * env; H.py = 1.5f * env;
            }
            else if (st == ChimeraState.Hurt)
            {   // 피격: 앞에서 맞아 뒤로 밀려남. 몸이 홀쭉하게 눌리고 머리가 늦게 꺾이며 눈을 질끈 감음
                var h = Hc(p);
                L.Lr = 22 * h.back; L.Rr = 22 * h.back; L.dx = 46 * h.back + 3 * h.jud; L.lean = 16 * h.back - 5 * h.rec; L.sq = -1.6f * h.imp + .5f * h.rec;
                B.r = 8 * h.back; B.dy = 2 * h.back; B.wing = -34 * h.back; B.fin = -12 * h.back;
                H.r = 14 * h.back - 22 * h.imp; H.dx = -12 * h.imp; H.dy = 3 * h.back; H.wink = Mathf.Clamp01(h.back * 2.2f); H.mouth = .7f * h.back;
            }
            else if (st == ChimeraState.Scare)
            {   // 놀람: '꺅!' 움찔했다가 위로 쭈욱 늘어나 뜬 채 바들바들, 눈은 휘둥그레, 그러고는 털썩
                var c = Sc(p);
                L.Lr = 14 * c.hold; L.Rr = -14 * c.hold; L.lift = 26 * c.hold; L.dx = 3 * c.shiver; L.sq = 1.2f * c.ant - 4.5f * c.hold + c.land;
                B.r = 2 * c.shiver; B.wing = -50 * c.hold; B.fin = -15 * c.hold;
                H.r = 6 * c.hold + 2 * c.shiver; H.dy = 6 * c.ant - 22 * c.hold; H.sy = 1 + .12f * c.hold; H.eyePop = .45f * c.hold; H.mouth = Mathf.Min(1, c.hold);
            }
            else
            {   // 점프: 깊게 웅크림 → 길쭉하게 늘어나며 도약 → 착지 때 납작, 두 번 출렁이며 멈춤
                var j = Jc(p);
                L.Lr = 32 * j.air; L.Ly = -8 * j.air; L.Rr = -32 * j.air; L.Ry = -8 * j.air; L.Lk = L.Rk = -35 * j.air;
                L.lift = JumpHeight * 4 * j.u * (1 - j.u); L.sq = 2.6f * j.s;
                B.r = 6 * j.v; B.wing = -45 * j.air; B.fin = 14 * j.v;
                H.r = 8 * j.v - 10 * j.down; H.dy = 8 * j.down - 6 * j.v; H.py = -2 * j.v;
                H.wink = p >= .75f ? Mathf.Clamp01(1.5f * j.s) : 0;
            }
        }

        public static float StateTail(ChimeraState st, float b, float p, int i)
        {
            if (st == ChimeraState.Idle) return 14 * W(b, 1, -.25f - i * .08f);
            if (st == ChimeraState.Run) return -16 + 18 * W(b * 2, 2, -.35f - i * .07f); // 뒤로 휘날림
            if (st == ChimeraState.Skid) { float env, k, jud; Sk(p, out env, out k, out jud); return -30 * k * env + 6 * jud; }
            if (st == ChimeraState.Hurt) { var h = Hc(p); return -26 * h.back + 8 * h.jud; }
            if (st == ChimeraState.Scare) { var c = Sc(p); return -40 * c.hold + 6 * c.shiver; }
            var j = Jc(p); return 36 * j.v - 14 * j.down;
        }

        public static float StateEar(ChimeraState st, float b, float p, int i)
        {
            if (st == ChimeraState.Idle)
            {   // 4박마다 귀 쫑긋
                float q = Bar(b), tw = q < .5f ? Mathf.Sin(Mathf.PI * q / .5f) : 0;
                return (i == 0 ? 1 : -1) * (5 * W(b, 1, -.3f) + 10 * tw);
            }
            if (st == ChimeraState.Run) return 30 + 9 * W(b * 2, 2, -.4f + i * .1f); // 바람에 뒤로 쏠린 채 펄럭
            if (st == ChimeraState.Skid) { float env, k, jud; Sk(p, out env, out k, out jud); return -24 * k * env + 4 * jud; }
            if (st == ChimeraState.Hurt) { var h = Hc(p); return -22 * h.back + 5 * h.jud; }
            if (st == ChimeraState.Scare) { var c = Sc(p); return (i == 0 ? -1 : 1) * 18 * c.hold + 6 * c.shiver; }
            return (i == 0 ? -1 : 1) * 26 * Jc(p).v;
        }

        public static float StateFire(ChimeraState st, float b, float p, int i)
        {
            if (st == ChimeraState.Idle) return 0;
            if (st == ChimeraState.Run) return 12 * W(b * 2, 2, -i * .1f);
            if (st == ChimeraState.Skid) return -14 * (1 - p) * (1 - p);
            if (st == ChimeraState.Hurt) return -14 * Hc(p).back;
            if (st == ChimeraState.Scare) return 20 * Sc(p).hold;
            return 14 * Jc(p).v;
        }

        // ── 조합 완료 연출 ──
        // 다리 → 몸통 → 머리 순서로 '뿅' 하고 붙은 뒤, CelebrateAt초에 이펙트가 터지며 기뻐 점프.
        public const float AssembleSeconds = 0.54f, CelebrateAt = 0.5f;

        /// <summary>0→살짝 넘쳤다가 1 로 가는 탄력 곡선.</summary>
        public static float BackOut(float e) { return 1 + 3.4f * Mathf.Pow(e - 1, 3) + 2.4f * Mathf.Pow(e - 1, 2); }

        /// <summary>a: 조합 시작 후 흐른 시간. 크기 k와 날아오는 거리 off(픽셀, 아래 +)를 돌려줍니다.</summary>
        public static void PopIn(float a, float delay, float from, out float k, out float off)
        {
            float e = Mathf.Clamp01((a - delay) / 0.3f);
            k = e <= 0 ? 0 : BackOut(e);
            off = from * (1 - e) * (1 - e);
        }

        /// <summary>불꽃은 어디에 달려 있든 늘 일렁입니다.</summary>
        public static Flick Flicker(float b, int i, float extra = 0)
        {
            return new Flick
            {
                r = extra + 4 * W(b, 1.5f, i * .37f),
                sx = 1 + .07f * W(b, 3, i * .21f),
                sy = 1 + .10f * W(b, 2.5f, i * .43f) + .14f * Hit(b, 4),
            };
        }
    }
}

using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>모든 키메라가 공유하는 박자 시계. Bpm만 바꾸면 전체 빠르기가 바뀝니다.</summary>
    public static class ChimeraBeat
    {
        public static float Bpm = 112f;
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
                return beat;
            }
        }
    }

    public enum ChimeraState { Groove, Idle, Run, Jump }

    public struct LegPose { public float Lr, Ly, Rr, Ry, lift, lean, dx, sq, foot; }
    public struct BodyPose { public float r, dx, dy, sx, sy, wing, fin; }
    public struct HeadPose { public float r, dx, dy, sx, sy, px, py, browR, browY, wink; }
    public struct Flick { public float r, sx, sy; }

    /// <summary>
    /// 부위별(동물별) 그루브 동작표. 각도는 "도", 거리는 원본 512 캔버스의 픽셀,
    /// 회전은 화면에서 시계 방향이 +, y는 아래가 + 입니다(웹 버전 chimera-anim.js와 동일).
    /// </summary>
    public static class ChimeraMoves
    {
        const float TAU = Mathf.PI * 2f;

        // ── 리듬 도구 ──
        static float Fr(float b) { return b - Mathf.Floor(b); }
        public static float Hit(float b, float k = 5f) { return Mathf.Exp(-k * Fr(b)); }          // 박자에 '쿵'
        static float W(float b, float f = 1f, float ph = 0f) { return Mathf.Sin(TAU * (b * f + ph)); } // 부드러운 흔들림
        static float Arc(float b) { return Mathf.Sin(Mathf.PI * Fr(b)); }                          // 한 박자 0→1→0
        static bool Odd(float b) { return (Mathf.FloorToInt(b) & 1) == 1; }
        static float Snap(float v, float k = 4f) { return (float)System.Math.Tanh(k * v); }        // 딱딱 끊기는 전환
        static float Bar(float b) { return Fr(b / 4f) * 4f; }                                      // 4박 마디 안 위치

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
                case "kangaroo": return Odd(b) == (i == 1) ? (i == 1 ? 22 : -22) * Arc(b) : 0; // 한쪽씩 번갈아
                default: return 5 * Hit(b + .9f, 4) * (i == 0 ? 1 : -1);
            }
        }

        // ── 상태 동작: 아이들 / 달리기 / 점프 ──
        // 춤(Groove)은 부위마다 다르지만, 이 셋은 모든 조합이 같은 규칙으로 움직입니다.
        // 통통 튀는 찌그러짐 + 귀·꼬리·날개가 한 박자 늦게 따라오는 느낌.
        public const float JumpSeconds = 0.9f, JumpLoopSeconds = 1.5f, JumpHeight = 110f;

        struct JumpCurve { public float u, air, v, s, down; }
        static JumpCurve Jc(float p)
        {
            bool inAir = p > .2f && p < .75f;
            var j = new JumpCurve { u = Mathf.Clamp01((p - .2f) / .55f) };
            j.air = inAir ? Mathf.Sin(Mathf.PI * j.u) : 0;
            j.v = inAir ? 1 - 2 * j.u : 0; // 올라갈 때 +, 내려올 때 -
            j.s = p < .2f ? Mathf.Sin(Mathf.PI / 2 * p / .2f)
                : inAir ? -.1f - .6f * (1 - j.u)
                : Mathf.Exp(-(p - .75f) * 12) * Mathf.Cos((p - .75f) * 28);
            j.down = Mathf.Max(0, j.s);
            return j;
        }

        /// <summary>Idle·Run은 b(박자), Jump는 p(0→1 진행도)를 씁니다.</summary>
        public static void State(ChimeraState st, float b, float p, out LegPose L, out BodyPose B, out HeadPose H)
        {
            L = new LegPose(); B = new BodyPose { sx = 1, sy = 1 }; H = new HeadPose { sx = 1, sy = 1, browY = -2 * Hit(b) };
            if (st == ChimeraState.Idle)
            {   // 온몸으로 숨쉬기, 느린 두리번
                float br = W(b, .5f);
                L.sq = .5f * br;
                B.sx = 1 + .03f * br; B.sy = 1 - .04f * br; B.dy = 1.5f * br; B.wing = 3 * W(b, .5f, -.1f); B.fin = 5 * W(b, 1);
                H.dy = 2.5f * W(b, .5f, -.12f); H.r = 2 * W(b, .25f); H.px = 1.5f * Snap(W(b, .125f), 3);
            }
            else if (st == ChimeraState.Run)
            {   // 한 박자에 두 걸음, 앞으로 숙이고 달림
                float c = b * 2;
                L.Lr = 34 * W(c); L.Ly = -5 * Mathf.Max(0, W(c, 1, .25f));
                L.Rr = 34 * W(c, 1, .5f); L.Ry = -5 * Mathf.Max(0, W(c, 1, .75f));
                L.lift = 9 * Arc(c * 2); L.lean = -7 + 1.5f * W(c); L.sq = .3f * Hit(c * 2, 6);
                B.sx = 1.04f + .04f * W(c, 2); B.sy = .97f - .04f * W(c, 2); B.dy = 2 * W(c, 2, .25f); B.r = 2 * W(c);
                B.wing = -18 + 14 * W(c, 2); B.fin = 10 * W(c, 2);
                H.dx = -5; H.dy = 4 * W(c, 2, -.2f); H.r = 3 + 3 * W(c, 2, -.3f); H.px = -1.5f;
            }
            else
            {   // 점프: 웅크림 → 쭉 늘어나며 도약 → 착지 때 납작해졌다가 출렁
                var j = Jc(p);
                L.Lr = 24 * j.air; L.Ly = -6 * j.air; L.Rr = -24 * j.air; L.Ry = -6 * j.air;
                L.lift = JumpHeight * 4 * j.u * (1 - j.u); L.sq = 2 * j.s;
                B.r = 4 * j.v; B.wing = -38 * j.air; B.fin = 10 * j.v;
                H.r = 5 * j.v - 7 * j.down; H.dy = 5 * j.down - 4 * j.v; H.py = -1.5f * j.v;
                H.wink = p >= .75f ? Mathf.Clamp01(1.5f * j.s) : 0;
            }
        }

        public static float StateTail(ChimeraState st, float b, float p, int i)
        {
            if (st == ChimeraState.Idle) return 8 * W(b, .5f, -.2f - i * .08f);
            if (st == ChimeraState.Run) return -10 + 14 * W(b * 2, 2, -.35f - i * .07f); // 뒤로 휘날림
            var j = Jc(p); return 26 * j.v - 10 * j.down;
        }

        public static float StateEar(ChimeraState st, float b, float p, int i)
        {
            if (st == ChimeraState.Idle)
            {   // 4박마다 귀 쫑긋
                float q = Bar(b), tw = q < .5f ? Mathf.Sin(Mathf.PI * q / .5f) : 0;
                return (i == 0 ? 1 : -1) * (2 * W(b, .5f, -.25f) + 10 * tw);
            }
            if (st == ChimeraState.Run) return 12 + 8 * W(b * 2, 2, -.4f + i * .1f);
            return (i == 0 ? -1 : 1) * 16 * Jc(p).v;
        }

        public static float StateFire(ChimeraState st, float b, float p, int i)
        {
            if (st == ChimeraState.Idle) return 0;
            if (st == ChimeraState.Run) return 8 * W(b * 2, 2, -i * .1f);
            return 10 * Jc(p).v;
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

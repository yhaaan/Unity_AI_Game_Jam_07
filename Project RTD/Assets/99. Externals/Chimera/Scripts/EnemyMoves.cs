using UnityEngine;

namespace ChimeraGroove
{
    public enum ScientistState { Idle, Walk, Attack }
    public enum SciAttack { Shoot, Swing, Pull }

    /// <summary>과학자별 설정. 좌표는 각 과학자 캔버스의 픽셀(왼쪽 위 원점).</summary>
    public class SciConfig
    {
        public int face;             // 바라보는 방향: -1 왼쪽, +1 오른쪽
        public float ground, height; // 발바닥 y, 키 → 모두 같은 키로 맞춤
        public SciAttack attack;     // 발사 / 휘두르기 / 끌어당기기
        public string shot;          // 발사체: "ray"(광선) / "egg"(달걀)
        public string arm;           // 휘두르는 위팔 레이어 id
        public int dir;              // 휘두르는 회전 방향
        public float swing;          // 내려치는 각도
        public string weapon;        // 이펙트가 나오는 자리(총구·자석 끝·휘두르는 무기 끝)의 기준이 되는 무기 레이어 id
        public Vector2 muzzle;
    }

    /// <summary>한 프레임의 과학자 자세. 각도는 도(시계 방향 +), 거리는 픽셀(아래 +).</summary>
    public struct SciPose
    {
        public float rootX, rootY, rootR, sq;
        public float torsoR, torsoDx, torsoDy, torsoSy;
        public float headR, headDy;
        public float leg0t, leg0s, leg1t, leg1s;   // 허벅지·정강이
        public float arm, armAll0, armAll1;        // 휘두르는 팔 / 나머지 팔의 흔들림
        public float hair, px, wink, mouth;
        public bool swinging, impact, walking;
        public int step;
        public string fx; public float fxT, fxOn, fxP; // 무기 이펙트: "ray" / "egg" / "pull" / "swing"
        public float fxCh, hitT;                       // 모으는 정도(발사 전) / 내려친 뒤 흐른 시간(초)
        public bool trail;                             // 휘두르는 궤적을 그릴 구간인지
    }

    /// <summary>미친 과학자 동작표 (웹 버전 enemies.js와 동일).</summary>
    public static class EnemyMoves
    {
        public const float SciHeight = 560f, AttackSeconds = 0.9f, AttackLoopSeconds = 1.9f;
        const float TAU = Mathf.PI * 2f;

        public static SciConfig Config(string id)
        {
            switch (id)
            {
                case "ray": return new SciConfig { face = -1, ground = 854, height = 809, attack = SciAttack.Shoot, shot = "ray", weapon = "weapon", muzzle = new Vector2(62, 500) };
                case "pipe": return new SciConfig { face = -1, ground = 624, height = 597, attack = SciAttack.Swing, arm = "upper_arm_left", dir = -1, swing = 205, weapon = "pipe", muzzle = new Vector2(572, 262) };
                case "net": return new SciConfig { face = 1, ground = 623, height = 590, attack = SciAttack.Swing, arm = "near-upper-arm", dir = 1, swing = 72, weapon = "net", muzzle = new Vector2(500, 135) };
                case "egg": return new SciConfig { face = -1, ground = 599, height = 570, attack = SciAttack.Shoot, shot = "egg", weapon = "weapon", muzzle = new Vector2(48, 320) };
                case "magnet": return new SciConfig { face = -1, ground = 615, height = 585, attack = SciAttack.Pull, arm = "upper_arm_left", weapon = "magnet", muzzle = new Vector2(40, 322) };
            }
            return null;
        }

        static float W(float b, float f = 1, float ph = 0) { return ChimeraMoves.W(b, f, ph); }
        static float Smooth(float t) { return t * t * (3 - 2 * t); }

        /// <summary>아이들: 까딱까딱 숨쉬다가 4박마다 어깨를 들썩이며 음흉하게 킥킥.</summary>
        public static SciPose Idle(float b, SciConfig c)
        {
            float br = W(b, 1), q = ChimeraMoves.Bar(b), snick = q >= 3 ? Mathf.Sin(Mathf.PI * (q - 3)) : 0;
            return new SciPose
            {
                sq = .4f * br,
                torsoDy = 2.5f * br + 3 * snick * Mathf.Sin((q - 3) * TAU * 5), torsoSy = 1 - .015f * br,
                headR = 2 * W(b, .5f, -.1f) + c.face * 6 * snick, headDy = 2 * W(b, 1, -.15f),
                armAll0 = -2 * W(b, 1, -.2f), armAll1 = 2 * W(b, 1, -.2f), hair = 4 * W(b, 1, -.3f),
                px = c.face * 2 + 2 * ChimeraMoves.Snap(W(b, .125f), 3), wink = .5f * snick, mouth = .3f * snick,
            };
        }

        /// <summary>걷기: 한 박자에 한 걸음, 무릎을 굽혀 성큼성큼.</summary>
        public static SciPose Walk(float b, SciConfig c)
        {
            float f = -c.face; // 왼쪽을 볼 때 +회전이 '앞으로'
            var p = new SciPose { torsoSy = 1, walking = true, step = Mathf.FloorToInt(b) };
            p.leg0t = f * 24 * W(b, .5f); p.leg0s = -f * 36 * Mathf.Max(0, Mathf.Cos(TAU * (b * .5f)));
            p.leg1t = f * 24 * W(b, .5f, .5f); p.leg1s = -f * 36 * Mathf.Max(0, Mathf.Cos(TAU * (b * .5f + .5f)));
            p.rootY = -6 * ChimeraMoves.Arc(b); p.rootR = -f * 3 + 1.5f * W(b, .5f); p.sq = .35f * ChimeraMoves.Hit(b, 6);
            p.torsoDy = 3 * W(b, 1, .25f); p.torsoR = 2.5f * W(b, .5f, .25f);
            p.headR = -2 * W(b, .5f, .1f); p.headDy = 3 * W(b, 1, .1f);
            p.armAll0 = -4 * W(b, .5f); p.armAll1 = 4 * W(b, .5f); p.hair = -c.face * 9 + 7 * W(b, 1, -.25f); // 머리카락은 뒤로 쏠린 채 출렁
            p.px = c.face * 2;
            return p;
        }

        /// <summary>공격(p: 0→1). 무기에 따라 발사 / 휘두르기 / 끌어당기기.</summary>
        public static SciPose Attack(float p, SciConfig c)
        {
            var o = new SciPose { torsoSy = 1 };
            if (c.attack == SciAttack.Shoot)
            {   // 앞으로 숙여 버티며 부르르 모았다가 '빵!' — 온몸이 뒤로 튕겨 나가며 껑충 뜀
                float ch = Mathf.Clamp01(p / .35f), t = p - .35f, rec = t >= 0 ? Mathf.Exp(-t * 6) : 0, pre = t < 0 ? 1 : 0;
                float hop = t >= 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .22f)) : 0, buzz = pre * Mathf.Sin(p * 130) * ch;
                o.sq = 1.4f * Mathf.Sin(Mathf.PI / 2 * ch) * pre - 1.1f * rec; o.rootX = -c.face * 30 * rec; o.rootY = -16 * hop;
                o.rootR = c.face * 5 * ch * pre - c.face * 13 * rec;
                o.torsoDx = -c.face * 26 * rec + 2.8f * buzz; o.torsoR = -c.face * 7 * rec;
                o.headR = -c.face * 5 * ch * pre + c.face * 15 * rec; o.headDy = 4 * rec;
                o.leg0t = 11 * rec; o.leg1t = -11 * rec; o.hair = -c.face * 24 * rec + 3 * buzz;
                o.px = c.face * 2.5f; o.wink = .7f * ch * pre; o.mouth = Mathf.Max(.4f * ch * pre, rec);
                o.fx = c.shot; o.fxT = t * AttackSeconds; o.fxCh = pre > 0 ? ch : 0;
            }
            else if (c.attack == SciAttack.Swing)
            {   // 웅크리며 뒤로 한껏 젖혔다가 껑충 뛰어들며 '후웅!' 내려치고, 땅이 울리며 멈칫
                float A = c.swing, W0 = -42;
                float a = p < .3f ? W0 * Mathf.Sin(Mathf.PI / 2 * p / .3f)
                    : p < .43f ? Mathf.LerpUnclamped(W0, A, ChimeraMoves.BackOut((p - .3f) / .13f))
                    : p < .7f ? A + 5 * Mathf.Sin((p - .43f) * 70) * (1 - (p - .43f) / .27f)
                    : A * (1 - Smooth((p - .7f) / .3f));
                float k = a / A, kp = Mathf.Max(0, k), wind = Mathf.Max(0, -a / 42), hitT = p - .43f, imp = hitT >= 0 ? Mathf.Exp(-hitT * 9) : 0;
                float leap = p >= .3f && p < .43f ? Mathf.Sin(Mathf.PI * (p - .3f) / .13f) : 0, shake = 3.5f * Mathf.Sin(p * 150) * imp;
                o.rootX = c.face * (40 * kp - 14 * wind) + shake; o.rootY = -20 * leap; o.sq = 1.4f * imp + .9f * wind; o.rootR = c.dir * 7 * k;
                o.torsoR = c.dir * 18 * k; o.torsoDy = 5 * imp;
                o.headR = c.dir * 9 * k; o.headDy = 3 * imp;
                o.leg0t = -c.face * 16 * kp; o.leg1t = c.face * 10 * kp;
                o.swinging = true; o.arm = c.dir * a; o.hair = -c.dir * 20 * k + 2 * shake;
                o.px = c.face * 2.5f; o.wink = .6f * wind; o.mouth = Mathf.Max(kp, .4f * wind); o.impact = hitT >= 0;
                o.fx = "swing"; o.trail = p >= .29f && p < .5f; o.hitT = hitT * AttackSeconds;
            }
            else
            {   // 발을 버티고 뒤로 젖힌 채 자석을 내밀어 덜덜 떨며 '위이잉' 끌어당김
                float on = Mathf.Clamp01(p / .15f) * Mathf.Clamp01((1 - p) / .15f), jit = Mathf.Sin(p * 170) * on;
                o.rootX = c.face * 10 * on + 3 * jit; o.sq = .8f * on; o.rootR = -c.face * 7 * on + .8f * jit;
                o.torsoDx = c.face * 16 * on; o.torsoR = c.face * 9 * on + 1.2f * jit;
                o.headR = -c.face * 6 * on + jit; o.headDy = 3 * on;
                o.leg0t = -13 * on; o.leg1t = 13 * on; o.swinging = true; o.arm = 5 * jit; o.hair = c.face * 10 * on + 6 * jit;
                o.px = c.face * 2.5f; o.wink = .4f * on; o.mouth = .8f * on;
                if (on > .05f) { o.fx = "pull"; o.fxOn = on; o.fxP = p; }
            }
            return o;
        }
    }
}

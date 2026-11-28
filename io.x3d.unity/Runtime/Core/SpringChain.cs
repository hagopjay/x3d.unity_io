using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace X3D.Core
{
    /// <summary>
    /// Spring bones (hair, tails, cloth strips, jiggle): the C# twin of x3d.loom x3d.springbone/src/springbone-core.js.
    /// Own code, written from x3d.springbone/SPEC.md; no vendor source. Same order of operations as the JavaScript and
    /// Python versions, so all three agree to rounding (the golden trace in tests/X3D.Core.Tests). No UnityEngine here:
    /// X3DSpringBone (Runtime/Unity) drives it from Transforms. Everything is in X3D's frame: +Y up, right-handed.
    /// Vectors are double[3], quaternions double[4] {x, y, z, w}, an affine is (L: row-major double[9], T: double[3]).
    /// </summary>
    public sealed class Affine
    {
        public double[] L = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        public double[] T = { 0, 0, 0 };
        public Affine() { }
        public Affine(double[] l, double[] t) { L = l; T = t; }
        public static Affine Identity { get { return new Affine(); } }
    }

    public sealed class SpringJointPose
    {
        public double[] Translation = { 0, 0, 0 }, Center = { 0, 0, 0 }, Rotation = { 0, 0, 0, 1 }, Scale = { 1, 1, 1 }, ScaleOrientation = { 0, 0, 0, 1 };
        public SpringJointPose Clone()
        {
            return new SpringJointPose { Translation = (double[])Translation.Clone(), Center = (double[])Center.Clone(), Rotation = (double[])Rotation.Clone(), Scale = (double[])Scale.Clone(), ScaleOrientation = (double[])ScaleOrientation.Clone() };
        }
    }

    public sealed class SpringJoint { public string Name; public int Parent; public double[] EndOffset; }
    public sealed class SpringTreeDesc { public List<SpringJoint> Joints = new List<SpringJoint>(); }
    public sealed class SpringTreeInput { public Affine ParentWorld = Affine.Identity; public List<SpringJointPose> Pose = new List<SpringJointPose>(); }

    public sealed class SpringCollider
    {
        public string Shape = "SPHERE", Bound = "OUTSIDE", Direction = "Y";
        public double[] Center = { 0, 0, 0 }, Axis = { 0, 0, 0 };
        public double Radius = 0.5, Radius2 = 0, Height = 0;
        public Affine World = Affine.Identity;
    }

    public sealed class SpringParams
    {
        public bool Enabled = true, Interpolate = false, GravityRestCompensation = true, SubstepRoots = true;
        public double UpdateRate = 60; public string TimeMode = "FIXED"; public int MaxSubsteps = 3;
        public string SpringModel = "POSITION";
        public double Damping = 0.1, Elasticity = 0.1, Stiffness = 0.1, Inert = 0, Friction = 0, Radius = 0, AngleLimit = 0, Stretch = 0;
        public double[][] DampingCurve = new double[0][], ElasticityCurve = new double[0][], StiffnessCurve = new double[0][], InertCurve = new double[0][],
            FrictionCurve = new double[0][], RadiusCurve = new double[0][], AngleLimitCurve = new double[0][], StretchCurve = new double[0][];
        public double Frequency = 0, DampingRatio = 0.2, EndLength = 0;
        public double[] EndOffset = { 0, 0, 0 }, Gravity = { 0, 0, 0 }, Force = { 0, 0, 0 }, Wind = { 0, 0, 0 };
        public string ForceUnits = "PER_STEP";
        public double WindTurbulence = 0, WindFrequency = 1; public int Seed = 1;
        public string FreezeAxis = "NONE", BranchMode = "SKIP";
        public double Weight = 1, ResetDistance = 0;
    }

    public sealed class SpringTreeOutput { public double[][] Rotations, Translations, Positions; }
    public sealed class SpringFrameOutput { public List<SpringTreeOutput> Trees = new List<SpringTreeOutput>(); public int Steps; public double DroppedTime; public bool Collided; }

    public sealed class SpringChain
    {
        // ------------------------------------------------------------------------------------------------ maths
        public static double[] Add(double[] a, double[] b) { return new[] { a[0] + b[0], a[1] + b[1], a[2] + b[2] }; }
        public static double[] Sub(double[] a, double[] b) { return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }; }
        public static double[] Mul(double[] a, double s) { return new[] { a[0] * s, a[1] * s, a[2] * s }; }
        public static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
        public static double[] Cross(double[] a, double[] b) { return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }
        public static double Len(double[] a) { return Math.Sqrt(Dot(a, a)); }
        public static double[] Unit(double[] a) { double l = Len(a); return l > 1e-300 ? Mul(a, 1 / l) : new double[] { 0, 0, 0 }; }
        static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
        static double Clamp01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }

        public static double[] QMul(double[] a, double[] b)
        {
            return new[] { a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
                           a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2] };
        }
        static double[] QConj(double[] q) { return new[] { -q[0], -q[1], -q[2], q[3] }; }
        public static double[] QNorm(double[] q)
        {
            double l = Math.Sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
            return l > 0 ? new[] { q[0] / l, q[1] / l, q[2] / l, q[3] / l } : new double[] { 0, 0, 0, 1 };
        }
        public static double[] QFromAxisAngle(double[] axis, double angle)
        {
            var a = Unit(axis);
            if (a[0] == 0 && a[1] == 0 && a[2] == 0) return new double[] { 0, 0, 0, 1 };
            double s = Math.Sin(angle / 2);
            return new[] { a[0] * s, a[1] * s, a[2] * s, Math.Cos(angle / 2) };
        }
        static double[] QFromTo(double[] u0, double[] v0)
        {
            var u = Unit(u0); var v = Unit(v0); double d = Dot(u, v);
            if (d >= 1 - 1e-15) return new double[] { 0, 0, 0, 1 };
            if (d <= -1 + 1e-15)
            {
                var ax = Cross(u, new double[] { 1, 0, 0 });
                if (Len(ax) < 1e-6) ax = Cross(u, new double[] { 0, 1, 0 });
                ax = Unit(ax); return new[] { ax[0], ax[1], ax[2], 0.0 };
            }
            var c = Cross(u, v); return QNorm(new[] { c[0], c[1], c[2], 1 + d });
        }
        static double[] QSlerp(double[] a, double[] b0, double t)
        {
            var b = b0; double d = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
            if (d < 0) { b = new[] { -b[0], -b[1], -b[2], -b[3] }; d = -d; }
            if (d > 1 - 1e-12) return QNorm(new[] { Lerp(a[0], b[0], t), Lerp(a[1], b[1], t), Lerp(a[2], b[2], t), Lerp(a[3], b[3], t) });
            double th = Math.Acos(d), s = Math.Sin(th), wa = Math.Sin((1 - t) * th) / s, wb = Math.Sin(t * th) / s;
            return new[] { a[0] * wa + b[0] * wb, a[1] * wa + b[1] * wb, a[2] * wa + b[2] * wb, a[3] * wa + b[3] * wb };
        }
        public static double[] M3FromQuat(double[] q0)
        {
            var q = QNorm(q0); double x = q[0], y = q[1], z = q[2], w = q[3];
            return new[] { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) };
        }
        static double[] M3Mul(double[] A, double[] B)
        {
            var C = new double[9];
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) C[r * 3 + c] = A[r * 3] * B[c] + A[r * 3 + 1] * B[3 + c] + A[r * 3 + 2] * B[6 + c];
            return C;
        }
        public static double[] M3V(double[] A, double[] v) { return new[] { A[0] * v[0] + A[1] * v[1] + A[2] * v[2], A[3] * v[0] + A[4] * v[1] + A[5] * v[2], A[6] * v[0] + A[7] * v[1] + A[8] * v[2] }; }
        static double[] M3T(double[] A) { return new[] { A[0], A[3], A[6], A[1], A[4], A[7], A[2], A[5], A[8] }; }
        static double[] M3Col(double[] A, int c) { return new[] { A[c], A[3 + c], A[6 + c] }; }
        static double[] M3Inv(double[] A)
        {
            double a = A[0], b = A[1], c = A[2], d = A[3], e = A[4], f = A[5], g = A[6], h = A[7], i = A[8];
            double co0 = e * i - f * h, co1 = f * g - d * i, co2 = d * h - e * g, det = a * co0 + b * co1 + c * co2, k = 1 / det;
            return new[] { co0 * k, (c * h - b * i) * k, (b * f - c * e) * k, co1 * k, (a * i - c * g) * k, (c * d - a * f) * k, co2 * k, (b * g - a * h) * k, (a * e - b * d) * k };
        }
        public static double[] QuatOfLinear(double[] A)
        {
            var c0 = Unit(M3Col(A, 0)); var c1 = Unit(M3Col(A, 1)); var c2 = Unit(M3Col(A, 2));
            double m00 = c0[0], m10 = c0[1], m20 = c0[2], m01 = c1[0], m11 = c1[1], m21 = c1[2], m02 = c2[0], m12 = c2[1], m22 = c2[2];
            double tr = m00 + m11 + m22; double[] q;
            if (tr > 0) { double s = Math.Sqrt(tr + 1) * 2; q = new[] { (m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s }; }
            else if (m00 > m11 && m00 > m22) { double s = Math.Sqrt(1 + m00 - m11 - m22) * 2; q = new[] { 0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s }; }
            else if (m11 > m22) { double s = Math.Sqrt(1 + m11 - m00 - m22) * 2; q = new[] { (m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s }; }
            else { double s = Math.Sqrt(1 + m22 - m00 - m11) * 2; q = new[] { (m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s }; }
            return QNorm(q);
        }
        public static Affine Compose(Affine A, Affine B) { return new Affine(M3Mul(A.L, B.L), Add(M3V(A.L, B.T), A.T)); }
        public static double[] ApplyPoint(Affine A, double[] p) { return Add(M3V(A.L, p), A.T); }
        public static Affine FromFields(SpringJointPose f)
        {
            var R = M3FromQuat(f.Rotation); var s = f.Scale; var L = R;
            if (s[0] != 1 || s[1] != 1 || s[2] != 1)
            {
                var SO = M3FromQuat(f.ScaleOrientation);
                L = M3Mul(R, M3Mul(SO, M3Mul(new[] { s[0], 0, 0, 0, s[1], 0, 0, 0, s[2] }, M3T(SO))));
            }
            return new Affine(L, Sub(Add(f.Translation, f.Center), M3V(L, f.Center)));
        }
        public static double EvalCurve(double[][] keys, double t)
        {
            if (keys == null || keys.Length == 0) return 1;
            if (t <= keys[0][0]) return keys[0][1];
            for (int i = 1; i < keys.Length; i++)
                if (t <= keys[i][0]) { var a = keys[i - 1]; var b = keys[i]; double w = b[0] - a[0]; return w > 0 ? Lerp(a[1], b[1], (t - a[0]) / w) : b[1]; }
            return keys[keys.Length - 1][1];
        }

        // ------------------------------------------------------------------------------------------------ tuning
        static void RootsSP(double f, double zeta, double rate, out double S, out double P)
        {
            double w = 2 * Math.PI * f, h = 1 / rate;
            if (zeta < 1) { double wd = w * Math.Sqrt(1 - zeta * zeta); S = 2 * Math.Exp(-zeta * w * h) * Math.Cos(wd * h); P = Math.Exp(-2 * zeta * w * h); return; }
            double r = Math.Sqrt(zeta * zeta - 1);
            S = Math.Exp((-zeta + r) * w * h) + Math.Exp((-zeta - r) * w * h); P = Math.Exp(-2 * zeta * w * h);
        }
        /// <summary>(frequency Hz, damping ratio) at rate -> (damping, elasticity) that sample that oscillator exactly.</summary>
        public static void TuningFromPhysical(double f, double zeta, double rate, string model, out double damping, out double elasticity)
        {
            RootsSP(f, zeta, rate, out double S, out double P);
            if (model == "FORCE") { damping = 1 - P; elasticity = 1 + P - S; }
            else { elasticity = 1 - (S - P); damping = 1 - P / (S - P); }
        }
        /// <summary>What oscillator (damping, elasticity) at rate behaves like for small motion: (frequency Hz, damping ratio).</summary>
        public static double[] PhysicalFromTuning(double d, double e, double rate, string model)
        {
            double h = 1 / rate, S = model == "FORCE" ? 2 - d - e : (1 - e) * (2 - d), P = model == "FORCE" ? 1 - d : (1 - e) * (1 - d), disc = S * S - 4 * P;
            if (P <= 0) return new[] { double.PositiveInfinity, double.PositiveInfinity };
            if (disc < 0)
            {
                double r = Math.Sqrt(P), th = Math.Atan2(Math.Sqrt(-disc), S), sigma = Math.Log(r) / h, wd = th / h, w = Math.Sqrt(sigma * sigma + wd * wd);
                return new[] { w / (2 * Math.PI), -sigma / w };
            }
            double l1 = (S + Math.Sqrt(disc)) / 2, l2 = (S - Math.Sqrt(disc)) / 2;
            if (l1 <= 0 || l2 <= 0) return new[] { double.NaN, double.NaN };
            double s1 = Math.Log(l1) / h, s2 = Math.Log(l2) / h, ww = Math.Sqrt(s1 * s2);
            return new[] { ww / (2 * Math.PI), -(s1 + s2) / (2 * ww) };
        }

        // ------------------------------------------------------------------------------------------------ colliders
        sealed class Prepared { public string Shape; public bool Inside; public double[] Center, Dir, A, B; public double R0, R1; }
        static int AxisIndex(string a) { return a == "X" ? 0 : a == "Z" ? 2 : 1; }
        static Prepared Prepare(SpringCollider c)
        {
            var W = c.World ?? Affine.Identity; double sc = Len(M3Col(W.L, 0));
            var o = new Prepared { Shape = c.Shape ?? "SPHERE", Inside = c.Bound == "INSIDE", Center = ApplyPoint(W, c.Center) };
            var ax = c.Axis ?? new double[] { 0, 0, 0 };
            o.Dir = (ax[0] != 0 || ax[1] != 0 || ax[2] != 0) ? Unit(M3V(W.L, ax)) : Unit(M3Col(W.L, AxisIndex(c.Direction)));
            o.R0 = c.Radius * sc; o.R1 = (c.Radius2 > 0 ? c.Radius2 : c.Radius) * sc; double h = c.Height * sc;
            if (o.Shape == "CAPSULE")
            {
                double seg = Math.Max(h - o.R0 - o.R1, 0);
                o.A = Add(o.Center, Mul(o.Dir, -(h / 2 - o.R0))); o.B = Add(o.Center, Mul(o.Dir, h / 2 - o.R1));   // caps inside height, centred
                if (seg <= 0) { o.Shape = "SPHERE"; o.R0 = Math.Max(o.R0, o.R1); }
            }
            return o;
        }
        static bool SphereBound(ref double[] p, double[] q, double R, double pr, bool inside)
        {
            var d = Sub(p, q); double l = Len(d);
            if (!inside)
            {
                double m = R + pr;
                if (l < m) { var n = l > 1e-300 ? Mul(d, 1 / l) : new double[] { 0, 1, 0 }; p = Add(q, Mul(n, m)); return true; }
                return false;
            }
            double mi = R - pr;
            if (l > mi) { var n = l > 1e-300 ? Mul(d, 1 / l) : new double[] { 0, 1, 0 }; p = Add(q, Mul(n, Math.Max(mi, 0))); return true; }
            return false;
        }
        static void RoundCone(double[] p, double[] a, double[] b, double r0, double r1, out double[] q, out double R)
        {
            var ab = Sub(b, a); double L = Len(ab);
            if (L < 1e-300) { q = a; R = Math.Max(r0, r1); return; }
            var u = Mul(ab, 1 / L); var ap = Sub(p, a); double tp = Dot(ap, u); double qq = Len(Sub(ap, Mul(u, tp))); double k = (r0 - r1) / L;
            if (Math.Abs(k) >= 1) { if (r0 >= r1) { q = a; R = r0; } else { q = b; R = r1; } return; }
            double t = tp - k * qq / Math.Sqrt(1 - k * k); t = t < 0 ? 0 : t > L ? L : t;
            q = Add(a, Mul(u, t)); R = r0 - k * t;
        }
        static bool Collide(Prepared c, ref double[] p, double pr)
        {
            if (c.Shape == "SPHERE") return SphereBound(ref p, c.Center, c.R0, pr, c.Inside);
            if (c.Shape == "CAPSULE") { RoundCone(p, c.A, c.B, c.R0, c.R1, out double[] q, out double R); return SphereBound(ref p, q, R, pr, c.Inside); }
            if (c.Shape == "PLANE")
            {
                double sd = Dot(Sub(p, c.Center), c.Dir);
                if (!c.Inside) { if (sd < pr) { p = Add(p, Mul(c.Dir, pr - sd)); return true; } }
                else if (sd > -pr) { p = Add(p, Mul(c.Dir, -pr - sd)); return true; }
            }
            return false;
        }
        static double Gust(double time, double depth, double seed)
        {
            double s = seed * 12.9898;
            return Math.Sin(time * 1.000 + depth * 2.1 + s) * 0.5 + Math.Sin(time * 2.718 + depth * 1.3 + s * 1.7) * 0.3 + Math.Sin(time * 5.393 + depth * 0.7 + s * 2.3) * 0.2;
        }

        // ------------------------------------------------------------------------------------------------ state
        sealed class Particle
        {
            public int Joint, Parent, Children; public double[] EndOffset;
            public double Depth, Damping, Elasticity, Stiffness, Inert, Friction, Radius, AngleLimit, Stretch;
            public double[] Pos, Prev, Last, FramePiv; public bool Hit;
        }
        sealed class Tree { public List<Particle> Parts = new List<Particle>(); public double[] LocalGravity; }
        sealed class Anim { public List<Affine> W = new List<Affine>(); public List<double[]> Piv = new List<double[]>(); }

        public readonly SpringParams P;
        readonly List<SpringTreeDesc> desc;
        List<Tree> trees;
        bool ready;
        double acc, time;
        double[] refAtStep;
        public int Steps; public double Dropped;
        public double EquivalentFrequency, EquivalentDampingRatio;

        public SpringChain(List<SpringTreeDesc> trees, SpringParams p) { desc = trees; P = p; Tune(); }

        void Tune()
        {
            if (P.Frequency > 0)
            {
                TuningFromPhysical(P.Frequency, P.DampingRatio, P.UpdateRate, P.SpringModel, out double d, out double e);
                P.Damping = Clamp01(d); P.Elasticity = Clamp01(e);
            }
            var eq = PhysicalFromTuning(P.Damping, P.Elasticity, P.UpdateRate, P.SpringModel);
            EquivalentFrequency = eq[0]; EquivalentDampingRatio = eq[1];
        }
        /// <summary>Call after changing P while running: the particles keep their state.</summary>
        public void Retune() { Tune(); if (trees != null) foreach (var T in trees) foreach (var pt in T.Parts) Coefficients(pt); }

        void Coefficients(Particle pt)
        {
            double a = pt.Depth;
            pt.Damping = Clamp01(P.Damping * EvalCurve(P.DampingCurve, a));
            pt.Elasticity = Clamp01(P.Elasticity * EvalCurve(P.ElasticityCurve, a));
            pt.Stiffness = Clamp01(P.Stiffness * EvalCurve(P.StiffnessCurve, a));
            pt.Inert = Clamp01(P.Inert * EvalCurve(P.InertCurve, a));
            pt.Friction = Clamp01(P.Friction * EvalCurve(P.FrictionCurve, a));
            pt.Radius = Math.Max(0, P.Radius * EvalCurve(P.RadiusCurve, a));
            pt.AngleLimit = Math.Max(0, P.AngleLimit * EvalCurve(P.AngleLimitCurve, a)) * Math.PI / 180;
            pt.Stretch = Clamp01(P.Stretch * EvalCurve(P.StretchCurve, a));
        }

        List<Anim> Animate(List<SpringTreeInput> input)
        {
            var output = new List<Anim>();
            for (int ti = 0; ti < desc.Count; ti++)
            {
                var a = new Anim(); var tin = input[ti];
                for (int k = 0; k < desc[ti].Joints.Count; k++)
                {
                    var j = desc[ti].Joints[k];
                    var pw = j.Parent < 0 ? (tin.ParentWorld ?? Affine.Identity) : a.W[j.Parent];
                    var w = Compose(pw, FromFields(tin.Pose[k]));
                    a.W.Add(w); a.Piv.Add(ApplyPoint(w, tin.Pose[k].Center));
                }
                output.Add(a);
            }
            return output;
        }
        double[] AnimPos(Particle pt, List<Particle> parts, Anim A)
        {
            if (pt.Joint >= 0) return A.Piv[pt.Joint];
            int pj = parts[pt.Parent].Joint;
            return Add(A.Piv[pj], M3V(A.W[pj].L, pt.EndOffset));
        }
        static double[] LocalOffset(Particle pt, List<Particle> parts, List<SpringJointPose> pose)
        {
            if (pt.Joint < 0) return pt.EndOffset;
            int pj = parts[pt.Parent].Joint;
            return Sub(Add(pose[pt.Joint].Translation, pose[pt.Joint].Center), pose[pj].Center);
        }

        void Setup(List<Anim> anim)
        {
            trees = new List<Tree>();
            for (int ti = 0; ti < desc.Count; ti++)
            {
                var td = desc[ti]; var A = anim[ti]; var T = new Tree(); var parts = T.Parts;
                var jp = new int[td.Joints.Count];
                for (int k = 0; k < td.Joints.Count; k++)
                {
                    var j = td.Joints[k]; int pp = j.Parent < 0 ? -1 : jp[j.Parent];
                    parts.Add(new Particle { Joint = k, Parent = pp }); jp[k] = parts.Count - 1;
                    if (pp >= 0) parts[pp].Children++;
                }
                var hasChild = new bool[td.Joints.Count];
                foreach (var j in td.Joints) if (j.Parent >= 0) hasChild[j.Parent] = true;
                for (int k = 0; k < td.Joints.Count; k++)
                {
                    var j = td.Joints[k];
                    if (hasChild[k] && j.EndOffset == null) continue;
                    double[] off = null;
                    if (j.EndOffset != null) off = (double[])j.EndOffset.Clone();
                    else if (P.EndLength > 0)
                    {
                        if (j.Parent >= 0) off = Mul(M3V(M3Inv(A.W[k].L), Sub(A.Piv[k], A.Piv[j.Parent])), P.EndLength);
                        else off = new[] { P.EndLength, 0, 0 };
                    }
                    else if (P.EndOffset[0] != 0 || P.EndOffset[1] != 0 || P.EndOffset[2] != 0) off = (double[])P.EndOffset.Clone();
                    if (off == null) continue;
                    parts.Add(new Particle { Joint = -1, Parent = jp[k], EndOffset = off }); parts[jp[k]].Children++;
                }
                var pos0 = new List<double[]>(); foreach (var pt in parts) pos0.Add(AnimPos(pt, parts, A));
                var dl = new double[parts.Count]; double total = 0;
                for (int i = 0; i < parts.Count; i++) if (parts[i].Parent >= 0) { dl[i] = dl[parts[i].Parent] + Len(Sub(pos0[i], pos0[parts[i].Parent])); total = Math.Max(total, dl[i]); }
                for (int i = 0; i < parts.Count; i++)
                {
                    var pt = parts[i]; pt.Depth = total > 0 ? dl[i] / total : 0; Coefficients(pt);
                    pt.Pos = (double[])pos0[i].Clone(); pt.Prev = (double[])pos0[i].Clone(); pt.Last = (double[])pos0[i].Clone(); pt.Hit = false;
                }
                T.LocalGravity = M3V(M3T(M3FromQuat(QuatOfLinear(A.W[0].L))), P.Gravity);
                trees.Add(T);
            }
            refAtStep = null; ready = true;
        }

        /// <summary>One frame: dt seconds since the last, the animated pose of every tree, the inert reference and colliders.</summary>
        public SpringFrameOutput Frame(double dt, double? frameTime, List<SpringTreeInput> input, Affine reference, List<SpringCollider> colliders)
        {
            var anim = Animate(input);
            if (!ready) Setup(anim);
            var refA = reference ?? input[0].ParentWorld ?? Affine.Identity;
            var refOrigin = refA.T; double objectScale = Len(M3Col(refA.L, 0));
            if (refAtStep == null) refAtStep = (double[])refOrigin.Clone();
            var objectMove = Sub(refOrigin, refAtStep);
            var cs = new List<Prepared>(); if (colliders != null) foreach (var c in colliders) cs.Add(Prepare(c));
            time = frameTime ?? time + dt;
            if (P.ResetDistance > 0 && Len(objectMove) > P.ResetDistance)
            {
                for (int ti = 0; ti < trees.Count; ti++) foreach (var pt in trees[ti].Parts) { var a = AnimPos(pt, trees[ti].Parts, anim[ti]); pt.Pos = (double[])a.Clone(); pt.Prev = (double[])a.Clone(); pt.Last = (double[])a.Clone(); }
                objectMove = new double[] { 0, 0, 0 }; refAtStep = (double[])refOrigin.Clone();
            }
            int loops = 1; double timeVar = 1;
            if (!P.Enabled || P.Weight <= 0) loops = -1;
            else if (P.TimeMode == "VARIABLE") { timeVar = P.UpdateRate > 0 ? dt * P.UpdateRate : 1; if (dt <= 0) loops = 0; }
            else
            {
                double h = 1 / P.UpdateRate; acc += dt; loops = 0;
                while (acc >= h - 1e-12) { acc -= h; if (++loops >= P.MaxSubsteps) { Dropped += Math.Max(acc, 0); acc = 0; break; } }
            }
            bool collided = false;
            if (loops > 0)
            {
                for (int k = 0; k < loops; k++)
                {
                    double tStep = P.TimeMode == "FIXED" ? (Steps + 1) / P.UpdateRate : time;
                    foreach (var T in trees) foreach (var pt in T.Parts) pt.Last = (double[])pt.Pos.Clone();
                    double share = (k + 1.0) / loops;
                    for (int ti = 0; ti < trees.Count; ti++)
                        collided = Step(trees[ti], anim[ti], input[ti].Pose, timeVar, k == 0 ? objectMove : new double[] { 0, 0, 0 }, objectScale, cs, tStep, loops > 1 && P.SubstepRoots ? share : 1) || collided;
                    Steps++;
                }
                refAtStep = (double[])refOrigin.Clone();
            }
            var carry = loops == 0 ? objectMove : new double[] { 0, 0, 0 };
            double alpha = (P.Interpolate && P.TimeMode == "FIXED") ? Clamp01(acc * P.UpdateRate) : 1;
            var output = new SpringFrameOutput { Steps = loops > 0 ? loops : 0, DroppedTime = Dropped, Collided = collided };
            for (int ti = 0; ti < trees.Count; ti++)
            {
                var parts = trees[ti].Parts; var show = new double[parts.Count][];
                for (int i = 0; i < parts.Count; i++)
                {
                    var pt = parts[i];
                    if (pt.Parent < 0) { show[i] = anim[ti].Piv[pt.Joint]; continue; }
                    var b = alpha < 1 ? new[] { Lerp(pt.Last[0], pt.Pos[0], alpha), Lerp(pt.Last[1], pt.Pos[1], alpha), Lerp(pt.Last[2], pt.Pos[2], alpha) } : pt.Pos;
                    show[i] = Add(b, carry);
                }
                output.Trees.Add(loops < 0 ? PassThrough(trees[ti], input[ti]) : Apply(trees[ti], desc[ti], input[ti], show));
            }
            return output;
        }

        double RestLen(Particle pt, Tree T, Anim A)
        {
            var pp = T.Parts[pt.Parent];
            return pt.Joint >= 0 ? Len(Sub(A.Piv[pp.Joint], A.Piv[pt.Joint])) : Len(M3V(A.W[pp.Joint].L, pt.EndOffset));
        }

        bool Step(Tree T, Anim A, List<SpringJointPose> pose, double timeVar, double[] objectMove, double objectScale, List<Prepared> cs, double t, double share)
        {
            double units = P.ForceUnits == "M_PER_S2" ? 1 / (P.UpdateRate * P.UpdateRate) : 1;
            var g = Mul(P.Gravity, units);
            if (P.GravityRestCompensation)
            {
                var restG = Mul(M3V(M3FromQuat(QuatOfLinear(A.W[0].L)), T.LocalGravity), units);
                var fdir = Unit(g); g = Sub(g, Mul(fdir, Math.Max(Dot(restG, fdir), 0)));
            }
            var baseF = Add(g, Mul(P.Force, units));
            bool windOn = P.Wind[0] != 0 || P.Wind[1] != 0 || P.Wind[2] != 0, hit = false;
            var parts = T.Parts;
            foreach (var pt in parts)
            {
                if (pt.Parent < 0)
                {
                    pt.Prev = pt.Pos; var target = A.Piv[pt.Joint];
                    pt.Pos = share < 1 && pt.FramePiv != null ? Add(pt.FramePiv, Mul(Sub(target, pt.FramePiv), share)) : (double[])target.Clone();
                    if (share >= 1) pt.FramePiv = (double[])target.Clone();
                    continue;
                }
                var v = Sub(pt.Pos, pt.Prev); var rmove = Mul(objectMove, pt.Inert);
                pt.Prev = Add(pt.Pos, rmove);
                double damping = pt.Damping;
                if (pt.Hit) { damping = Math.Min(1, damping + pt.Friction); pt.Hit = false; }
                var f = baseF;
                if (windOn) f = Add(f, Mul(P.Wind, units * (1 + P.WindTurbulence * Gust(t * P.WindFrequency * 2 * Math.PI, pt.Depth * 6, P.Seed))));
                pt.Pos = Add(Add(pt.Pos, Mul(v, 1 - damping)), Add(Mul(f, objectScale * timeVar), rmove));
            }
            for (int i = 1; i < parts.Count; i++)
            {
                var pt = parts[i]; if (pt.Parent < 0) continue;
                var p0 = parts[pt.Parent]; double restLen = RestLen(pt, T, A);
                double stiff = Lerp(1, pt.Stiffness, P.Weight);
                if (stiff > 0 || pt.Elasticity > 0)
                {
                    var o = LocalOffset(pt, parts, pose); var restPos = Add(p0.Pos, M3V(A.W[p0.Joint].L, o));
                    var d = Sub(restPos, P.SpringModel == "FORCE" ? pt.Last : pt.Pos);
                    pt.Pos = Add(pt.Pos, Mul(d, pt.Elasticity * timeVar));
                    if (stiff > 0)
                    {
                        d = Sub(restPos, pt.Pos); double l = Len(d), maxlen = restLen * (1 - stiff) * 2;
                        if (l > maxlen) pt.Pos = Add(pt.Pos, Mul(d, (l - maxlen) / l));
                    }
                }
                if (cs.Count > 0)
                {
                    double pr = pt.Radius * objectScale;
                    foreach (var c in cs) { var p = pt.Pos; if (Collide(c, ref p, pr)) { pt.Hit = true; hit = true; } pt.Pos = p; }
                }
                if (P.FreezeAxis != "NONE")
                {
                    var n = Unit(M3Col(A.W[p0.Joint].L, AxisIndex(P.FreezeAxis)));
                    pt.Pos = Sub(pt.Pos, Mul(n, Dot(Sub(pt.Pos, p0.Pos), n)));
                }
                var dd = Sub(p0.Pos, pt.Pos); double ll = Len(dd);
                if (ll > 0) pt.Pos = Add(pt.Pos, Mul(dd, ((ll - restLen) / ll) * (1 - pt.Stretch)));
                if (pt.AngleLimit > 0)
                {
                    var o = LocalOffset(pt, parts, pose); var rd = Unit(M3V(A.W[p0.Joint].L, o)); var sv = Sub(pt.Pos, p0.Pos); double sl = Len(sv);
                    if (sl > 0)
                    {
                        var sd = Mul(sv, 1 / sl); double c = Math.Max(-1, Math.Min(1, Dot(rd, sd))), ang = Math.Acos(c);
                        if (ang > pt.AngleLimit)
                        {
                            var ax = Len(Cross(rd, sd)) > 1e-12 ? Cross(rd, sd) : new double[] { 1, 0, 0 };
                            pt.Pos = Add(p0.Pos, Mul(M3V(M3FromQuat(QFromAxisAngle(ax, pt.AngleLimit)), rd), sl));
                        }
                    }
                }
            }
            return hit;
        }

        static SpringTreeOutput PassThrough(Tree T, SpringTreeInput tin)
        {
            var o = new SpringTreeOutput { Rotations = new double[tin.Pose.Count][], Translations = new double[tin.Pose.Count][], Positions = new double[T.Parts.Count][] };
            for (int i = 0; i < T.Parts.Count; i++) o.Positions[i] = (double[])T.Parts[i].Pos.Clone();
            return o;
        }

        SpringTreeOutput Apply(Tree T, SpringTreeDesc td, SpringTreeInput tin, double[][] show)
        {
            int n = td.Joints.Count;
            var o = new SpringTreeOutput { Rotations = new double[n][], Translations = new double[n][], Positions = new double[T.Parts.Count][] };
            var W = new Affine[n]; var local = new List<SpringJointPose>(); foreach (var f in tin.Pose) local.Add(f.Clone());
            var kids = new List<int>[n]; for (int j = 0; j < n; j++) kids[j] = new List<int>();
            for (int i = 0; i < T.Parts.Count; i++) if (T.Parts[i].Parent >= 0) kids[T.Parts[T.Parts[i].Parent].Joint].Add(i);
            for (int j = 0; j < n; j++)
            {
                var pw = td.Joints[j].Parent < 0 ? (tin.ParentWorld ?? Affine.Identity) : W[td.Joints[j].Parent];
                var cur = Compose(pw, FromFields(local[j])); var ks = kids[j];
                if (ks.Count == 1 || (ks.Count > 1 && P.BranchMode == "MEAN"))
                {
                    var pivot = ApplyPoint(cur, local[j].Center); var v0 = new double[] { 0, 0, 0 }; var v1 = new double[] { 0, 0, 0 };
                    foreach (int i in ks) { var off = LocalOffset(T.Parts[i], T.Parts, local); v0 = Add(v0, M3V(cur.L, off)); v1 = Add(v1, Sub(show[i], pivot)); }
                    if (Len(v0) > 1e-12 && Len(v1) > 1e-12)
                    {
                        var Q = QFromTo(v0, v1); var qA = QuatOfLinear(pw.L); var rAnim = QNorm(local[j].Rotation);
                        var r = QMul(QConj(qA), QMul(Q, QMul(qA, rAnim)));
                        if (P.Weight < 1) r = QSlerp(rAnim, r, Clamp01(P.Weight));
                        local[j].Rotation = r; o.Rotations[j] = r; cur = Compose(pw, FromFields(local[j]));
                    }
                    foreach (int i in ks)
                    {
                        var pt = T.Parts[i]; if (pt.Joint < 0 || pt.Stretch <= 0) continue;
                        var pv = ApplyPoint(cur, local[j].Center); var o2 = M3V(M3Inv(cur.L), Sub(show[i], pv));
                        var tk = Sub(Add(o2, local[j].Center), local[pt.Joint].Center);
                        local[pt.Joint].Translation = tk; o.Translations[pt.Joint] = tk;
                    }
                }
                W[j] = cur;
            }
            for (int i = 0; i < T.Parts.Count; i++) o.Positions[i] = (double[])show[i].Clone();
            return o;
        }

        /// <summary>FNV-1a over positions rounded to 1 nm, rounding half up as JavaScript's Math.round does.</summary>
        public string StateHash()
        {
            uint h = 0x811c9dc5;
            if (trees != null)
                foreach (var T in trees) foreach (var pt in T.Parts) foreach (var x in pt.Pos)
                        {
                            string s = ((long)Math.Floor(x * 1e9 + 0.5)).ToString(CultureInfo.InvariantCulture);
                            foreach (char ch in s) { h ^= ch; h = unchecked(h * 0x01000193); }
                        }
            return h.ToString("x8", CultureInfo.InvariantCulture);
        }
    }
}

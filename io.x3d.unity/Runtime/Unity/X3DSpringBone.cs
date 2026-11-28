using System.Collections.Generic;
using UnityEngine;
using X3D.Core;

namespace X3D.UnityBridge
{
    /// <summary>
    /// SpringBoneChain (x3d.loom x3d.springbone) in Unity: secondary motion on Transform chains, solved by the same
    /// code as X_ITE and Blender (X3D.Core.SpringChain). Runs in Unity world space after the Animator (LateUpdate).
    /// Vectors here (gravity, force, wind) are Unity world vectors; X3D's +Y up is Unity's +Y up.
    /// Own code. Status: Suspected until compiled and run in a Unity Editor (GameCI waits on the licence secrets).
    /// </summary>
    [AddComponentMenu("X3D/X3D Spring Bone Chain")]
    [DefaultExecutionOrder(10000)]
    public sealed class X3DSpringBone : MonoBehaviour
    {
        public List<Transform> roots = new List<Transform>();
        public List<Transform> exclusions = new List<Transform>();
        public List<X3DSpringBoneCollider> colliders = new List<X3DSpringBoneCollider>();
        [Tooltip("Frame whose motion `inert` cancels; default: this object.")] public Transform reference;

        public float updateRate = 60; public string timeMode = "FIXED"; public int maxSubsteps = 3; public bool interpolate;
        [Tooltip("POSITION (the common Unity asset's behaviour) or FORCE (any damping ratio).")] public string springModel = "POSITION";
        [Range(0, 1)] public float damping = 0.1f, elasticity = 0.1f, stiffness = 0.1f, inert, friction;
        public float radius, angleLimit; [Range(0, 1)] public float stretch;
        public AnimationCurve dampingCurve, elasticityCurve, stiffnessCurve, inertCurve, frictionCurve, radiusCurve, angleLimitCurve, stretchCurve;
        [Tooltip("Physical tuning: above 0 sets damping and elasticity exactly.")] public float frequency; public float dampingRatio = 0.2f;
        public float endLength; public Vector3 endOffset, gravity, force, wind;
        [Tooltip("PER_STEP (the common Unity asset's units) or M_PER_S2.")] public string forceUnits = "PER_STEP";
        public bool gravityRestCompensation = true; public float windTurbulence, windFrequency = 1; public int seed = 1;
        public string freezeAxis = "NONE", branchMode = "SKIP"; [Range(0, 1)] public float weight = 1; public float resetDistance; public bool substepRoots = true;

        [Header("Read only")] public float equivalentFrequency; public float equivalentDampingRatio; public string stateHash; public int steps;

        SpringChain chain; readonly List<List<Transform>> bones = new List<List<Transform>>();
        readonly List<List<Quaternion>> written = new List<List<Quaternion>>(), animated = new List<List<Quaternion>>();

        static double[] V(Vector3 v) { return new double[] { v.x, v.y, v.z }; }
        static double[] Q(Quaternion q) { return new double[] { q.x, q.y, q.z, q.w }; }
        static Affine A(Matrix4x4 m) { return new Affine(new double[] { m.m00, m.m01, m.m02, m.m10, m.m11, m.m12, m.m20, m.m21, m.m22 }, new double[] { m.m03, m.m13, m.m23 }); }
        static double[][] Sample(AnimationCurve c)
        {   // the Unity curve is Hermite; SpringBoneChain keys are linear, so sample 11 depths
            if (c == null || c.length == 0) return new double[0][];
            var k = new double[11][]; for (int i = 0; i <= 10; i++) k[i] = new double[] { i / 10.0, c.Evaluate(i / 10f) }; return k;
        }

        public SpringParams Params()
        {
            return new SpringParams
            {
                UpdateRate = updateRate, TimeMode = timeMode, MaxSubsteps = maxSubsteps, Interpolate = interpolate, SpringModel = springModel,
                Damping = damping, Elasticity = elasticity, Stiffness = stiffness, Inert = inert, Friction = friction, Radius = radius, AngleLimit = angleLimit, Stretch = stretch,
                DampingCurve = Sample(dampingCurve), ElasticityCurve = Sample(elasticityCurve), StiffnessCurve = Sample(stiffnessCurve), InertCurve = Sample(inertCurve),
                FrictionCurve = Sample(frictionCurve), RadiusCurve = Sample(radiusCurve), AngleLimitCurve = Sample(angleLimitCurve), StretchCurve = Sample(stretchCurve),
                Frequency = frequency, DampingRatio = dampingRatio, EndLength = endLength, EndOffset = V(endOffset), Gravity = V(gravity), Force = V(force),
                ForceUnits = forceUnits, GravityRestCompensation = gravityRestCompensation, Wind = V(wind), WindTurbulence = windTurbulence, WindFrequency = windFrequency,
                Seed = seed, FreezeAxis = freezeAxis, BranchMode = branchMode, Weight = weight, ResetDistance = resetDistance, SubstepRoots = substepRoots,
            };
        }

        public void Rebuild()
        {
            bones.Clear(); written.Clear(); animated.Clear();
            var trees = new List<SpringTreeDesc>();
            foreach (var r in roots)
            {
                if (r == null) continue;
                var list = new List<Transform>(); var td = new SpringTreeDesc();
                void Visit(Transform t, int parent)
                {
                    int k = list.Count; list.Add(t); td.Joints.Add(new SpringJoint { Name = t.name, Parent = parent });
                    for (int i = 0; i < t.childCount; i++) { var c = t.GetChild(i); if (!exclusions.Contains(c)) Visit(c, k); }
                }
                Visit(r, -1);
                bones.Add(list); trees.Add(td);
                written.Add(new List<Quaternion>(new Quaternion[list.Count])); animated.Add(new List<Quaternion>(new Quaternion[list.Count]));
            }
            chain = new SpringChain(trees, Params());
            equivalentFrequency = (float)chain.EquivalentFrequency; equivalentDampingRatio = (float)chain.EquivalentDampingRatio;
        }

        void OnEnable() { Rebuild(); }

        void LateUpdate()
        {
            if (chain == null) Rebuild();
            var input = new List<SpringTreeInput>();
            for (int ti = 0; ti < bones.Count; ti++)
            {
                var tin = new SpringTreeInput { ParentWorld = A(bones[ti][0].parent != null ? bones[ti][0].parent.localToWorldMatrix : Matrix4x4.identity) };
                for (int j = 0; j < bones[ti].Count; j++)
                {
                    var b = bones[ti][j];
                    // an Animator writes every frame; without one, the rotation we wrote last frame is not a new pose
                    if (b.localRotation != written[ti][j] || animated[ti][j] == default(Quaternion)) animated[ti][j] = b.localRotation;
                    tin.Pose.Add(new SpringJointPose { Translation = V(b.localPosition), Rotation = Q(animated[ti][j]), Scale = V(b.localScale) });
                }
                input.Add(tin);
            }
            var cs = new List<SpringCollider>();
            foreach (var c in colliders) if (c != null && c.isActiveAndEnabled) cs.Add(c.ToCore());
            var refT = reference != null ? reference : transform;
            var output = chain.Frame(Time.deltaTime, Time.time, input, A(refT.localToWorldMatrix), cs);
            for (int ti = 0; ti < bones.Count; ti++)
                for (int j = 0; j < bones[ti].Count; j++)
                {
                    var q = output.Trees[ti].Rotations[j];
                    if (q != null) { bones[ti][j].localRotation = new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]); written[ti][j] = bones[ti][j].localRotation; }
                    var tr = output.Trees[ti].Translations[j];
                    if (tr != null) bones[ti][j].localPosition = new Vector3((float)tr[0], (float)tr[1], (float)tr[2]);
                }
            steps = chain.Steps; stateHash = chain.StateHash();
        }
    }
}

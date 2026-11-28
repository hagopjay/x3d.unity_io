using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace X3D.UnityBridge.Editor
{
    /// <summary>
    /// Moves spring-bone rigs between a licensed copy of the popular Unity spring-bone asset (the user's own, in their
    /// own project; nothing of it ships here) and X3DSpringBone, by reflection on its public field names only.
    /// Parameter names carry over one to one (x3d.springbone/SPEC.md section 8); Hermite curves are sampled at 11 depths;
    /// its Default update mode becomes VARIABLE, the others FIXED. Tapered capsules: the asset puts radius at the +axis
    /// end and radius2 at the -axis end, SpringBoneChain the other way round, so the two are swapped.
    /// Status: Suspected until run in a Unity Editor with the asset installed.
    /// </summary>
    public static class SpringBoneMapping
    {
        static Type Find(string name) { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null); }
        static T Get<T>(object o, string field, T fallback)
        {
            var f = o.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            return f != null && f.GetValue(o) is T v ? v : fallback;
        }

        [MenuItem("X3D/Spring Bones/Convert asset spring bones to X3DSpringBone (selection)")]
        public static void ConvertSelection()
        {
            var type = Find("DynamicBone");
            if (type == null) { Debug.LogWarning("[X3D] No spring-bone asset component type found in this project."); return; }
            var colliderType = Find("DynamicBoneColliderBase");
            int made = 0;
            foreach (var go in Selection.gameObjects)
                foreach (var src in go.GetComponents(type))
                {
                    var dst = Undo.AddComponent<X3DSpringBone>(go);
                    var root = Get<Transform>(src, "m_Root", null); if (root != null) dst.roots.Add(root);
                    var roots = Get<System.Collections.IList>(src, "m_Roots", null); if (roots != null) foreach (var r in roots) if (r is Transform t && !dst.roots.Contains(t)) dst.roots.Add(t);
                    dst.updateRate = Get(src, "m_UpdateRate", 60f);
                    dst.timeMode = Get<object>(src, "m_UpdateMode", null)?.ToString() == "Default" ? "VARIABLE" : "FIXED";
                    dst.damping = Get(src, "m_Damping", 0.1f); dst.elasticity = Get(src, "m_Elasticity", 0.1f); dst.stiffness = Get(src, "m_Stiffness", 0.1f);
                    dst.inert = Get(src, "m_Inert", 0f); dst.friction = Get(src, "m_Friction", 0f); dst.radius = Get(src, "m_Radius", 0f);
                    dst.dampingCurve = Get<AnimationCurve>(src, "m_DampingDistrib", null); dst.elasticityCurve = Get<AnimationCurve>(src, "m_ElasticityDistrib", null);
                    dst.stiffnessCurve = Get<AnimationCurve>(src, "m_StiffnessDistrib", null); dst.inertCurve = Get<AnimationCurve>(src, "m_InertDistrib", null);
                    dst.frictionCurve = Get<AnimationCurve>(src, "m_FrictionDistrib", null); dst.radiusCurve = Get<AnimationCurve>(src, "m_RadiusDistrib", null);
                    dst.endLength = Get(src, "m_EndLength", 0f); dst.endOffset = Get(src, "m_EndOffset", Vector3.zero);
                    dst.gravity = Get(src, "m_Gravity", Vector3.zero); dst.force = Get(src, "m_Force", Vector3.zero); dst.weight = Get(src, "m_BlendWeight", 1f);
                    dst.freezeAxis = (Get<object>(src, "m_FreezeAxis", null)?.ToString() ?? "None").ToUpperInvariant();
                    var ex = Get<System.Collections.IList>(src, "m_Exclusions", null); if (ex != null) foreach (var e in ex) if (e is Transform t) dst.exclusions.Add(t);
                    var cols = Get<System.Collections.IList>(src, "m_Colliders", null);
                    if (cols != null && colliderType != null)
                        foreach (var c in cols)
                        {
                            if (!(c is Component cc)) continue;
                            var x = Undo.AddComponent<X3DSpringBoneCollider>(cc.gameObject);
                            bool plane = c.GetType().Name.Contains("Plane");
                            x.shape = plane ? "PLANE" : (Get(c, "m_Height", 0f) > 2 * Get(c, "m_Radius", 0f) ? "CAPSULE" : "SPHERE");
                            x.bound = Get<object>(c, "m_Bound", null)?.ToString() == "Inside" ? "INSIDE" : "OUTSIDE";
                            x.direction = Get<object>(c, "m_Direction", null)?.ToString() ?? "Y";
                            x.center = Get(c, "m_Center", Vector3.zero);
                            float r = Get(c, "m_Radius", 0.5f), r2 = Get(c, "m_Radius2", 0f);
                            if (r2 > 0) { x.radius = r2; x.radius2 = r; } else { x.radius = r; x.radius2 = 0; }
                            x.height = Get(c, "m_Height", 0f);
                            dst.colliders.Add(x);
                        }
                    dst.Rebuild(); made++;
                }
            Debug.Log("[X3D] Converted " + made + " spring-bone component(s) to X3DSpringBone; disable the originals to compare side by side.");
        }
    }
}

using UnityEngine;
using X3D.Core;

namespace X3D.UnityBridge
{
    /// <summary>SpringBoneCollider in Unity: a sphere, (tapered) capsule or plane on this Transform. Height includes the caps, centred on center; radius at the -axis end, radius2 at the +axis end.</summary>
    [AddComponentMenu("X3D/X3D Spring Bone Collider")]
    public sealed class X3DSpringBoneCollider : MonoBehaviour
    {
        public string shape = "SPHERE", bound = "OUTSIDE", direction = "Y";
        public Vector3 center, axis; public float radius = 0.5f, radius2, height;
        public SpringCollider ToCore()
        {
            var m = transform.localToWorldMatrix;
            return new SpringCollider
            {
                Shape = shape, Bound = bound, Direction = direction, Center = new double[] { center.x, center.y, center.z }, Axis = new double[] { axis.x, axis.y, axis.z },
                Radius = radius, Radius2 = radius2, Height = height,
                World = new Affine(new double[] { m.m00, m.m01, m.m02, m.m10, m.m11, m.m12, m.m20, m.m21, m.m22 }, new double[] { m.m03, m.m13, m.m23 }),
            };
        }
    }
}

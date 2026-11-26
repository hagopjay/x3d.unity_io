using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using X3D.Core;
using X3D.UnityBridge;

// The Unity-API half of the bridge, run by GameCI in GitHub Actions (see .github/workflows/unity.yml).
// The pure-C# half has its own 41 tests under tests/X3D.Core.Tests.
public class X3DBridgeTests
{
    static string Sample()
    {
        var p = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../samples/scenes/hello-xr.x3d"));
        return File.ReadAllText(p);
    }

    [Test] public void HelloXr_BuildsWithZeroGaps()
    {
        var r = X3DSceneBuilder.Build(Sample());
        Assert.AreEqual(0, r.Receipt.Gaps.Count, string.Join(", ", r.Receipt.Gaps));
        Assert.AreEqual(762, r.Receipt.Triangles);
        Assert.AreEqual(6, r.Receipt.Points);
        Assert.AreEqual(64, r.Receipt.Sha256.Length);
        Object.DestroyImmediate(r.Root);
    }

    [Test] public void HelloXr_HasViewpointAndLight()
    {
        var r = X3DSceneBuilder.Build(Sample());
        Assert.AreEqual(1, r.Viewpoints.Count);
        Assert.AreEqual(1, r.Root.GetComponentsInChildren<Light>().Length);
        Object.DestroyImmediate(r.Root);
    }

    [Test] public void ZMirror_NegatesZ_AndKeepsMeshesOutward()
    {
        var r = X3DSceneBuilder.Build("<X3D profile='Interchange' version='4.0'><Scene><Transform DEF='T' translation='1 2 3'><Shape><Box/></Shape></Transform></Scene></X3D>");
        Assert.AreEqual(new Vector3(1, 2, -3), r.Transforms["T"].localPosition);
        var mesh = r.Root.GetComponentInChildren<MeshFilter>().sharedMesh;
        var md = new MeshData { Positions = Flat(mesh.vertices), Triangles = mesh.triangles };
        Assert.AreEqual(8.0, Convention.SignedVolume(md), 1e-3, "mirror + winding flip keeps the default Box outward (volume +8); inside-out would be negative");
        Object.DestroyImmediate(r.Root);
    }

    [Test] public void Animation_DrivesSpinnerAndMaterial()
    {
        var r = X3DSceneBuilder.Build(Sample());
        Assert.IsTrue(r.Transforms.ContainsKey("Spinner"));
        Assert.IsTrue(r.Materials.ContainsKey("CubeMat"));
        var ups = r.Animation.Evaluate(2.0);
        Assert.AreEqual(3, ups.Count);
        Object.DestroyImmediate(r.Root);
    }

    [Test] public void Export_ThenParse_KeepsShapeCount()
    {
        var r = X3DSceneBuilder.Build(Sample());
        List<string> gaps;
        var xml = X3DExporter.Export(r.Root, MirrorAxis.Z, out gaps);
        var back = X3DParser.Parse(xml);
        int shapes = 0;
        System.Action<X3DNode> walk = null;
        walk = n => { if (n.Type == "Shape") shapes++; foreach (var c in n.Children) walk(c); };
        foreach (var n in back.Roots) walk(n);
        Assert.AreEqual(r.Root.GetComponentsInChildren<MeshFilter>().Length, shapes);
        Object.DestroyImmediate(r.Root);
    }

    [Test] public void PointCloudBaker_PacksPositionsAndColours()
    {
        var m = new MeshData { Positions = new float[] { 1, 2, 3, 4, 5, 6 }, Colors = new float[] { 1, 0, 0, 1, 0, 1, 0, 1 }, IsPoints = true };
        Texture2D p, c; int n;
        X3DPointCloudBaker.Bake(m, out p, out c, out n);
        Assert.AreEqual(2, n);
        Assert.AreEqual(new Color(4, 5, 6, 1), p.GetPixel(1, 0));
        Assert.AreEqual(new Color(0, 1, 0, 1), c.GetPixel(1, 0));
    }

    static float[] Flat(Vector3[] v) { var f = new float[v.Length * 3]; for (int i = 0; i < v.Length; i++) { f[i * 3] = v[i].x; f[i * 3 + 1] = v[i].y; f[i * 3 + 2] = v[i].z; } return f; }
}

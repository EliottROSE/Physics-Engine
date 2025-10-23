using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EPA : MonoBehaviour
{
    const float eps = 1e-6f;
    
    public struct CollisionPair
    {
        public CustomRigidbody body1;
        public CustomRigidbody body2;
        
        public Vector3 point; // Point of collision
        public Vector3 normal; // normal of collision point
        public float penetration; // how far the rigidbodies enter in collision
    }

    #region Triangle Class
    
    public struct SupportPoint
    {
        public Vector3 minkowski; // sA - sB
        public Vector3 sA;        // support on collider1
        public Vector3 sB;        // support on collider2

        public SupportPoint(Vector3 _sA, Vector3 _sB)
        {
            sA = _sA;
            sB = _sB;
            minkowski = sA - sB;
        }
    }
    
    public static Vector3 ComputeBarycentrics(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a;
        Vector3 v1 = c - a;
        Vector3 v2 = p - a;

        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);

        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) < 1e-12f) return new Vector3(1f/3f,1f/3f,1f/3f); // fallback

        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1f - v - w;

        return new Vector3(u, v, w);
    }
    
    public class Triangle
    {
        public SupportPoint[] sp = new SupportPoint[3]; // 0=a,1=b,2=c
        private Vector3 normal;
        private bool isNormalComputed = false;

        public Triangle(SupportPoint p0, SupportPoint p1, SupportPoint p2)
        {
            Set(p0, p1, p2);
        }

        public void Set(SupportPoint p0, SupportPoint p1, SupportPoint p2)
        {
            sp[0] = p0; sp[1] = p1; sp[2] = p2;
            isNormalComputed = false;
        }

        public Vector3 a => sp[0].minkowski;
        public Vector3 b => sp[1].minkowski;
        public Vector3 c => sp[2].minkowski;

        public Vector3 GetNormal()
        {
            if (isNormalComputed) return normal;

            normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude <= 1e-12f) normal = Vector3.up;
            normal.Normalize();

            // Ensure normal points away from origin
            if (Vector3.Dot(normal, a) < 0f) normal = -normal;

            isNormalComputed = true;
            return normal;
        }
    }

    #endregion

    #region  Helper Methods
    public static int GetClosestFace(List<Triangle> faces)
    {
        int closest = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < faces.Count; i++)
        {
            Triangle face = faces[i];
            Vector3 n = face.GetNormal();

            float distance = Vector3.Dot(n, face.a);

            if (distance < minDistance)
            {
                minDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    public static Vector3Int Quantize(Vector3 p)
    {

        return new Vector3Int(
            Mathf.RoundToInt(p.x / eps),
            Mathf.RoundToInt(p.y / eps),
            Mathf.RoundToInt(p.z / eps)
        );
    }

    private static (Vector3Int, Vector3Int) EdgeKey(Vector3 p1, Vector3 p2)
    {
        var a = Quantize(p1);
        var b = Quantize(p2);

        if (a.x != b.x ? a.x < b.x : (a.y != b.y ? a.y < b.y : a.z <= b.z))
            return (a, b);
        return (b, a);
    }
    #endregion

    #region Polytope Methods
    // Build base tetrahedron from EPA first iteration
    public static List<Triangle> BuildGJKPolytop(List<SupportPoint> simplex)
    {
        List<Triangle> faces = new List<Triangle>(4);
        if (simplex == null || simplex.Count != 4) return faces;

        faces.Add(new Triangle(simplex[3], simplex[1], simplex[0]));
        faces.Add(new Triangle(simplex[1], simplex[2], simplex[0]));
        faces.Add(new Triangle(simplex[3], simplex[2], simplex[1]));
        faces.Add(new Triangle(simplex[2], simplex[3], simplex[0]));

        return faces;
    }

    // Rebuild the polytope with a new support point
    public static void ReBuildPolytop(ref List<Triangle> polytope, SupportPoint newSP)
    {
        Vector3 newPoint = newSP.minkowski;

        List<int> visibleFaces = new List<int>();
        for (int i = 0; i < polytope.Count; i++)
        {
            Triangle face = polytope[i];
            Vector3 normal = face.GetNormal();
            if (Vector3.Dot(normal, newPoint - face.a) > eps) visibleFaces.Add(i);
        }

        if (visibleFaces.Count == 0) return;

        var edgeMap = new Dictionary<(Vector3Int, Vector3Int), (SupportPoint A, SupportPoint B)>();

        void TryToggleEdge(SupportPoint p1, SupportPoint p2)
        {
            var key = EdgeKey(p1.minkowski, p2.minkowski);
            if (edgeMap.TryGetValue(key, out var existing))
            {
                // if reverse exists, remove internal edge
                if (existing.A.minkowski == p2.minkowski && existing.B.minkowski == p1.minkowski)
                    edgeMap.Remove(key);
            }
            else
            {
                edgeMap[key] = (p1, p2);
            }
        }

        foreach (int idx in visibleFaces.Distinct())
        {
            Triangle f = polytope[idx];
            TryToggleEdge(f.sp[0], f.sp[1]);
            TryToggleEdge(f.sp[1], f.sp[2]);
            TryToggleEdge(f.sp[2], f.sp[0]);
        }

        foreach (int idx in visibleFaces.Distinct().OrderByDescending(i => i))
            if (idx >= 0 && idx < polytope.Count) polytope.RemoveAt(idx);

        foreach (var kv in edgeMap.Values)
        {
            SupportPoint A = kv.A;
            SupportPoint B = kv.B;
            Triangle nf = new Triangle(A, B, newSP);

            Vector3 n = Vector3.Cross(nf.b - nf.a, nf.c - nf.a);
            if (n.sqrMagnitude <= 1e-12f) continue;

            n.Normalize();
            if (Vector3.Dot(n, nf.a) < 0f)
            {
                // flip winding => swap A,B in triangle (and their supports)
                nf.Set(B, A, newSP);
            }

            polytope.Add(nf);
        }
    }
    #endregion

    #region Main EPA Method
    
    public static SupportPoint MakeSupport(CustomCollider A, CustomCollider B, Vector3 dir)
    {
        Vector3 sA = A.GetSupport(dir);
        Vector3 sB = B.GetSupport(-dir);
        return new SupportPoint(sA, sB);
    }

    public static CollisionPair ExpendingPolytopeAlgorithm(CustomCollider collider1, CustomCollider collider2,
        List<SupportPoint> gjkSimplex, int maxIterations)
    {
        float scale = Mathf.Max(collider1.transform.lossyScale.magnitude, collider2.transform.lossyScale.magnitude);
        float tolerance = 1e-4f * Mathf.Max(1f, scale);

        List<Triangle> epaSimplex = EPA.BuildGJKPolytop(gjkSimplex);

        for (int i = 0; i < maxIterations; i++)
        {
            int closestFaceIndex = GetClosestFace(epaSimplex);
            Triangle closestFace = epaSimplex[closestFaceIndex];
            Vector3 normal = closestFace.GetNormal();

            float dist = Vector3.Dot(normal, closestFace.a);

            // compute support as SupportPoint (sA,sB) then minkowski
            Vector3 sA = collider1.GetSupport(normal);
            Vector3 sB = collider2.GetSupport(-normal);
            SupportPoint sp = new SupportPoint(sA, sB);
            float supportDist = Vector3.Dot(normal, sp.minkowski);

            if ((supportDist - dist) <= tolerance)
            {
                CollisionPair pair = new CollisionPair();
                pair.normal = normal;
                pair.penetration = Mathf.Max(dist, 0f);

                // projection of origin on face in Minkowski space
                Vector3 proj = normal * dist;
                Vector3 bary = ComputeBarycentrics(proj, closestFace.a, closestFace.b, closestFace.c);

                // interpolate supports
                Vector3 pA = bary.x * closestFace.sp[0].sA
                             + bary.y * closestFace.sp[1].sA
                             + bary.z * closestFace.sp[2].sA;

                Vector3 pB = bary.x * closestFace.sp[0].sB
                             + bary.y * closestFace.sp[1].sB
                             + bary.z * closestFace.sp[2].sB;

                pair.point = (pA + pB) * 0.5f;

                if (!collider1.gameObject.TryGetComponent(out pair.body1))
                    pair.body1 = collider1.gameObject.AddComponent<CustomRigidbody>();
                if (!collider2.gameObject.TryGetComponent(out pair.body2))
                    pair.body2 = collider2.gameObject.AddComponent<CustomRigidbody>();

                return pair;
            }

            ReBuildPolytop(ref epaSimplex, sp);
        }

        return new CollisionPair { };
    }

    #endregion
}
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
    public class Triangle
    {
        public Vector3 a, b, c;
        private Vector3 normal;
        private bool isNormalComputed = false;

        public Triangle(Vector3 _a, Vector3 _b, Vector3 _c)
        {
            Set(_a, _b, _c);
        }

        public void Set(Vector3 _a, Vector3 _b, Vector3 _c)
        {
            a = _a;
            b = _b;
            c = _c;
            isNormalComputed = false;
        }

        public Vector3 GetNormal()
        {
            if (isNormalComputed)
            {
                return normal;
            }

            normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude <= 1e-12f)
            {
                normal = Vector3.up;
            }

            normal.Normalize();

            // TODO : probably remove this, should never face the origin
            // Check if normal need to be flip to face away from the origin
            if (Vector3.Dot(normal, a) < 0f)
            {
                normal = -normal;
            }

            isNormalComputed = true;

            return normal;
        }
    }

    #endregion

    #region  Helper Methods
    public static int GetClosestFace(List<EPA.Triangle> faces)
    {
        int closest = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < faces.Count; i++)
        {
            EPA.Triangle face = faces[i];
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
    public static List<Triangle> BuildGJKPolytop(List<Vector3> simplex)
    {
        List<Triangle> faces = new List<Triangle>(4);
        if (simplex == null || simplex.Count != 4)
        {
            return faces;
        }

        // Specific winding order to ensure normals point outward from origin
        faces.Add(new Triangle(simplex[3], simplex[1], simplex[0]));
        faces.Add(new Triangle(simplex[1], simplex[2], simplex[0]));
        faces.Add(new Triangle(simplex[3], simplex[2], simplex[1]));
        faces.Add(new Triangle(simplex[2], simplex[3], simplex[0]));

        return faces;
    }

    // Rebuild the polytope with a new support point
    public static void ReBuildPolytop(ref List<Triangle> polytope, Vector3 newPoint)
    {
        // Get all faces visible from the new support point
        List<int> visibleFaces = new List<int>();
        for (int i = 0; i < polytope.Count; i++)
        {
            Triangle face = polytope[i];
            Vector3 normal = face.GetNormal();
            if (Vector3.Dot(normal, newPoint - face.a) > eps)
            {
                visibleFaces.Add(i);
            }
        }

        var counts = new Dictionary<(Vector3Int, Vector3Int), int>();
        var rawEdge = new Dictionary<(Vector3Int, Vector3Int), (Vector3 A, Vector3 B)>();

        foreach (int idx in visibleFaces)
        {
            Triangle f = polytope[idx];

            var key1 = EdgeKey(f.a, f.b);
            if (!counts.ContainsKey(key1))
            {
                counts[key1] = 1;
                rawEdge[key1] = (f.a, f.b);
            }
            else counts[key1]++;

            var key2 = EdgeKey(f.b, f.c);
            if (!counts.ContainsKey(key2))
            {
                counts[key2] = 1;
                rawEdge[key2] = (f.b, f.c);
            }
            else counts[key2]++;

            var key3 = EdgeKey(f.c, f.a);
            if (!counts.ContainsKey(key3))
            {
                counts[key3] = 1;
                rawEdge[key3] = (f.c, f.a);
            }
            else counts[key3]++;
        }

        // Remove visible faces
        foreach (int idx in visibleFaces.Distinct().OrderByDescending(i => i))
        {
            if (idx >= 0 && idx < polytope.Count) polytope.RemoveAt(idx);
        }

        // Add new faces
        foreach (var kv in counts)
        {
            if (kv.Value != 1) continue;
            var (A, B) = rawEdge[kv.Key];

            Triangle nf = new Triangle(B, A, newPoint);
            Vector3 n = Vector3.Cross(nf.b - nf.a, nf.c - nf.a).normalized;

            if (Vector3.Dot(n, newPoint) < 0f)
                nf.Set(A, B, newPoint);

            polytope.Add(nf);
        }
    }
    #endregion

    #region Main EPA Method

    public static CollisionPair ExpendingPolytopeAlgorithm(CustomCollider collider1, CustomCollider collider2,
        List<Vector3> gjkSimplex, int maxIterations)
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

            Vector3 supportPoint = PhysicsManager.GetSupport(collider1, collider2, normal);
            float supportDist = Vector3.Dot(normal, supportPoint);

            if ((supportDist - dist) <= tolerance)
            {
                EPA.CollisionPair pair = new CollisionPair();
                pair.normal = normal;
                pair.penetration = Mathf.Max(dist, 0f);

                Vector3 s1 = collider1.GetSupport(-normal);
                Vector3 s2 = collider2.GetSupport(normal);
                pair.point = (s1 + s2) * 0.5f;

                if (!collider1.gameObject.TryGetComponent(out pair.body1))
                    pair.body1 = collider1.gameObject.AddComponent<CustomRigidbody>();
                if (!collider2.gameObject.TryGetComponent(out pair.body2))
                    pair.body2 = collider2.gameObject.AddComponent<CustomRigidbody>();

                return pair;
            }
            ReBuildPolytop(ref epaSimplex, supportPoint);
        }
        return new CollisionPair { };
    }

    #endregion
}
using System.Collections.Generic;
using UnityEngine;

public class GJK : MonoBehaviour
{
    public static Vector3 GetSupport(CustomCollider collider1, CustomCollider collider2, Vector3 direction)
    {
        return collider1.GetSupport(direction) - collider2.GetSupport(-direction);
    }
    
    public static bool CheckGJKCollision(CustomCollider collider1, CustomCollider collider2, uint maxIterations, ref List<Vector3> outGJKPoints)
    {
        float colliderScaleMagnitude = Mathf.Max(
            collider1.transform.lossyScale.magnitude,
            collider2.transform.lossyScale.magnitude
        );

        float eps = 1e-6f * Mathf.Max(1f, colliderScaleMagnitude);

        Vector3 direction = collider2.transform.position - collider1.transform.position;
        if (direction == Vector3.zero)
            direction = Vector3.right;

        List<Vector3> simplex = new List<Vector3> { GetSupport(collider1, collider2, direction) };

        direction = -simplex[0];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            Vector3 newPoint = GetSupport(collider1, collider2, direction);

            if (Vector3.Dot(newPoint, direction) <= eps)
                return false;

            simplex.Add(newPoint);

            if (ContainsOrigin(simplex, ref direction, eps))
            {
                outGJKPoints = simplex;
                return true;
            }
        }

        return false;
    }


    private static bool ContainsOrigin(List<Vector3> simplex, ref Vector3 direction, float eps)
    {
        if (simplex.Count == 2)
        {
            Vector3 a = simplex[1];
            Vector3 b = simplex[0];

            Vector3 ab = b - a;
            Vector3 ao = -a;

            if (Vector3.Dot(ab, ao) > eps)
            {
                direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            }
            else
            {
                simplex.RemoveAt(0);
                direction = ao;
            }
        }
        else if (simplex.Count == 3)
        {
            Vector3 a = simplex[2];
            Vector3 b = simplex[1];
            Vector3 c = simplex[0];

            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ao = -a;

            Vector3 abc = Vector3.Cross(ab, ac);

            if (Vector3.Dot(Vector3.Cross(abc, ac), ao) > eps)
            {
                if (Vector3.Dot(ac, ao) > eps)
                {
                    simplex.RemoveAt(1);
                    direction = Vector3.Cross(Vector3.Cross(ac, ao), ac);
                }
                else
                {
                    simplex.RemoveAt(0);
                    return ContainsOrigin(simplex, ref direction, eps);
                }
            }
            else
            {
                if (Vector3.Dot(Vector3.Cross(ab, abc), ao) > eps)
                {
                    if (Vector3.Dot(ab, ao) > eps)
                    {
                        simplex.RemoveAt(0);
                        direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
                    }
                    else
                    {
                        simplex.Clear();
                        simplex.Add(a);
                        direction = ao;
                    }
                }
                else
                {
                    if (Vector3.Dot(abc, ao) > eps)
                    {
                        direction = abc;
                    }
                    else
                    {
                        (simplex[0], simplex[1]) = (simplex[1], simplex[0]);
                        direction = -abc;
                    }
                }
            }
        }
        else if (simplex.Count == 4)
        {
            Vector3 a = simplex[3];
            Vector3 b = simplex[2];
            Vector3 c = simplex[1];
            Vector3 d = simplex[0];

            Vector3 ao = -a;

            Vector3 abc = Vector3.Cross(b - a, c - a);
            Vector3 acd = Vector3.Cross(c - a, d - a);
            Vector3 adb = Vector3.Cross(d - a, b - a);

            if (Vector3.Dot(abc, ao) > eps)
            {
                simplex.RemoveAt(0);
                direction = abc;
                return false;
            }

            if (Vector3.Dot(acd, ao) > eps)
            {
                simplex.RemoveAt(2);
                direction = acd;
                return false;
            }

            if (Vector3.Dot(adb, ao) > eps)
            {
                simplex.RemoveAt(1);
                direction = adb;
                return false;
            }

            return true;
        }

        return false;
    }
}

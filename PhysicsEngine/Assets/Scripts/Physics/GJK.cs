using System.Collections.Generic;
using UnityEngine;

public class GJK : MonoBehaviour
{
    public static EPA.SupportPoint GetSupport(CustomCollider collider1, CustomCollider collider2, Vector3 direction)
    {
        return EPA.MakeSupport(collider1, collider2, direction);
    }
    
    public static bool CheckGJKCollision(CustomCollider collider1, CustomCollider collider2, uint maxIterations, ref List<EPA.SupportPoint> outGJKPoints)
    {
        float colliderScaleMagnitude = Mathf.Max(
            collider1.transform.lossyScale.magnitude,
            collider2.transform.lossyScale.magnitude
        );

        float eps = 1e-6f * Mathf.Max(1f, colliderScaleMagnitude);

        Vector3 direction = collider2.transform.position - collider1.transform.position;
        if (direction == Vector3.zero)
            direction = Vector3.right;

        List<EPA.SupportPoint> simplex = new List<EPA.SupportPoint> { GetSupport(collider1, collider2, direction) };

        direction = -simplex[0].minkowski;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            EPA.SupportPoint newPoint = GetSupport(collider1, collider2, direction);

            if (Vector3.Dot(newPoint.minkowski, direction) <= eps)
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


    private static bool ContainsOrigin(List<EPA.SupportPoint> simplex, ref Vector3 direction, float eps)
    {
        if (simplex.Count == 2)
        {
            EPA.SupportPoint a = simplex[1];
            EPA.SupportPoint b = simplex[0];

            Vector3 ab = b.minkowski - a.minkowski;
            Vector3 ao = -a.minkowski;

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
            EPA.SupportPoint a = simplex[2];
            EPA.SupportPoint b = simplex[1];
            EPA.SupportPoint c = simplex[0];

            Vector3 ab = b.minkowski - a.minkowski;
            Vector3 ac = c.minkowski - a.minkowski;
            Vector3 ao = -a.minkowski;

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
            EPA.SupportPoint a = simplex[3];
            EPA.SupportPoint b = simplex[2];
            EPA.SupportPoint c = simplex[1];
            EPA.SupportPoint d = simplex[0];

            Vector3 ao = -a.minkowski;

            Vector3 abc = Vector3.Cross(b.minkowski - a.minkowski, c.minkowski - a.minkowski);
            Vector3 acd = Vector3.Cross(c.minkowski - a.minkowski, d.minkowski - a.minkowski);
            Vector3 adb = Vector3.Cross(d.minkowski - a.minkowski, b.minkowski - a.minkowski);

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

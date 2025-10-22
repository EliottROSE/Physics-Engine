using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public class CustomCollider : MonoBehaviour
{  
    protected List<Vector3> localPoints = new List<Vector3>();
    protected List<Vector3> points = new List<Vector3>();
    
    protected AABB aabb;

    #region MonoBehaviour
    protected virtual void Awake()
    {
        aabb = new AABB();
        UpdatePoints();
    }
    
    protected virtual void FixedUpdate()
    {
        UpdatePoints();
    }
    #endregion
    
    #region Getter
    public AABB GetAABB() { return aabb; }
    #endregion

    public virtual void InitAABB()
    {
        aabb = new AABB();
        aabb.SetAABB(transform.position, transform.lossyScale);
    }

    public virtual Vector3 GetSupport(Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-12f)
            direction = Vector3.right;
        else
            direction.Normalize();
        Vector3 support = Vector3.zero;
        float maxProjection = Mathf.NegativeInfinity;

        foreach (Vector3 point in points)
        {
            float projection = Vector3.Dot(point, direction);

            if (projection > maxProjection)
            {
                maxProjection = projection;
                support = point;
            }
        }
        
        return support;
    }

    protected virtual void UpdatePoints()
    {
        if (localPoints.Count <= 0)
            return;
        
        points.Clear();
        
        for (int i = 0; i < localPoints.Count; i++)
        {
            points.Add(transform.TransformPoint(localPoints[i]));
        }
    }
    
    public virtual float GetDragCoefficient() { return 1.0f; } // Placeholder value
    public virtual float GetCrossSectionalArea(Vector3 direction) { return 1.0f; } // Placeholder value
}

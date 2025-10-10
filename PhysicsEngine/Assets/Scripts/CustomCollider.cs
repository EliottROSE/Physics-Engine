using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public class CustomCollider : MonoBehaviour
{   
    protected Vector3 position = Vector3.zero;
    protected Quaternion rotation = Quaternion.identity;
    protected Vector3 scale = Vector3.one;
    
    protected List<Vector3> points = new List<Vector3>();
    
    protected AABB aabb;

    #region MonoBehaviour
    protected virtual void Start()
    {
        position = transform.position;
        scale = transform.localScale;
        rotation = transform.rotation;
        
        aabb = new AABB();
        aabb.SetAABB(transform.position, transform.localScale);
    }

    protected virtual void Update()
    {
        position = transform.position;
        scale = transform.localScale;
        rotation = transform.rotation;
    }

    //public void OnEnable()
    //{
    //    if (PhysicsManager.Instance != null && Application.isPlaying)
    //    {
    //        PhysicsManager.Instance.InsertAABB(aabb);
    //    }
    //}
    //
    //public void OnDisable()
    //{
    //    if (PhysicsManager.Instance != null && Application.isPlaying)
    //    {
    //        PhysicsManager.Instance.RemoveAABB(aabb);
    //    }
    //}
    #endregion
    
    #region Getter
    public Vector3 GetPosition() { return position; }
    public Quaternion GetRotation() { return rotation; }
    public Vector3 GetScale() { return scale; }
    public AABB GetAABB() { return aabb; return gameObject.GetComponentInParent<AABB>(); }
    #endregion

    public virtual void InitAABB()
    {
        aabb = new AABB();
        aabb.SetAABB(transform.position, transform.localScale);
    }

    public virtual Vector3 GetSupport(Vector3 direction)
    {
        Vector3 support =  Vector3.zero;
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
}

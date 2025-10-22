using System.Collections.Generic;
using UnityEngine;

public class CustomBoxCollider : CustomCollider
{
    [SerializeField] private float dragCoefficient = 1.05f; // Typical value for a cube
    protected override void Awake()
    {
        localPoints = new List<Vector3>(8)
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f)
        };
        
        base.Awake();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        aabb.SetAABB(points);
    }

    public override void InitAABB()
    {
        aabb.SetAABB(points);
    }

    public override Vector3 GetSupport(Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-12f)
            direction = Vector3.right;
        else
            direction.Normalize();
        Vector3 localDir = Quaternion.Inverse(transform.rotation) * direction;
        
        Vector3 halfExtents = transform.lossyScale * 0.5f;
        Vector3 localSupport = new Vector3(
            localDir.x >= 0 ? halfExtents.x : -halfExtents.x,
            localDir.y >= 0 ? halfExtents.y : -halfExtents.y,
            localDir.z >= 0 ? halfExtents.z : -halfExtents.z
        );
        
        return transform.position + (transform.rotation * localSupport);
    }
    
    public override float GetDragCoefficient() { return dragCoefficient; }
    
    public override float GetCrossSectionalArea(Vector3 direction)
    {
        Vector3 localDir = Quaternion.Inverse(transform.rotation) * direction.normalized;
        float absX = Mathf.Abs(localDir.x);
        float absY = Mathf.Abs(localDir.y);
        float absZ = Mathf.Abs(localDir.z);

        Vector3 halfExtents = transform.lossyScale * 0.5f;

        float areaXY = (halfExtents.x * 2) * (halfExtents.y * 2) * absZ;
        float areaYZ = (halfExtents.y * 2) * (halfExtents.z * 2) * absX;
        float areaZX = (halfExtents.z * 2) * (halfExtents.x * 2) * absY;

        return areaXY + areaYZ + areaZX;
    }
}

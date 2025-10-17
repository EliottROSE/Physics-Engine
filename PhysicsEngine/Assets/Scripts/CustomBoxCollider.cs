using UnityEngine;

public class CustomBoxCollider : CustomCollider
{
    [SerializeField] private float dragCoefficient = 1.05f; // Typical value for a cube
    
    private static readonly Vector3[] localCorners = new Vector3[]
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

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        UpdatePoints();
        aabb.SetAABB(points);
    }

    public override void InitAABB()
    {
        //aabb = new AABB();
        UpdatePoints();
        aabb.SetAABB(points);
    }
    private void UpdatePoints()
    {
        points.Clear();
        
        for (int i = 0; i < 8; i++)
        {
            points.Add(transform.TransformPoint(localCorners[i]));
        }
    }

    public override Vector3 GetSupport(Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-12f)
            direction = Vector3.right;
        else
            direction.Normalize();
        Vector3 localDir = Quaternion.Inverse(rotation) * direction;
        
        Vector3 halfExtents = scale * 0.5f;
        Vector3 localSupport = new Vector3(
            localDir.x >= 0 ? halfExtents.x : -halfExtents.x,
            localDir.y >= 0 ? halfExtents.y : -halfExtents.y,
            localDir.z >= 0 ? halfExtents.z : -halfExtents.z
        );
        
        return position + (rotation * localSupport);
    }
    
    public override float GetDragCoefficient() { return dragCoefficient; }
    
    public override float GetCrossSectionalArea(Vector3 direction)
    {
        Vector3 localDir = Quaternion.Inverse(rotation) * direction.normalized;
        float absX = Mathf.Abs(localDir.x);
        float absY = Mathf.Abs(localDir.y);
        float absZ = Mathf.Abs(localDir.z);

        Vector3 halfExtents = scale * 0.5f;

        float areaXY = (halfExtents.x * 2) * (halfExtents.y * 2) * absZ;
        float areaYZ = (halfExtents.y * 2) * (halfExtents.z * 2) * absX;
        float areaZX = (halfExtents.z * 2) * (halfExtents.x * 2) * absY;

        return areaXY + areaYZ + areaZX;
    }
}

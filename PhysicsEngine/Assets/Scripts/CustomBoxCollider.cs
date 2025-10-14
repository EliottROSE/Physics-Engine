using UnityEngine;

public class CustomBoxCollider : CustomCollider
{
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
        Vector3 localDir = Quaternion.Inverse(rotation) * direction;
        
        Vector3 halfExtents = scale * 0.5f;
        Vector3 localSupport = new Vector3(
            localDir.x >= 0 ? halfExtents.x : -halfExtents.x,
            localDir.y >= 0 ? halfExtents.y : -halfExtents.y,
            localDir.z >= 0 ? halfExtents.z : -halfExtents.z
        );
        
        return position + (rotation * localSupport);
    }
}

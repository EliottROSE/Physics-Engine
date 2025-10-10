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

    public override void InitAABB()
    {
        aabb = new AABB();
        UpdatePoints();
        aabb.SetAABB(points);
    }

    protected override void Update()
    {
        base.Update();
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
}

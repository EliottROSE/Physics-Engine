using UnityEngine;

public class CustomSphereCollider : CustomCollider
{
    [SerializeField] private float radius;

    protected override void Update()
    {
        base.Update();
        aabb.SetAABB(position, Vector3.one * radius);
    }

    public override void InitAABB()
    {
        aabb.SetAABB(position, Vector3.one * radius);
    }

    public override Vector3 GetSupport(Vector3 direction)
    {
        return position + radius * direction;
    }
}

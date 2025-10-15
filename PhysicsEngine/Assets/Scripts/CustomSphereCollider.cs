using UnityEngine;

public class CustomSphereCollider : CustomCollider
{
    [SerializeField] private float radius = 1.0f;
    [SerializeField] private float dragCoefficient = 0.47f; // Typical value for a sphere

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
    
    public override float GetDragCoefficient() { return dragCoefficient; }
    
    public override float GetCrossSectionalArea(Vector3 direction)
    {
        return Mathf.PI * radius * radius;
    }
}

using System.Linq;
using UnityEngine;

public class CustomSphereCollider : CustomCollider
{
    private float radius = 1.0f;
    [SerializeField] private float dragCoefficient = 0.47f; // Typical value for a sphere
    
    public float Radius => radius;

    protected void Start()
    {
        localPoints = PhysicsManager.Instance.LocalSpherePoints;
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        Vector3 s = transform.lossyScale;
        radius = 0.5f * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        aabb.SetAABB(transform.position, Vector3.one * (radius * 2f));
    }

    public override void InitAABB()
    {
        Vector3 s = transform.lossyScale;
        radius = 0.5f * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        aabb.SetAABB(transform.position, Vector3.one * (radius * 2f));
    }
    
    public override float GetDragCoefficient() { return dragCoefficient; }
    
    public override float GetCrossSectionalArea(Vector3 direction)
    {
        return Mathf.PI * radius * radius;
    }
}

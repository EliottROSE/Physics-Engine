using System.Linq;
using UnityEngine;

public class CustomSphereCollider : CustomCollider
{
    private float radius = 1.0f;
    [SerializeField] private float dragCoefficient = 0.47f; // Typical value for a sphere
    
    public float Radius => radius;

    protected void Start()
    {
        GameObject sphereGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh mesh = sphereGO.GetComponent<MeshFilter>().sharedMesh;
        localPoints = mesh.vertices.ToList();
    }

    protected override void Update()
    {
        base.Update();
        radius = transform.localScale.x * 0.5f;
        
        aabb.SetAABB(transform.position, Vector3.one * (radius * 2f));
    }

    public override void InitAABB()
    {
        aabb.SetAABB(transform.position, Vector3.one * (radius * 2f));
    }
    
    public override float GetDragCoefficient() { return dragCoefficient; }
    
    public override float GetCrossSectionalArea(Vector3 direction)
    {
        return Mathf.PI * radius * radius;
    }
}

using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    public CustomCollider coll1, coll2;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (AABB.CheckAABBCollision(coll1.GetAABB(), coll2.GetAABB()))
            Debug.Log("Kayou");
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    #region Singleton
    static PhysicsManager instance = null;
            
    public static PhysicsManager Instance 
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<PhysicsManager>();
            return instance;
        }
    }
    #endregion
    
    public class Node
    {
        private int parentIndex;
        
        private int leftIndex;
        private int rightIndex;
        
        private int colliderIndex;
        private bool isLeaf = false;
    }
    
    List<AABB> bounds = new List<AABB>();
    List<CustomCollider> colliders = new List<CustomCollider>();
    
    List<Node> boundsTree = new List<Node>();
    
    void Start()
    {
        bounds = FindObjectsOfType<AABB>().ToList();
        colliders = FindObjectsOfType<CustomCollider>().ToList();
        
        BuildAABBTree();
    }

    // Update is called once per frame
    void Update()
    {
        foreach (AABB bound in bounds)
        {
            bound.DrawAABB();
        }
    }

    public void BuildAABBTree()
    {
        if (colliders.Count == 0)
        {
            Debug.Log("No colliders found in the scene");
            return;
        }
        
        
    }
}

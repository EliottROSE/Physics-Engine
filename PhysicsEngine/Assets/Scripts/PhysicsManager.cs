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
        private int parentIndex; // -1 if root
        
        private int leftIndex; // -1 if empty || if collider
        private int rightIndex; // -1 if empty || if collider
        
        private bool isLeaf = false; // false if AABB | true if Collider
        private int AABBIndex; // AABB index

        public Node(int parent, int left, int right, bool isCollider, int AABB)
        {
            parent = parentIndex;
            
            leftIndex = left;
            rightIndex = right;
            
            isLeaf = isCollider;
            AABBIndex = AABB;
        }
    }
    
    List<AABB> bounds = new List<AABB>();
    List<CustomCollider> colliders = new List<CustomCollider>();
    
    List<Node> boundsTree = new List<Node>();
    
    // {index, aabb, collider} : {0, aabb1, null}
    
    void Start()
    {
        bounds = FindObjectsOfType<AABB>().ToList();

        foreach (AABB bound in bounds)
        {
            CustomCollider collider;
            if (bound.gameObject.TryGetComponent<CustomCollider>(out collider))
            {
                colliders.Add(collider);
            }
        }
        
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

        // Special case if there is only one collider
        if (colliders.Count == 1)
        {
            Node root = new Node(-1, -1, -1, true, 0); // 0 beceause there is only one collider so only one aabb in list
            return;
        }
        
        
    }
}

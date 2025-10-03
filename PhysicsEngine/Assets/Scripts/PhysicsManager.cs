using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering;
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
        public int parentIndex; // -1 if root
        
        public int leftIndex; // -1 if empty || if collider
        public int rightIndex; // -1 if empty || if collider
        
        public bool isLeaf = false; // false if AABB | true if Collider
        public int AABBIndex; // AABB index

        public Node(int parent, int left, int right, bool isCollider, int AABB)
        {
            parentIndex = parent;
            
            leftIndex = left;
            rightIndex = right;
            
            isLeaf = isCollider;
            AABBIndex = AABB;
        }
        
        
    }
    
    // Existing collider in the scene
    private List<CustomCollider> colliders = new List<CustomCollider>();
    // Existing bounds
    private List<AABB> collidersBounds = new List<AABB>();
    
    // All bounds inside tree, include both existing collider and abstract detection zone
    private List<AABB> bounds = new List<AABB>();
    // AABB tree
    private List<Node> boundsTree = new List<Node>();
    // Index of the root 
    private int root;
    
    void Start()
    {
        colliders = FindObjectsOfType<CustomCollider>().ToList();

        foreach (CustomCollider collider in colliders)
        {
            collider.InitAABB();
            AABB bound = collider.GetAABB();
            
            collidersBounds.Add(bound);
            bounds.Add(bound);
        }
        BuildAABBTree();
    }
    
    

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (AABB bound in bounds)
        {
            Gizmos.DrawWireCube(bound.GetPosition(), bound.GetScale());
        }
    }

    // Update is called once per frame
    void Update()
    {
        foreach (AABB bound in bounds)
        {
            //bound.DrawAABB();
        }
    }

    public void BuildAABBTree()
    {
        if (collidersBounds.Count == 0)
        {
            Debug.Log("No colliders found in the scene");
            return;
        }

        // Special case if there is only one collider, it become the root
        if (collidersBounds.Count == 1)
        {
            Node first = new Node(-1, -1, -1, true, 0); // 0 beceause there is only one collider so only one aabb in list
            boundsTree.Add(first);
            root = 0;
            return;
        }

        foreach (AABB bound in collidersBounds)
        {
            InsertAABB(bound);
        }
    }
    
    public void InsertAABB(AABB bound)
    {
        // If the tree is empty the first bound become the root
        if (boundsTree.Count == 0)
        {
            Node node = new Node(-1, -1, -1, true, 0);
            boundsTree.Add(node);            
        }

        // If it's an existing collider or a abstract detection zone
        bool isLeaf = false;
        Node currentNode = null;

        while (!currentNode.isLeaf)
        {
            float leftValue = AABB.GetUnionCost(bounds[boundsTree[currentNode.leftIndex].AABBIndex], bound);
            float rightValue = AABB.GetUnionCost(bounds[boundsTree[currentNode.rightIndex].AABBIndex], bound);
            
            
        }
        //int newIndex = 0;
        //bool isRight = true;
        //
        //while (!isLeaf)
        //{
        //    if (currentNode.leftIndex == -1 && currentNode.rightIndex == -1)
        //    {
        //        isLeaf = true;
        //        continue;
        //    }
        //    float leftValue = AABB.GetUnionCost(bounds[boundsTree[currentNode.leftIndex].AABBIndex], bound);
        //    float rightValue = AABB.GetUnionCost(bounds[boundsTree[currentNode.rightIndex].AABBIndex], bound);
        //
        //    if (leftValue < rightValue)
        //    {
        //        newIndex = currentNode.leftIndex;
        //        currentNode = boundsTree[currentNode.leftIndex];
        //        isRight = false;
        //    }
        //    else
        //    {
        //        newIndex = currentNode.rightIndex;
        //        currentNode = boundsTree[currentNode.rightIndex];
        //    }
        //}
        //
        //AABB newParentAABB = new AABB();
        //newParentAABB.SetAABB(bounds[currentNode.AABBIndex], bound);
        //bounds.Add(newParentAABB);
        //
        //Node newParentNode = null;
        //
        //currentNode.parentIndex = boundsTree.Count;
        //
        //bounds.Add(bound);
        //Node newBoundsNode = new Node(boundsTree.Count, -1, -1, true, bounds.Count - 1);
        //boundsTree.Add(newBoundsNode);
        //
        //if (isRight)
        //{
        //    // issue with left (change -1)
        //    newParentNode = new Node(currentNode.parentIndex, boundsTree.Count, newIndex,false, bounds.Count - 1);
        //}
        //else
        //{
        //    // issue with right (change -1)
        //    newParentNode = new Node(currentNode.parentIndex, newIndex, boundsTree.Count,false, bounds.Count - 1);
        //}
        //boundsTree.Add(newParentNode);
    }
}

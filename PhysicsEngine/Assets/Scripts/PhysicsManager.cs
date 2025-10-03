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

        Debug.Log(boundsTree.Count);
        Debug.Log(bounds.Count);
    }

    public void InsertAABB(AABB bound)
    {
        // If the tree is empty the first bound become the root
        if (boundsTree.Count == 0)
        {
            Node node = new Node(-1, -1, -1, true, 0);
            boundsTree.Add(node);
            return;
        }


        Node currentNode = boundsTree[root];
        bool isLeft = true;
        while (!currentNode.isLeaf)
        {
            float leftValue = AABB.GetUnionCost(bounds[boundsTree[currentNode.leftIndex].AABBIndex], bound);
            float rightValue = AABB.GetUnionCost(bounds[boundsTree[currentNode.rightIndex].AABBIndex], bound);

            if (leftValue < rightValue)
            {
                currentNode = boundsTree[currentNode.leftIndex];
            }
            else
            {
                currentNode = boundsTree[currentNode.rightIndex];
                isLeft = false;
            }
        }

        // New parent abstract detection zone
        AABB newParentAABB = new AABB();
        newParentAABB.SetAABB(bounds[currentNode.AABBIndex], bound);
        bounds.Add(newParentAABB);

        int newLeftIndex = 0;
        
        // Special case if process root
        if (currentNode.parentIndex == -1)
        {
            newLeftIndex = 0;
        }
        else
        {
            if (isLeft)
                newLeftIndex = boundsTree[currentNode.parentIndex].leftIndex;
            else
                newLeftIndex = boundsTree[currentNode.parentIndex].rightIndex;
        }


        // left is currentNode
        Node newParentNode = new Node(currentNode.parentIndex, newLeftIndex, -1, false, bounds.Count - 1);
        boundsTree.Add(newParentNode);
        if (currentNode.parentIndex == -1)
        {
            root = boundsTree.Count - 1;
        }
        else
        {
            
        }
                
        // Change currentNode informations
        currentNode.parentIndex = boundsTree.Count - 1;

        // new bound node
        bounds.Add(bound);
        Node newBoundNode = new Node(boundsTree.Count - 1, -1, -1, true, bounds.Count - 1);
        boundsTree.Add(newBoundNode);
        newParentNode.rightIndex = boundsTree.Count - 1;
    }
}
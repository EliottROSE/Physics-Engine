using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.FullSerializer.Internal;
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

    #region Node
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
    #endregion

    struct CollisionPair
    {
        Rigidbody body1;
        Rigidbody body2;
        
        Vector3 point; // Point of collision
        Vector3 normal; // normal of collision point
        float penetration; // how far the rigidbodies enter in collision
    }
    // Existing collider in the scene
    private List<CustomCollider> colliders = new List<CustomCollider>();

    // Existing bounds
    private List<AABB> collidersBounds = new List<AABB>();

    // All bounds inside tree, include both existing collider and abstract detection zone
    private List<AABB> bounds = new List<AABB>();

    // AABB tree
    private List<Node> boundsTree = new List<Node>();
    
    List<int> availableBoundsTreeIndexes = new List<int>();
    List<int> availableBoundsIndexes = new List<int>();

    // Index of the root 
    private int root;

    public static Vector3 GetSupport(CustomCollider collider1, CustomCollider collider2, Vector3 direction)
    {
        return collider1.GetSupport(direction) - collider2.GetSupport(-direction);
    }

    public static bool CheckGJKCollision(CustomCollider collider1, CustomCollider collider2, uint maxIterations)
    {
        if (maxIterations == 0)
            return false;
        
        Vector3 point1 = GetSupport(collider1, collider2, Vector3.right);
        Vector3 point2 = GetSupport(collider1, collider2, Vector3.zero - point1);
        
        Vector3 lineNormal = Vector3.Cross(point1, point2);
        if (Vector3.Dot(lineNormal, -point1) < 0f)
            lineNormal = -lineNormal;
        
        lineNormal.Normalize();
        
        Vector3 point3 = GetSupport(collider1, collider2, lineNormal);
        
        Vector3 faceNormal = Vector3.Cross(point2 - point1, point3 - point1);
        if (Vector3.Dot(faceNormal, -point1) < 0f)
            faceNormal = -faceNormal;
        faceNormal.Normalize();
        
        Vector3 point4 = GetSupport(collider1, collider2, faceNormal);
        
        
        
        return false;
    }

    void Start()
    {
        colliders = FindObjectsOfType<CustomCollider>().ToList();

        foreach (CustomCollider collider in colliders)
        {
            collider.InitAABB();
            AABB bound = collider.GetAABB();

            collidersBounds.Add(bound);
        }

        BuildAABBTree();
    }


    private void OnDrawGizmos()
    {
        foreach (Node node in boundsTree)
        {
            if (node != null && node.isLeaf)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bounds[node.AABBIndex].GetPosition(), bounds[node.AABBIndex].GetScale());
            }
            else if (node != null && !node.isLeaf)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(bounds[node.AABBIndex].GetPosition(), bounds[node.AABBIndex].GetScale());
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
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
            int nodeIndex = AddAndReturnNodeIndex(node);
            AddBound(bound);
            root = nodeIndex;
            return;
        }

        Node currentNode = boundsTree[root];
        bool isLeft = true;
        while (!currentNode.isLeaf)
        {
            float leftValue = AABB.GetUnionCost(bounds[GetBoundIndexFromTree(currentNode.leftIndex)], bound);
            float rightValue = AABB.GetUnionCost(bounds[GetBoundIndexFromTree(currentNode.rightIndex)], bound);

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
        int newParentAABBIndex = AddAndReturnBoundIndex(newParentAABB);

        int newLeftIndex = 0;

        // Special case if process root
        if (currentNode.parentIndex == -1)
        {
            newLeftIndex = root;
        }
        else
        {
            if (isLeft)
                newLeftIndex = boundsTree[currentNode.parentIndex].leftIndex;
            else
                newLeftIndex = boundsTree[currentNode.parentIndex].rightIndex;
        }

        // left is currentNode
        Node newParentNode = new Node(currentNode.parentIndex, newLeftIndex, -1, false, newParentAABBIndex);
        int newParentNodeIndex = AddAndReturnNodeIndex(newParentNode);

        if (currentNode.parentIndex == -1)
        {
            root = newParentNodeIndex;
        }
        else
        {
            if (isLeft)
                boundsTree[currentNode.parentIndex].leftIndex = newParentNodeIndex;
            else
                boundsTree[currentNode.parentIndex].rightIndex = newParentNodeIndex;
        }

        // Change currentNode informations
        currentNode.parentIndex = newParentNodeIndex;

        // new bound node
        int newBoundIndex = AddAndReturnBoundIndex(bound);
        Node newBoundNode = new Node(newParentNodeIndex, -1, -1, true, newBoundIndex);
        int newBoundNodeIndex = AddAndReturnNodeIndex(newBoundNode);
        boundsTree[newParentNodeIndex].rightIndex = newBoundNodeIndex;

        UpdateFromChildren(newParentNodeIndex);
    }

    public void RemoveAABB(AABB bound)
    {
        int nodeIndex = -1;
        bool isLeft = true;

        // Find the leaf node corresponding to the AABB
        for (int i = 0; i < boundsTree.Count; i++)
        {
            Node node = boundsTree[i];
            if (node != null && node.isLeaf && bounds[node.AABBIndex] == bound)
            {
                nodeIndex = i;
                if (node.parentIndex != -1 && boundsTree[node.parentIndex].rightIndex == nodeIndex)
                    isLeft = false;
                break;
            }
        }

        if (nodeIndex == -1)
            return; // Not found

        Node leafNode = boundsTree[nodeIndex];
        int oldParentIndex = leafNode.parentIndex;

        if (oldParentIndex == -1)
        {
            // Only one node in the tree, remove root
            bounds[leafNode.AABBIndex] = null;
            availableBoundsIndexes.Add(leafNode.AABBIndex);
            boundsTree[nodeIndex] = null;
            availableBoundsTreeIndexes.Add(nodeIndex);
            root = -1;
            return;
        }

        Node oldParent = boundsTree[oldParentIndex];
        int siblingIndex = (oldParent.leftIndex == nodeIndex) ? oldParent.rightIndex : oldParent.leftIndex;
        Node sibling = boundsTree[siblingIndex];

        // Update sibling parent
        sibling.parentIndex = oldParent.parentIndex;

        if (oldParent.parentIndex == -1)
        {
            // Old parent was root
            root = siblingIndex;
        }
        else
        {
            Node grandParent = boundsTree[oldParent.parentIndex];
            if (grandParent.leftIndex == oldParentIndex)
                grandParent.leftIndex = siblingIndex;
            else
                grandParent.rightIndex = siblingIndex;
        }

        // Remove old parent + AABB
        bounds[oldParent.AABBIndex] = null;
        availableBoundsIndexes.Add(oldParent.AABBIndex);
        boundsTree[oldParentIndex] = null;
        availableBoundsTreeIndexes.Add(oldParentIndex);

        // Remove leaf node + AABB
        bounds[leafNode.AABBIndex] = null;
        availableBoundsIndexes.Add(leafNode.AABBIndex);
        boundsTree[nodeIndex] = null;
        availableBoundsTreeIndexes.Add(nodeIndex);

        if (boundsTree[siblingIndex].parentIndex != -1)
            UpdateFromChildren(boundsTree[siblingIndex].parentIndex);
    }
    
    private void UpdateFromChildren(int nodeIndex)
    {
        AABB leftBound = bounds[GetBoundIndexFromTree(boundsTree[nodeIndex].leftIndex)];
        AABB rightBound = bounds[GetBoundIndexFromTree(boundsTree[nodeIndex].rightIndex)];;
        bounds[boundsTree[nodeIndex].AABBIndex].SetAABB(leftBound, rightBound);
        int parentIndex = boundsTree[nodeIndex].parentIndex;
        if (parentIndex != -1)
            UpdateFromChildren(parentIndex);
    }

    public int GetBoundIndexFromTree(int treeIndex)
    {
        if (treeIndex < 0 || treeIndex >= boundsTree.Count)
        {
            Debug.LogError("Invalid tree index");    
            return -1;
        }
        
        return boundsTree[treeIndex].AABBIndex;
    }

    public void AddNode(Node node)
    {
        if (availableBoundsTreeIndexes.Count == 0)
        {
            boundsTree.Add(node);
            return;
        }
        boundsTree[availableBoundsTreeIndexes[0]] = node;
        availableBoundsTreeIndexes.RemoveAt(0);
    }

    public void AddBound(AABB bound)
    {
        if (availableBoundsIndexes.Count == 0)
        {
            bounds.Add(bound);
            return;
        }
        bounds[availableBoundsIndexes[0]] = bound;
        availableBoundsIndexes.RemoveAt(0);
    }
    public int AddAndReturnNodeIndex(Node node)
    {
        if (availableBoundsTreeIndexes.Count == 0)
        {
            boundsTree.Add(node);
            return boundsTree.Count - 1;
        }

        int index = availableBoundsTreeIndexes[0];
        availableBoundsTreeIndexes.RemoveAt(0);
        boundsTree[index] = node;
        return index;
    }

    public int AddAndReturnBoundIndex(AABB bound)
    {
        if (availableBoundsIndexes.Count == 0)
        {
            bounds.Add(bound);
            return bounds.Count - 1;
        }

        int index = availableBoundsIndexes[0];
        availableBoundsIndexes.RemoveAt(0);
        bounds[index] = bound;
        return index;
    }
    
}
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer.Internal;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PhysicsManager : MonoBehaviour
{
    #region Singleton

    static PhysicsManager instance = null;

    public static PhysicsManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<PhysicsManager>();
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

        public int ColliderIndex;

        public Node(int parent, int left, int right, bool isCollider, int AABB, int Index)
        {
            parentIndex = parent;

            leftIndex = left;
            rightIndex = right;

            isLeaf = isCollider;
            AABBIndex = AABB;

            ColliderIndex = Index;
        }
    }
    #endregion

    struct ColliderPair
    {
        public int collidersIndex;
        public int boundIndex;
    }

    public struct CollisionPair
    {
        public Rigidbody body1;
        public Rigidbody body2;
        
        public Vector3 point; // Point of collision
        public Vector3 normal; // normal of collision point
        public float penetration; // how far the rigidbodies enter in collision
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
    
    public class Triangle
    {
        public Vector3 a, b, c;
        
        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public Vector3 GetNormal()
        {
            return Vector3.Cross(b - a, c - a).normalized;
        }
    }
    
    private static Triangle MakeFace(Vector3 a, Vector3 b, Vector3 c, Vector3 opposite)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, opposite - a) < 0f)
            return new Triangle(a, c, b);
        return new Triangle(a, b, c);
    }
    
    private static int GetClosestFaceIndex(List<Triangle> faces, List<int> outsideFaces)
    {
        int closest = -1;
        float minDistance = float.MaxValue;

        foreach (int i in outsideFaces)
        {
            Triangle face = faces[i];
            Vector3 n = face.GetNormal();

            float distance = Vector3.Dot(n, -face.a);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    public static Vector3 GetSupport(CustomCollider collider1, CustomCollider collider2, Vector3 direction)
    {
        return collider1.GetSupport(direction) - collider2.GetSupport(-direction);
    }

    public static bool CheckGJKCollision(CustomCollider collider1, CustomCollider collider2, uint maxIterations)
    {
        float colliderScaleMagnitude = Mathf.Max(
            collider1.transform.lossyScale.magnitude,
            collider2.transform.lossyScale.magnitude
        );
        
        float eps = 1e-6f * Mathf.Max(1f, colliderScaleMagnitude);
        
        Vector3 direction = collider2.transform.position - collider1.transform.position;
        direction = (direction.sqrMagnitude > eps) ? direction.normalized : Vector3.right;
        
        Vector3 point1 = GetSupport(collider1, collider2, direction);
        if (Vector3.Dot(point1, direction) < 0)
            return false;

        direction = -point1;
        direction = (direction.sqrMagnitude > eps) ? direction : Vector3.right;
        
        Vector3 point2 = GetSupport(collider1, collider2, direction);
        
        //Vector3 lineNormal = Vector3.Cross(point1, point2);
        Vector3 lineDir = point2 - point1;
        Vector3 lineNormal = Vector3.Cross(lineDir, -point1);
        if (Vector3.Dot(lineNormal, -point1) < 0f) 
            lineNormal = -lineNormal;
        lineNormal = (lineNormal.sqrMagnitude > eps) ? lineNormal.normalized : Vector3.right;
        
        Vector3 point3 = GetSupport(collider1, collider2, lineNormal);
        
        Vector3 faceNormal = Vector3.Cross(point2 - point1, point3 - point1);
        if (Vector3.Dot(faceNormal, -point1) < 0f) 
            faceNormal = -faceNormal;
        faceNormal = (faceNormal.sqrMagnitude > eps) ? faceNormal.normalized : Vector3.right;
        faceNormal.Normalize();
        
        Vector3 point4 = GetSupport(collider1, collider2, faceNormal);
        
        List<Triangle> faces = new List<Triangle>();
        faces.Add(MakeFace(point1, point2, point3, point4));
        faces.Add(MakeFace(point2, point3, point4, point1));
        faces.Add(MakeFace(point3, point4, point1, point2));
        faces.Add(MakeFace(point4, point1, point2, point3));
        
        List<int> outsideFaces = new List<int>();

        for (int i = 0; i < maxIterations; ++i)
        {
            outsideFaces.Clear();
            for (int j = 0; j < faces.Count; ++j)
            {
                Triangle face = faces[j];
                Vector3 n = face.GetNormal();
                float dot = Vector3.Dot(n, face.a);
                if (dot > 0f)
                    outsideFaces.Add(j);
            }

            if (outsideFaces.Count == 0)
                return true;

            int closest = GetClosestFaceIndex(faces, outsideFaces);
            
            Triangle chosenFace = faces[closest];
            Vector3 chosenNormal = chosenFace.GetNormal(); // Already got normal for EPA
            
            // Penetration : 
            //float penetration = -Vector3.Dot(chosenNormal, chosenFace.a);
            //Mathf.Abs(penetration);

            // contact point : 
            //Vector3 contactPoint;

            Vector3 newPoint = GetSupport(collider1, collider2, chosenNormal);
            if (Vector3.Dot(newPoint, chosenNormal) <= eps)
                return false;

            faces[0] = MakeFace(chosenFace.a, chosenFace.b, newPoint, chosenFace.c);
            faces[1] = MakeFace(chosenFace.b, chosenFace.c, newPoint, chosenFace.a);
            faces[2] = MakeFace(chosenFace.c, chosenFace.a, newPoint, chosenFace.b);
            faces[3] = MakeFace(chosenFace.a, chosenFace.b, chosenFace.c, newPoint);
        }
        
        return false;
    }

    public static CollisionPair ExpendingPolygonAlgorithm(CustomCollider collider1, CustomCollider collider2, List<Triangle> faces, List<int> outsideFaces,int maxIterations)
    {
        float eps = 1e-6f;

        for (int i = 0; i < maxIterations; i++)
        {
            Triangle closestFace = faces[GetClosestFaceIndex(faces, outsideFaces)];
            Vector3 dir = closestFace.GetNormal();

            float dist = Vector3.Dot(dir, closestFace.a);

            Vector3 newPoint = GetSupport(collider1, collider2, dir);
            float newDist = Vector3.Dot(newPoint, dir);


            if (newDist - dist < eps)
            {
                Vector3 Cp = ProjectOriginOnTriangle(closestFace.a.point, closestFace.b.point, closestFace.c.point);
                Vector3 bary = ComputeBarycentric(Cp, closestFace.a.point, closestFace.b.point, closestFace.c.point);

                Vector3 point = bary.x * closestFace.a.supportA + bary.y * closestFace.b.supportA + bary.z * closestFace.c.supportA;
                Vector3 contactB = bary.x * closestFace.a.supportB + bary.y * closestFace.b.supportB + bary.z * closestFace.c.supportB;

                CollisionPair pair = new CollisionPair();
                pair.normal = closestFace.GetNormal();
                pair.penetration = dist;

                return pair;
                
            }
            // Remake face 
            faces[0] = MakeFace(closestFace.a, closestFace.b, newPoint, closestFace.c);
            faces[1] = MakeFace(closestFace.b, closestFace.c, newPoint, closestFace.a);
            faces[2] = MakeFace(closestFace.c, closestFace.a, newPoint, closestFace.b);
            faces[3] = MakeFace(closestFace.a, closestFace.b, closestFace.c, newPoint);
        }
        return new CollisionPair { };
    }

    void Start()
    {
        
        colliders = FindObjectsOfType<CustomCollider>().ToList();

        foreach (CustomCollider collider in colliders)
        {
            if (!collider.gameObject.activeInHierarchy)
                continue;
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

    void Update()
    {
        //for (int i = 0; i < colliders.Count; i++)
        //{
        //    //colliders[i].UpdateCollider(); 
        //    collidersBounds[i] = colliders[i].GetAABB();
        //}
        
        boundsTree.Clear();
        bounds.Clear();
        root = 0;
        availableBoundsTreeIndexes.Clear();
        availableBoundsIndexes.Clear();
        BuildAABBTree();
        List<CollisionPair> pairs = DetectCollisions();
    }

    // AABB Tree functions
    #region Tree
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
            Node first = new Node(-1, -1, -1, true, 0, 0); // 0 beceause there is only one collider so only one aabb in list
            boundsTree.Add(first);
            root = 0;
            return;
        }

        int i = 0;
        foreach (AABB bound in collidersBounds)
        {
            InsertAABB(bound, i);
            i++;
        }
    }

    public void InsertAABB(AABB bound, int colliderIndex)
    {
        // If the tree is empty the first bound become the root
        if (boundsTree.Count == 0)
        {
            Node node = new Node(-1, -1, -1, true, 0, colliderIndex);
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
        } //

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
        Node newParentNode = new Node(currentNode.parentIndex, newLeftIndex, -1, false, newParentAABBIndex, -1);
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
        Node newBoundNode = new Node(newParentNodeIndex, -1, -1, true, newBoundIndex, colliderIndex);
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
    #endregion

    // AABB Tree helper functions, add to list, get index, etc...
    #region TreeHelperFunctions
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
    #endregion
    
    #region MainCollisionFunctions

    public List<CollisionPair> DetectCollisions()
    {
        List<(int, int)> broadPhasePairs = new List<(int, int)>();

        if (root == -1 || boundsTree.Count == 0)
            return new List<CollisionPair>();

        DetectAllCollisionsFromNode(root, broadPhasePairs);
        List<CollisionPair> colliderPairs = new List<CollisionPair>();

        foreach ((int a, int b) in broadPhasePairs)
        {
            if (boundsTree[a].isLeaf && boundsTree[b].isLeaf)
                Debug.Log($"AABB {a} collide with AABB {b}");

            CustomCollider colliderA = colliders[boundsTree[a].ColliderIndex];
            CustomCollider colliderB = colliders[boundsTree[b].ColliderIndex];
            if (CheckGJKCollision(colliderA, colliderB, 64))
            {
                 Debug.Log($"Collider {a} collide with Collider {b}");
            }
           //{
           //
           //}
                
        }

        return colliderPairs;
    }
    public void DetectAllCollisionsFromNode(int nodeIndex, List<(int, int)> outPairs)
    {
        if (nodeIndex == -1)
            return;

        Node node = boundsTree[nodeIndex];
        if (node.isLeaf)
            return;

        DetectCollisionPairsRecursive(node.leftIndex, node.rightIndex, outPairs);

        DetectAllCollisionsFromNode(node.leftIndex, outPairs);
        DetectAllCollisionsFromNode(node.rightIndex, outPairs);
    }

    public void DetectCollisionPairsRecursive(int nodeIndex1, int nodeIndex2, List<(int, int)> outPairs)
    {
        // Root case
        if (nodeIndex1 == nodeIndex2)
        {
            Node node = boundsTree[nodeIndex1];
            if (!node.isLeaf && node.leftIndex != -1 && node.rightIndex != -1)
            {
                DetectCollisionPairsRecursive(node.leftIndex, node.rightIndex, outPairs);
            }
            return;
        }

        Node node1 = boundsTree[nodeIndex1];
        Node node2 = boundsTree[nodeIndex2];

        if (!AABB.CheckAABBCollision(bounds[node1.AABBIndex], bounds[node2.AABBIndex]))
        {
            return;
        }

        if (node1.isLeaf && node2.isLeaf)
        {
            if (AABB.CheckAABBCollision(bounds[node1.AABBIndex], bounds[node2.AABBIndex]))
            {
                outPairs.Add((nodeIndex1, nodeIndex2));
            }
            return;
        }

        if (node1.isLeaf && !node2.isLeaf)
        {
            DetectCollisionPairsRecursive(nodeIndex1, node2.leftIndex, outPairs);
            DetectCollisionPairsRecursive(nodeIndex1, node2.rightIndex, outPairs);
            return;
        }

        if (node2.isLeaf && !node1.isLeaf)
        {
            DetectCollisionPairsRecursive(nodeIndex2, node1.leftIndex, outPairs);
            DetectCollisionPairsRecursive(nodeIndex2, node1.rightIndex, outPairs);
            return;
        }

        DetectCollisionPairsRecursive(node1.leftIndex, node2.leftIndex, outPairs);
        DetectCollisionPairsRecursive(node1.leftIndex, node2.rightIndex, outPairs);
        DetectCollisionPairsRecursive(node1.rightIndex, node2.leftIndex, outPairs);
        DetectCollisionPairsRecursive(node1.rightIndex, node2.rightIndex, outPairs);
    }

    // Basic half work 
    public void DetectCollisionPair(int nodeIndex, List<(int, int)> outPairs)
    {
        Node node = boundsTree[nodeIndex];

        if (node.leftIndex == -1 && node.rightIndex == -1)
            return;

        Node leftNode = boundsTree[node.leftIndex];
        Node rightNode = boundsTree[node.rightIndex];

        if ((leftNode.isLeaf && rightNode.isLeaf) && AABB.CheckAABBCollision(bounds[leftNode.AABBIndex], bounds[rightNode.AABBIndex]))
        {
            outPairs.Add((node.leftIndex, node.rightIndex));
        }
        else if (!leftNode.isLeaf)
        {
            DetectCollisionPair(node.leftIndex, outPairs);
        }
        else if (!rightNode.isLeaf)
        {
            DetectCollisionPair(node.rightIndex, outPairs);
        }

    }
    #endregion
}
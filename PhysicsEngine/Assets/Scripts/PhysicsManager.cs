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
    [SerializeField] private float penetrationPercentage = 0.4f;
    [SerializeField] private float penetrationAllowance = 0.1f;
    
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
    
    public class Triangle
    {
        public Vector3 a, b, c;
        private Vector3 normal;
        private bool isNormalComputed = false;
        
        public Triangle(Vector3 _a, Vector3 _b, Vector3 _c)
        {
            Set(_a, _b, _c);
        }

        public void Set(Vector3 _a, Vector3 _b, Vector3 _c)
        {
                this.a = _a;
                this.b = _b;
                this.c = _c;

            isNormalComputed = false;
        }

        public Vector3 GetNormal()
        {
            if (isNormalComputed)
                return normal;
            
            normal = Vector3.Cross(b - a, c - a).normalized;
            isNormalComputed = true;
            return normal;
        }

        public static List<Triangle> BuildGJKTetrahedron(List<Vector3> simplex)
        {
            List<Triangle> faces = new List<Triangle>(4);

            if (simplex.Count < 4 || simplex.Count > 4 || simplex.Count <= 0)
            {
                Debug.LogError("Cannot build tetrahedron");
                return faces;
            }

            faces[0] = new Triangle(simplex[0], simplex[1], simplex[2]);
            faces[1] = new Triangle(simplex[1], simplex[2], simplex[3]);
            faces[2] = new Triangle(simplex[2], simplex[3], simplex[0]);
            faces[3] = new Triangle(simplex[3], simplex[0], simplex[1]);
            
            return faces;
        }
        
        public static List<Triangle> BuildGJKTetrahedron(ref List<Triangle> tetrahedron, List<Vector3> newPoints)
        {
            List<Triangle> faces = new List<Triangle>();
            
            
            
            return faces;
        }
    }
    
    private static Triangle GetClosestFace(List<Triangle> faces)
    {
        Triangle closest = new Triangle(Vector3.zero, Vector3.zero, Vector3.zero);
        float minDistance = float.MaxValue;

        foreach (Triangle face in faces)
        {
            Vector3 n = face.GetNormal();

            float distance = Vector3.Dot(n, -face.a);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = face;
            }
        }

        return closest;
    }

    struct ColliderPair
    {
        public int collidersIndex;
        public int boundIndex;
    }

    public struct CollisionPair
    {
        public CustomRigidbody body1;
        public CustomRigidbody body2;
        
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

    // Collider1, Collider2, faces = final samplex of GJK, maxIterations = maximum number of iterations
    public static CollisionPair ExpendingPolytopeAlgorithm(CustomCollider collider1, CustomCollider collider2, List<Vector3> gjkSimplex, int maxIterations)
    {
        // Value use to check value close to zero with float
        float eps = 1e-6f;
        
        List<Triangle> epaSimplex = Triangle.BuildGJKTetrahedron(gjkSimplex);
        
        // Summ create from GJK result, will expend on each iteration until we find the closest face
        
        
        for (int i = 0; i < maxIterations; i++)
        {
            // get closest face to the origin
            Triangle closestFace = GetClosestFace(epaSimplex);
            
            // Get a new support point
            Vector3 supportPoint = GetSupport(collider1, collider2, -closestFace.GetNormal());
            
            // Find the distance to the origin
            float dist = Vector3.Dot(closestFace.GetNormal(), -closestFace.a);
            // Find the distance to the origin with this new support point
            float supportDist = Vector3.Dot(closestFace.GetNormal(), -supportPoint);

            // If distance between new distance from support point and the base distance, the new point is in resonnable distance from the plan
            if (supportDist - dist < eps)
            {
                

                CollisionPair pair = new CollisionPair();
                pair.normal = closestFace.GetNormal();
                pair.penetration = dist;
                
                return pair;
                
            }
            // Add new support point to polygon

        }
        return new CollisionPair { };
    }
    
    private void ResolveCollisions(List<CollisionPair> collisionPairs)
    {
        if (collisionPairs.Count == 0)
            return;
        
        foreach (CollisionPair pair in collisionPairs)
        {
            CustomRigidbody body1 = pair.body1;
            CustomRigidbody body2 = pair.body2;

            if (body1.Type != CustomRigidbody.BodyType.Dynamic && body2.Type != CustomRigidbody.BodyType.Dynamic)
                continue;
            
            Vector3 normal = pair.normal.normalized;
            
            Vector3 ab  = body2.Center - body1.Center;
            if (Vector3.Dot(ab, normal) < float.Epsilon)
                normal = -normal;
            
            Vector3 relativeVelocity = body2.Velocity - body1.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);
            
            if (velocityAlongNormal > float.Epsilon)
                continue;

            float restitution = Mathf.Min(body1.Restitution, body2.Restitution);
            
            float invMass1 = body1.GetInverseMass();
            float invMass2 = body2.GetInverseMass();
            float totalInvMass = invMass1 + invMass2;
            
            float impulseMagnitude = -(1 + restitution) * velocityAlongNormal;
            impulseMagnitude /= totalInvMass;
            
            Vector3 impulse = impulseMagnitude * normal;
            body1.AddImpulse(-impulse);
            body2.AddImpulse(impulse);
            
            float penetration = Mathf.Max(pair.penetration - penetrationAllowance, 0f);
            Vector3 correction = (penetration / totalInvMass) * penetrationPercentage * normal;
            if (body1.Type == CustomRigidbody.BodyType.Dynamic)
                body1.MoveCenter(-correction * invMass1);
            if (body2.Type == CustomRigidbody.BodyType.Dynamic)
                body2.MoveCenter(correction * invMass2);
        }
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

    private static bool CheckGJKCollision(CustomCollider collider1, CustomCollider collider2, uint maxIterations)
    {
        Vector3 direction = collider2.transform.position - collider1.transform.position;
        if (direction == Vector3.zero)
            direction = Vector3.right;

        List<Vector3> simplex = new List<Vector3> { GetSupport(collider1, collider2, direction) };

        direction = -simplex[0];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            Vector3 newPoint = GetSupport(collider1, collider2, direction);

            if (Vector3.Dot(newPoint, direction) <= 0f)
                return false;

            simplex.Add(newPoint);

            if (ContainsOrigin(simplex, ref direction))
                return true;
        }

        return false;
    }

    private static Vector3 GetSupport(CustomCollider collider1, CustomCollider collider2, Vector3 direction)
    {
        return collider1.GetSupport(direction) - collider2.GetSupport(-direction);
    }

    private static bool ContainsOrigin(List<Vector3> simplex, ref Vector3 direction)
    {
        if (simplex.Count == 2)
        {
            Vector3 a = simplex[1];
            Vector3 b = simplex[0];

            Vector3 ab = b - a;
            Vector3 ao = -a;

            if (Vector3.Dot(ab, ao) > 0)
            {
                direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            }
            else
            {
                simplex.RemoveAt(0);
                direction = ao;
            }
        }
        else if (simplex.Count == 3)
        {
            Vector3 a = simplex[2];
            Vector3 b = simplex[1];
            Vector3 c = simplex[0];

            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ao = -a;

            Vector3 abc = Vector3.Cross(ab, ac);

            if (Vector3.Dot(Vector3.Cross(abc, ac), ao) > 0)
            {
                if (Vector3.Dot(ac, ao) > 0)
                {
                    simplex.RemoveAt(1);
                    direction = Vector3.Cross(Vector3.Cross(ac, ao), ac);
                }
                else
                {
                    simplex.RemoveAt(0);
                    return ContainsOrigin(simplex, ref direction);
                }
            }
            else
            {
                if (Vector3.Dot(Vector3.Cross(ab, abc), ao) > 0)
                {
                    if (Vector3.Dot(ab, ao) > 0)
                    {
                        simplex.RemoveAt(0);
                        direction = Vector3.Cross(Vector3.Cross(ab, ao), ab);
                    }
                    else
                    {
                        simplex.Clear();
                        simplex.Add(a);
                        direction = ao;
                    }
                }
                else
                {
                    if (Vector3.Dot(abc, ao) > 0)
                    {
                        direction = abc;
                    }
                    else
                    {
                        (simplex[0], simplex[1]) = (simplex[1], simplex[0]);
                        direction = -abc;
                    }
                }
            }
        }
        else if (simplex.Count == 4)
        {
            Vector3 a = simplex[3];
            Vector3 b = simplex[2];
            Vector3 c = simplex[1];
            Vector3 d = simplex[0];

            Vector3 ao = -a;

            Vector3 abc = Vector3.Cross(b - a, c - a);
            Vector3 acd = Vector3.Cross(c - a, d - a);
            Vector3 adb = Vector3.Cross(d - a, b - a);

            if (Vector3.Dot(abc, ao) > 0)
            {
                simplex.RemoveAt(0);
                direction = abc;
                return false;
            }

            if (Vector3.Dot(acd, ao) > 0)
            {
                simplex.RemoveAt(2);
                direction = acd;
                return false;
            }

            if (Vector3.Dot(adb, ao) > 0)
            {
                simplex.RemoveAt(1);
                direction = adb;
                return false;
            }
            
            return true;
        }
        return false;
    }
    
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
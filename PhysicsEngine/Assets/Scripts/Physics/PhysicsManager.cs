using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer.Internal;
using UnityEditor.Experimental.GraphView;
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

    #region Properties
    // Collision resolution properties
    [SerializeField] private float penetrationPercentage = 0.4f;
    [SerializeField] private float penetrationAllowance = 0.1f;
    [SerializeField] private float bounceThreshold = 0.5f;

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
    private List<int> availableColliderIndexes = new List<int>();

    // Index of the root 
    private int root;
    
    //private bool IsDebugMode = false;
    #endregion

    #region MonoBehaviour Methods
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

    private bool isRemove = false;
    void FixedUpdate()
    {
        // TODO : Change to update existing tree instead of rebuild every frame
        boundsTree.Clear();
        bounds.Clear();
        root = -1;
        availableBoundsTreeIndexes.Clear();
        availableBoundsIndexes.Clear();
        BuildAABBTree();
        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    if (!isRemove)
        //    {
        //        RemoveAABB(colliders[0].GetAABB());
        //        isRemove = true;
        //    }
        //    else
        //    {
        //        isRemove = false;
        //         InsertAABB(colliders[0].GetAABB(), 0);
        //    }
        //}
        List<EPA.CollisionPair> pairs = DetectCollisions();
        ResolveCollisions(pairs);
    }
    #endregion

    #region AABB Tree

    private int AddColliderToTracking(CustomCollider collider)
    {
        if (!collider) return -1;

        collider.InitAABB();
        AABB bound = collider.GetAABB();

        int index;
        if (availableColliderIndexes.Count > 0)
        {
            index = availableColliderIndexes[0];
            availableColliderIndexes.RemoveAt(0);

            if (index < colliders.Count) colliders[index] = collider;
            else
            {
                colliders.Add(collider);
                index = colliders.Count - 1;
            }

            if (index < collidersBounds.Count) collidersBounds[index] = bound;
            else
            {
                collidersBounds.Add(bound);
            }
        }
        else
        {
            colliders.Add(collider);
            collidersBounds.Add(bound);
            index = colliders.Count - 1;
        }

        return index;
    }
    
    public void AddCollider(GameObject newColliderObject)
    {
        bool foundColliderComponent = newColliderObject.TryGetComponent<CustomCollider>(out CustomCollider collider);
        if (!colliders.Contains(collider) && foundColliderComponent)
        {
            int index = AddColliderToTracking(collider);
            InsertAABB(collider.GetAABB(), index);
        }
    }
    
    public void RemoveCollider(GameObject ColliderObject)
    {
        bool foundColliderComponent = ColliderObject.TryGetComponent<CustomCollider>(out CustomCollider collider);
        if (colliders.Contains(collider) && foundColliderComponent)
        {
            int idx = colliders.IndexOf(collider);
            if (idx < 0) return;

            // Retire son AABB de l’arbre
            AABB bound = collidersBounds[idx];
            if (bound != null)
                RemoveAABB(bound);

            colliders[idx] = null;
            collidersBounds[idx] = null;
            availableColliderIndexes.Add(idx);
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
            Node first =
                new Node(-1, -1, -1, true, 0, 0); // 0 beceause there is only one collider so only one aabb in list
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

    public void UpdateTree(CustomCollider collider)
    {//TODO : Uncomment this when its working
        //AABB bound = collider.GetAABB();
        //
        //int colliderIndex = colliders.IndexOf(collider);
        //if (colliderIndex < 0)
        //    return;
        //
        //RemoveAABB(bound);
        //InsertAABB(bound, colliderIndex);
    }
    public void InsertAABB(AABB bound, int colliderIndex)
    {
        if (bound == null) return;

        // Si l’arbre est vide
        if (boundsTree.Count == 0 || root == -1)
        {
            int leafBoundIndex = AddAndReturnBoundIndex(bound);
            Node node = new Node(-1, -1, -1, true, leafBoundIndex, colliderIndex);
            int nodeIndex = AddAndReturnNodeIndex(node);
            root = nodeIndex;
            return;
        }

        Node currentNode = boundsTree[root];
        bool isLeft = true;

        // Descend jusqu'à une feuille
        while (currentNode != null && !currentNode.isLeaf)
        {
            int leftBI = GetBoundIndexFromTree(currentNode.leftIndex);
            int rightBI = GetBoundIndexFromTree(currentNode.rightIndex);
            AABB leftB = (leftBI >= 0 && leftBI < bounds.Count) ? bounds[leftBI] : null;
            AABB rightB = (rightBI >= 0 && rightBI < bounds.Count) ? bounds[rightBI] : null;

            float leftValue = leftB != null ? AABB.GetUnionCost(leftB, bound) : float.PositiveInfinity;
            float rightValue = rightB != null ? AABB.GetUnionCost(rightB, bound) : float.PositiveInfinity;

            if (leftValue <= rightValue)
            {
                isLeft = true;
                currentNode = boundsTree[currentNode.leftIndex];
            }
            else
            {
                isLeft = false;
                currentNode = boundsTree[currentNode.rightIndex];
            }
        }

        // Sécurité
        if (currentNode == null) return;

        // Nouveau parent
        AABB newParentAABB = new AABB();
        if (currentNode.AABBIndex < 0 || currentNode.AABBIndex >= bounds.Count) return;
        var currentBound = bounds[currentNode.AABBIndex];
        if (currentBound == null) return;

        newParentAABB.SetAABB(currentBound, bound);
        int newParentAABBIndex = AddAndReturnBoundIndex(newParentAABB);

        int newLeftIndex;
        if (currentNode.parentIndex == -1)
        {
            newLeftIndex = root;
        }
        else
        {
            newLeftIndex = isLeft
                ? boundsTree[currentNode.parentIndex].leftIndex
                : boundsTree[currentNode.parentIndex].rightIndex;
        }

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

        currentNode.parentIndex = newParentNodeIndex;

        int newBoundIndex = AddAndReturnBoundIndex(bound);
        Node newBoundNode = new Node(newParentNodeIndex, -1, -1, true, newBoundIndex, colliderIndex);
        int newBoundNodeIndex = AddAndReturnNodeIndex(newBoundNode);
        boundsTree[newParentNodeIndex].rightIndex = newBoundNodeIndex;

        UpdateFromChildren(newParentNodeIndex);
    }
    
    public void RemoveAABB(AABB bound)
    {
        if (bound == null) return;

        int nodeIndex = -1;

        for (int i = 0; i < boundsTree.Count; i++)
        {
            Node node = boundsTree[i];
            if (node == null || !node.isLeaf) continue;
            int aabbIdx = node.AABBIndex;
            if (aabbIdx >= 0 && aabbIdx < bounds.Count && bounds[aabbIdx] == bound)
            {
                nodeIndex = i;
                break;
            }
        }

        if (nodeIndex == -1) return;

        Node leafNode = boundsTree[nodeIndex];
        int oldParentIndex = leafNode.parentIndex;

        // Si c'était la racine
        if (oldParentIndex == -1)
        {
            // Libère la bound du leaf
            if (leafNode.AABBIndex >= 0 && leafNode.AABBIndex < bounds.Count)
            {
                bounds[leafNode.AABBIndex] = null;
                availableBoundsIndexes.Add(leafNode.AABBIndex);
            }

            boundsTree[nodeIndex] = null;
            availableBoundsTreeIndexes.Add(nodeIndex);
            root = -1;
            return;
        }

        Node oldParent = boundsTree[oldParentIndex];
        if (oldParent == null) return;

        int siblingIndex = (oldParent.leftIndex == nodeIndex) ? oldParent.rightIndex : oldParent.leftIndex;
        if (siblingIndex < 0 || siblingIndex >= boundsTree.Count) return;
        Node sibling = boundsTree[siblingIndex];
        if (sibling == null) return;

        sibling.parentIndex = oldParent.parentIndex;

        if (oldParent.parentIndex == -1)
        {
            root = siblingIndex;
        }
        else
        {
            Node grandParent = boundsTree[oldParent.parentIndex];
            if (grandParent != null)
            {
                if (grandParent.leftIndex == oldParentIndex)
                    grandParent.leftIndex = siblingIndex;
                else
                    grandParent.rightIndex = siblingIndex;
            }
        }

        // Libère les AABB du parent et de la feuille
        if (oldParent.AABBIndex >= 0 && oldParent.AABBIndex < bounds.Count)
        {
            bounds[oldParent.AABBIndex] = null;
            availableBoundsIndexes.Add(oldParent.AABBIndex);
        }
        if (leafNode.AABBIndex >= 0 && leafNode.AABBIndex < bounds.Count)
        {
            bounds[leafNode.AABBIndex] = null;
            availableBoundsIndexes.Add(leafNode.AABBIndex);
        }

        boundsTree[oldParentIndex] = null;
        availableBoundsTreeIndexes.Add(oldParentIndex);

        boundsTree[nodeIndex] = null;
        availableBoundsTreeIndexes.Add(nodeIndex);

        if (sibling.parentIndex != -1)
            UpdateFromChildren(sibling.parentIndex);
    }
    
    private void UpdateFromChildren(int nodeIndex)
    {
        AABB leftBound = bounds[GetBoundIndexFromTree(boundsTree[nodeIndex].leftIndex)];
        AABB rightBound = bounds[GetBoundIndexFromTree(boundsTree[nodeIndex].rightIndex)];
        ;
        bounds[boundsTree[nodeIndex].AABBIndex].SetAABB(leftBound, rightBound);
        int parentIndex = boundsTree[nodeIndex].parentIndex;
        if (parentIndex != -1)
            UpdateFromChildren(parentIndex);
    }

    #endregion
    
    #region Tree Helper Methods

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

    public List<EPA.CollisionPair> DetectCollisions()
    {
        List<(int, int)> broadPhasePairs = new List<(int, int)>();

        if (root == -1 || boundsTree.Count == 0)
            return new List<EPA.CollisionPair>();

        DetectAllCollisionsFromNode(root, broadPhasePairs);
        List<EPA.CollisionPair> collisionPairs = new List<EPA.CollisionPair>();

        foreach ((int a, int b) in broadPhasePairs)
        {
            CustomCollider colliderA = colliders[boundsTree[a].ColliderIndex];
            CustomCollider colliderB = colliders[boundsTree[b].ColliderIndex];

            List<Vector3> outGJKPoints = new List<Vector3>();
            if (GJK.CheckGJKCollision(colliderA, colliderB, 64, ref outGJKPoints))
            {
                EPA.CollisionPair pair = EPA.ExpendingPolytopeAlgorithm(colliderA, colliderB, outGJKPoints, 64);
                if (pair.point != Vector3.zero && pair.normal != Vector3.zero && pair.penetration != 0f)
                {
                    colliderA.TriggerEnter(colliderB);
                    colliderB.TriggerEnter(colliderA);
                    collisionPairs.Add(pair);
                }
            }
        }
        return collisionPairs;
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
    
    public void ResolveCollisions(List<EPA.CollisionPair> collisionPairs)
    {
        if (collisionPairs.Count == 0)
            return;

        const float eps = 1e-5f;

        foreach (EPA.CollisionPair pair in collisionPairs)
        {
            CustomRigidbody body1 = pair.body1;
            CustomRigidbody body2 = pair.body2;

            if (!body1 || !body2)
                continue;

            if (body1.Type != CustomRigidbody.BodyType.Dynamic && body2.Type != CustomRigidbody.BodyType.Dynamic)
                continue;

            Vector3 normal = pair.normal.sqrMagnitude > eps ? pair.normal.normalized : Vector3.up;

            Vector3 ab = body2.Center - body1.Center;
            if (Vector3.Dot(ab, normal) <= 0f)
                normal = -normal;

            Vector3 relativeVelocity = body2.Velocity - body1.Velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            if (velocityAlongNormal > 0f)
                continue;

            float restitution = Mathf.Min(body1.Restitution, body2.Restitution);

            if (Mathf.Abs(restitution) < bounceThreshold)
                restitution = 0f;

            Debug.Log($"Restitution: {restitution}");

            float invMass1 = body1.GetInverseMass();
            float invMass2 = body2.GetInverseMass();
            float totalInvMass = invMass1 + invMass2;
            if (totalInvMass <= eps)
                continue;

            float impulseMagnitude = -(1 + restitution) * velocityAlongNormal;
            impulseMagnitude /= totalInvMass;

            // Normal impulse
            Vector3 impulse = impulseMagnitude * normal;
            body1.AddImpulse(-impulse);
            body2.AddImpulse(impulse);

            // Friction impulse
            Vector3 tangent = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal;
            if (tangent.sqrMagnitude > eps)
            {
                tangent.Normalize();
                float velocityAlongTangent = Vector3.Dot(relativeVelocity, tangent);

                if (velocityAlongTangent > eps)
                {
                    float staticFriction = Mathf.Sqrt(body1.StaticFriction * body2.StaticFriction);
                    float dynamicFriction = Mathf.Sqrt(body1.DynamicFriction * body2.DynamicFriction);
                    float frictionImpulseMag = -velocityAlongTangent / totalInvMass;

                    float maxFriction = impulseMagnitude * staticFriction;
                    Vector3 frictionImpulse;
                    if (Mathf.Abs(frictionImpulseMag) < maxFriction)
                    {
                        // Static friction
                        frictionImpulse = frictionImpulseMag * tangent;
                    }
                    else
                    {
                        // Dynamic friction
                        frictionImpulse = -impulseMagnitude * dynamicFriction * tangent;
                    }

                    body1.AddImpulse(-frictionImpulse);
                    body2.AddImpulse(frictionImpulse);
                }
            }

            // Positional correction
            float penetration = Mathf.Max(pair.penetration - penetrationAllowance, 0f);
            if (penetration > 0f)
            {
                Vector3 correction = (penetration / totalInvMass) * penetrationPercentage * normal;
                if (body1.Type == CustomRigidbody.BodyType.Dynamic)
                    body1.MoveCenter(-correction * invMass1);
                if (body2.Type == CustomRigidbody.BodyType.Dynamic)
                    body2.MoveCenter(correction * invMass2);
            }
        }
    }
    #endregion
}
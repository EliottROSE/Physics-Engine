using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer.Internal;
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
    
    private List<Vector3> localSpherePoints = new List<Vector3>();
    public List<Vector3> LocalSpherePoints => localSpherePoints;
    
    //private bool IsDebugMode = false;
    #endregion

    #region MonoBehaviour Methods

    private void Awake()
    {
        localSpherePoints = GenerateSpherePoints(64);
        for (int i = 0; i < localSpherePoints.Count; i++)
            localSpherePoints[i] *= 0.5f;
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

    private bool isRemove = false;
    void FixedUpdate()
    {
        List<EPA.CollisionPair> pairs = DetectCollisions();
        for (int i = 0; i < 4; i++)
            ResolveCollisions(pairs);
    }
    #endregion
    
    #region Collider Management
    public static List<Vector3> GenerateSpherePoints(int count)
    {
        List<Vector3> pts = new List<Vector3>(count);
        float phi = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < count; i++)
        {
            float y = 1f - (i / (float)(count - 1)) * 2f;
            float radius = Mathf.Sqrt(1 - y * y);
            float theta = phi * i;
            float x = Mathf.Cos(theta) * radius;
            float z = Mathf.Sin(theta) * radius;
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
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
        AABB bound = collider.GetAABB();
        
        int colliderIndex = colliders.IndexOf(collider);
        if (colliderIndex < 0)
            return;
        
        RemoveAABB(bound);
        InsertAABB(bound, colliderIndex);
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
            if (boundsTree != null && (boundsTree[a] == null || boundsTree[b] == null))
            {
                continue;
            }
            
            CustomCollider colliderA = colliders[boundsTree[a].ColliderIndex];
            CustomCollider colliderB = colliders[boundsTree[b].ColliderIndex];

            List<EPA.SupportPoint> outGJKPoints = new List<EPA.SupportPoint>();
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
            Vector3 contact = pair.point;

            Vector3 ab = body2.Center - body1.Center;
            if (Vector3.Dot(ab, normal) <= 0f)
                normal = -normal;

            Vector3 ra = contact - body1.Center;
            Vector3 rb = contact - body2.Center;

            // Relative velocity at contact
            Vector3 v1p = body1.Velocity + Vector3.Cross(body1.AngularVelocity, ra);
            Vector3 v2p = body2.Velocity + Vector3.Cross(body2.AngularVelocity, rb);
            Vector3 relativeVelocity = v2p - v1p;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            if (velocityAlongNormal > 0f)
                continue;

            float restitution = Mathf.Min(body1.Restitution, body2.Restitution);
            if (Mathf.Abs(velocityAlongNormal) < bounceThreshold)
                restitution = 0f;

            float invMass1 = body1.GetInverseMass();
            float invMass2 = body2.GetInverseMass();
            float totalInvMass = invMass1 + invMass2;
            if (totalInvMass <= eps)
                continue;

            // Inertia tensors
            Matrix4x4 invInertia1 = body1.GetInverseInertiaTensorWorld();
            Matrix4x4 invInertia2 = body2.GetInverseInertiaTensorWorld();

            // Angular components
            Vector3 raCrossN = Vector3.Cross(ra, normal);
            Vector3 rbCrossN = Vector3.Cross(rb, normal);

            Vector3 termA = Vector3.Cross(invInertia1.MultiplyVector(raCrossN), ra);
            Vector3 termB = Vector3.Cross(invInertia2.MultiplyVector(rbCrossN), rb);
            float angularFactor = Mathf.Max(0f, Vector3.Dot(termA + termB, normal));

            Vector3 momentum1 = invInertia1.MultiplyVector(Vector3.Cross(ra, normal));
            Vector3 momentum2 = invInertia2.MultiplyVector(Vector3.Cross(rb, normal)); // <-- corrigé ici

            float denom = totalInvMass +
                          Vector3.Dot(Vector3.Cross(momentum1, ra), normal) +
                          Vector3.Dot(Vector3.Cross(momentum2, rb), normal);

            float impulseMagnitude = -(1f + restitution) * velocityAlongNormal / denom;
            if (!float.IsFinite(impulseMagnitude))
                continue;

            // --- Normal impulse ---
            Vector3 impulse = impulseMagnitude * normal;

            // Linear impulses
            body1.AddImpulse(-impulse);
            body2.AddImpulse( impulse);

            // Angular impulses (correct conversion: Δω = I⁻¹ * (r × impulse))
            Vector3 angDelta1 = invInertia1.MultiplyVector(Vector3.Cross(ra, -impulse));
            Vector3 angDelta2 = invInertia2.MultiplyVector(Vector3.Cross(rb,  impulse));

            body1.AddAngularImpulse(angDelta1);
            body2.AddAngularImpulse(angDelta2);

            // --- Friction impulse ---
            Vector3 tangent = relativeVelocity - Vector3.Dot(relativeVelocity, normal) * normal;
            if (tangent.sqrMagnitude > eps)
            {
                tangent.Normalize();
                float velocityAlongTangent = Vector3.Dot(relativeVelocity, tangent);
                
                if (Mathf.Abs(velocityAlongTangent) < 0.1f)
                    velocityAlongTangent = 0f;

                if (Mathf.Abs(velocityAlongTangent) > eps)
                {
                    Vector3 raCrossT = Vector3.Cross(ra, tangent);
                    Vector3 rbCrossT = Vector3.Cross(rb, tangent);
                    Vector3 termAT = Vector3.Cross(invInertia1.MultiplyVector(raCrossT), ra);
                    Vector3 termBT = Vector3.Cross(invInertia2.MultiplyVector(rbCrossT), rb);
                    float angularFactorT = Mathf.Max(0f, Vector3.Dot(termAT + termBT, tangent));
                    float denomT = totalInvMass + angularFactorT;

                    if (denomT > eps)
                    {
                        float jT = -velocityAlongTangent / denomT;

                        float muS = Mathf.Sqrt(body1.StaticFriction * body2.StaticFriction);
                        float muD = Mathf.Sqrt(body1.DynamicFriction * body2.DynamicFriction);
                        float maxStatic = Mathf.Abs(impulseMagnitude) * muS;

                        Vector3 jtVec = (Mathf.Abs(jT) < maxStatic)
                            ? jT * tangent                                   // static
                            : -Mathf.Sign(velocityAlongTangent) * muD * Mathf.Abs(impulseMagnitude) * tangent; // dynamic

                        // Linear friction
                        body1.AddImpulse(-jtVec);
                        body2.AddImpulse( jtVec);

                        // Angular friction
                        Vector3 angF1 = invInertia1.MultiplyVector(Vector3.Cross(ra, -jtVec));
                        Vector3 angF2 = invInertia2.MultiplyVector(Vector3.Cross(rb,  jtVec));
                        body1.AddAngularImpulse(angF1);
                        body2.AddAngularImpulse(angF2);
                    }
                }
            }

            // --- Rolling / spinning resistance ---
            float w1 = body1.AngularVelocity.magnitude;
            float w2 = body2.AngularVelocity.magnitude;
            if (w1 > 1e-6f)
            {
                float k = 0.05f * body1.DynamicFriction;
                Vector3 torque = -body1.AngularVelocity.normalized * k * (w1 * w1);
                Vector3 deltaOmega = invInertia1.MultiplyVector(torque * Time.fixedDeltaTime);
                body1.AddAngularImpulse(deltaOmega);
            }
            if (w2 > 1e-6f)
            {
                float k = 0.05f * body2.DynamicFriction;
                Vector3 torque = -body2.AngularVelocity.normalized * k * (w2 * w2);
                Vector3 deltaOmega = invInertia2.MultiplyVector(torque * Time.fixedDeltaTime);
                body2.AddAngularImpulse(deltaOmega);
            }

            // --- Clamp tiny angular velocities ---
            const float angZeroThreshold = 1e-3f;
            if (body1.AngularVelocity.magnitude < angZeroThreshold)
                body1.angularVelocity = Vector3.zero;
            if (body2.AngularVelocity.magnitude < angZeroThreshold)
                body2.angularVelocity = Vector3.zero;

            // --- Positional correction ---
            float penetration = Mathf.Max(pair.penetration - penetrationAllowance, 0f);
            if (penetration > 0f)
            {
                Vector3 correction = (penetration / totalInvMass) * penetrationPercentage * normal;
                if (body1.Type == CustomRigidbody.BodyType.Dynamic)
                    body1.MoveCenter(-correction * invMass1);
                if (body2.Type == CustomRigidbody.BodyType.Dynamic)
                    body2.MoveCenter(correction * invMass2);
            }
            
            // --- Clamp tiny linear velocities ---
            const float linZeroThreshold = 0.01f;
            if (body1.Velocity.magnitude < linZeroThreshold)
                body1.SetVelocity(Vector3.zero);
            if (body2.Velocity.magnitude < linZeroThreshold)
                body2.SetVelocity(Vector3.zero);
        }
    }
    #endregion
}
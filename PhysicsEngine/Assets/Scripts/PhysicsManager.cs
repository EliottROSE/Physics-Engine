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
                instance = FindFirstObjectByType<PhysicsManager>();
            return instance;
        }
    }

    #endregion

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

    public class MinkowskiTetrahedron
    {
        public List<Triangle> faces = new List<Triangle>();
        
        public MinkowskiTetrahedron(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            faces.Add(new Triangle(a, b, c));
            faces.Add(new Triangle(b, c, d));
            faces.Add(new Triangle(c, d, a));
            faces.Add(new Triangle(a, b, d));
        }

        private Triangle MakeFace(Vector3 a, Vector3 b, Vector3 c, Vector3 opposite)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, opposite - a) < 0f)
                return new Triangle(a, c, b);
            return new Triangle(a, b, c);
        }

        public bool IsOriginInsideMinkowskiTetrahedron()
        {
            foreach (Triangle face in faces)
            {
                Vector3 n = face.GetNormal();
                float dot = Vector3.Dot(n, -face.a);
                if (dot > 0f)
                    return false;
            }
            return true;
        }

        public void Reshape(CustomCollider collider1, CustomCollider collider2)
        {
            Triangle chosenFace = null;
            Vector3 chosenNormal = Vector3.zero;
            float minDistance = float.MaxValue;

            foreach (Triangle face in faces)
            {
                Vector3 n = face.GetNormal();
                float dot = Vector3.Dot(n, -face.a);
                if (dot > 0f)
                {
                    float dist = Mathf.Abs(dot);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        chosenFace = face;
                        chosenNormal = n;
                    }
                }
            }

            if (chosenFace == null)
                return;
            
            if (chosenNormal.sqrMagnitude < 1e-8f)
                return;

            Vector3 newPoint = GetSupport(collider1, collider2, chosenNormal);
            if (Vector3.Dot(newPoint, chosenNormal) <= 0f)
                return;
            
            faces.Clear();
            faces.Add(MakeFace(chosenFace.a, chosenFace.b, newPoint, chosenFace.c));
            faces.Add(MakeFace(chosenFace.b, chosenFace.c, newPoint, chosenFace.a));
            faces.Add(MakeFace(chosenFace.c, chosenFace.a, newPoint, chosenFace.b));
            faces.Add(MakeFace(chosenFace.a, chosenFace.b, chosenFace.c, newPoint));
        }
    }

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

    public static Vector3 GetSupport(CustomCollider collider1, CustomCollider collider2, Vector3 direction)
    {
        return collider1.GetSupport(direction) - collider2.GetSupport(-direction);
    }

    public static bool CheckGJKCollision(CustomCollider collider1, CustomCollider collider2, uint maxIterations)
    {
        if (maxIterations == 0)
            return false;

        Vector3 direction = Vector3.right;
        
        Vector3 point1 = GetSupport(collider1, collider2, direction);

        direction = -point1;
        direction = (direction.sqrMagnitude > Mathf.Epsilon) ? direction : Vector3.right;
        
        Vector3 point2 = GetSupport(collider1, collider2, direction);
        
        Vector3 lineNormal = Vector3.Cross(point1, point2);
        if (Vector3.Dot(lineNormal, -point1) < 0f) 
            lineNormal = -lineNormal;
        
        if (lineNormal.sqrMagnitude < 1e-8f)
            return false;
        
        lineNormal.Normalize();
        
        Vector3 point3 = GetSupport(collider1, collider2, lineNormal);
        
        Vector3 faceNormal = Vector3.Cross(point2 - point1, point3 - point1);
        if (Vector3.Dot(faceNormal, -point1) < 0f) 
            faceNormal = -faceNormal;
        
        if (faceNormal.sqrMagnitude < 1e-8f)
            return false;
        
        faceNormal.Normalize();
        
        Vector3 point4 = GetSupport(collider1, collider2, faceNormal);
        
        MinkowskiTetrahedron tetrahedron = new MinkowskiTetrahedron(point1, point2, point3, point4);
        
        for (int i = 0; i < maxIterations; ++i)
        {
            if (tetrahedron.IsOriginInsideMinkowskiTetrahedron())
                return true;

            tetrahedron.Reshape(collider1, collider2);
        }
        
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
        Gizmos.color = Color.red;
        //foreach (AABB bound in bounds)
        //{
        //        Gizmos.DrawWireCube(bound.GetPosition(), bound.GetScale());
        //    if (bound)
        //    else
        //        Gizmos.DrawWireCube(bound.GetPosition(), bound.GetScale(), Color.green);
        //}
        
        foreach (Node node in boundsTree)
        {
            if (node.isLeaf)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(bounds[node.AABBIndex].GetPosition(), bounds[node.AABBIndex].GetScale());
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(bounds[node.AABBIndex].GetPosition(), bounds[node.AABBIndex].GetScale());
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        bounds.Clear();
        boundsTree.Clear();
        root = 0;

        BuildAABBTree();
        
        if (CheckGJKCollision(colliders[0], colliders[1], 16))
            Debug.Log("GJK Collision");
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
            bounds.Add(bound);
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
        int newParentIndex = boundsTree.Count - 1;
        if (currentNode.parentIndex == -1)
        {
            root = boundsTree.Count - 1;
        }
        else
        {
            if (isLeft)
                boundsTree[currentNode.parentIndex].leftIndex = boundsTree.Count - 1;
            else
                boundsTree[currentNode.parentIndex].rightIndex = boundsTree.Count - 1;
        }
                
        // Change currentNode informations
        currentNode.parentIndex = boundsTree.Count - 1;

        // new bound node
        bounds.Add(bound);
        Node newBoundNode = new Node(boundsTree.Count - 1, -1, -1, true, bounds.Count - 1);
        boundsTree.Add(newBoundNode);
        newParentNode.rightIndex = boundsTree.Count - 1;
        UpdateFromChildren(newParentIndex);
    }

    private void UpdateFromChildren(int nodeIndex)
    {
        AABB leftBound = bounds[boundsTree[boundsTree[nodeIndex].leftIndex].AABBIndex];
        AABB rightBound = bounds[boundsTree[boundsTree[nodeIndex].rightIndex].AABBIndex];
        bounds[boundsTree[nodeIndex].AABBIndex].SetAABB(leftBound, rightBound);
        int parentIndex = boundsTree[nodeIndex].parentIndex;
        if (parentIndex != -1)
            UpdateFromChildren(parentIndex);
    }
}
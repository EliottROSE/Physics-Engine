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
        const float EPS = 1e-8f;
        
        Vector3 direction = collider2.transform.position - collider1.transform.position;
        direction = (direction.sqrMagnitude > EPS) ? direction.normalized : Vector3.right;
        
        Vector3 point1 = GetSupport(collider1, collider2, direction);
        if (Vector3.Dot(point1, direction) < 0)
            return false;

        direction = -point1;
        direction = (direction.sqrMagnitude > EPS) ? direction : Vector3.right;
        
        Vector3 point2 = GetSupport(collider1, collider2, direction);
        
        //Vector3 lineNormal = Vector3.Cross(point1, point2);
        Vector3 lineDir = point2 - point1;
        Vector3 lineNormal = Vector3.Cross(lineDir, -point1);
        if (Vector3.Dot(lineNormal, -point1) < 0f) 
            lineNormal = -lineNormal;
        lineNormal = (lineNormal.sqrMagnitude > EPS) ? lineNormal.normalized : Vector3.right;
        
        Vector3 point3 = GetSupport(collider1, collider2, lineNormal);
        
        Vector3 faceNormal = Vector3.Cross(point2 - point1, point3 - point1);
        if (Vector3.Dot(faceNormal, -point1) < 0f) 
            faceNormal = -faceNormal;
        faceNormal = (faceNormal.sqrMagnitude > EPS) ? faceNormal.normalized : Vector3.right;
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
            Vector3 chosenNormal = chosenFace.GetNormal();

            Vector3 newPoint = GetSupport(collider1, collider2, chosenNormal);
            if (Vector3.Dot(newPoint, chosenNormal) <= EPS)
                return false;

            faces[0] = MakeFace(chosenFace.a, chosenFace.b, newPoint, chosenFace.c);
            faces[1] = MakeFace(chosenFace.b, chosenFace.c, newPoint, chosenFace.a);
            faces[2] = MakeFace(chosenFace.c, chosenFace.a, newPoint, chosenFace.b);
            faces[3] = MakeFace(chosenFace.a, chosenFace.b, chosenFace.c, newPoint);
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
        
        if (CheckGJKCollision(colliders[0], colliders[1], 64))
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
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{   
    protected Vector3 position = Vector3.zero;
    protected Quaternion rotation = Quaternion.identity;
    protected Vector3 scale = Vector3.one;
    
    private AABB aabb;
    private LineRenderer AABBdebugRenderer;
    private List<Vector3> AABBdebugPoints = new List<Vector3>(8);

    #region MonoBehaviour
    private void Start()
    {
        aabb = new AABB(transform.position, transform.localScale);
        position = transform.position;
        scale = transform.localScale;
        rotation = transform.rotation;
    }

    private void Update()
    {
        position = transform.position;
        scale = transform.localScale;
        rotation = transform.rotation;
        DrawAABB();
    }
    #endregion
    
    #region Getter
    public Vector3 GetPosition() { return position; }
    public Quaternion GetRotation() { return rotation; }
    public Vector3 GetScale() { return scale; }
    public AABB GetAABB() { return aabb; }
    #endregion
    
    #region CustomMethods
    public void DrawAABB()
    {
        if (AABBdebugRenderer == null)
        {
            AABBdebugRenderer = gameObject.AddComponent<LineRenderer>();
            
            AABBdebugRenderer.startWidth = 0.01f;
            AABBdebugRenderer.endWidth = 0.01f;
            
            AABBdebugRenderer.material = new Material(Shader.Find("Sprites/Default"));
            AABBdebugRenderer.startColor = Color.green;
            AABBdebugRenderer.endColor = Color.green;

            Vector3 AABBmin = aabb.GetAABBMinPos();
            Vector3 AABBmax = aabb.GetAABBMaxPos();
            
            GetDebugPoints(AABBmin, AABBmax);
        }
        AABBdebugRenderer.positionCount = AABBdebugPoints.Count;
        AABBdebugRenderer.SetPositions(AABBdebugPoints.ToArray());
    }

    private void GetDebugPoints(Vector3 AABBmin, Vector3 AABBmax)
    {
        Vector3[] corners = new Vector3[8];
        corners[0] = AABBmin;
        corners[1] = new Vector3(AABBmax.x, AABBmin.y, AABBmin.z);
        corners[2] = new Vector3(AABBmin.x, AABBmax.y, AABBmin.z);
        corners[3] = new Vector3(AABBmax.x, AABBmax.y, AABBmin.z);
        corners[4] = new Vector3(AABBmin.x, AABBmin.y, AABBmax.z);
        corners[5] = new Vector3(AABBmax.x, AABBmin.y, AABBmax.z);
        corners[6] = new Vector3(AABBmin.x, AABBmax.y, AABBmax.z);
        corners[7] = AABBmax;

        int[] edges = new int[]
        {
            0,1, 1,3, 3,2, 2,0, // face avant
            4,5, 5,7, 7,6, 6,4, // face arrière
            0,4, 1,5, 2,6, 3,7  // liaisons
        };

        AABBdebugPoints.Clear();
        for (int i = 0; i < edges.Length; i++)
        {
            AABBdebugPoints.Add(corners[edges[i]]);
        }
    }
    
    #endregion
}

public class CustomBoxCollider : CustomCollider
{
    private Vector3 minPos;
    private Vector3 maxPos;
    
    CustomBoxCollider(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        minPos = position - 1 / 2 * (scale);
        maxPos = position + 1 / 2 * (scale);
    }

    private void Start()
    {
        
    }
    
}

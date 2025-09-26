using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{   
    protected Vector3 position;
    protected Quaternion rotation;
    protected Vector3 scale;
    
    protected Vector3 AABBminPos;
    protected Vector3 AABBmaxPos;
    
    private void Start()
    {
        
    }

    private void Update()
    {
        
    }
    
    public Vector3 GetPosition() { return position; }
    public Quaternion GetRotation() { return rotation; }
    public Vector3 GetScale() { return scale; }
    
    public Vector3 GetAABBMinPos() { return AABBminPos; }
    public Vector3 GetAABBMaxPos() { return AABBmaxPos; }

    static bool CheckAABBCollision(CustomCollider A, CustomCollider B)
    {
        Vector3 minA = A.GetAABBMinPos();
        Vector3 maxA = A.GetAABBMaxPos();
        
        
        return true;
    }

}

public class CustomBoxCollider : CustomCollider
{
    private Vector3 minPos;
    private Vector3 maxPos;
    
    LineRenderer debugRenderer;
    private List<Vector3> debugPoints = new List<Vector3>();

    CustomBoxCollider(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        minPos = position - 1 / 2 * (scale);
        maxPos = position + 1 / 2 * (scale);
    }

    private void Start()
    {
        
    }
    
    public void DrawBox()
    {
        if (debugRenderer == null)
        {
            debugRenderer = gameObject.AddComponent<LineRenderer>();
            
        }
        debugRenderer.positionCount = debugPoints.Count;
        debugRenderer.SetPositions(debugPoints.ToArray());
    }
    
    

}

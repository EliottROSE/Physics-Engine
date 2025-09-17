using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class Collider : MonoBehaviour
{   
    protected Vector3 position;
    protected Quaternion rotation;
    protected Vector3 scale;
    
    private void Start()
    {
        
    }

    private void Update()
    {
        
    }
}

public class BoxCollider : Collider
{
    private Vector3 minPos;
    private Vector3 maxPos;
    
    LineRenderer debugRenderer;
    private List<Vector3> debugPoints = new List<Vector3>();

    BoxCollider(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        // min = center - 1/2*(scale)
        // max = center + 1/2*(scale)
    }

    private void Start()
    {
        debugRenderer = gameObject.AddComponent<LineRenderer>();
    }
    
    public void DrawBox()
    {
        
    }
    
    

}

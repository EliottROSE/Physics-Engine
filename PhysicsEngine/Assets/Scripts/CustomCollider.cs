using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class CustomCollider : MonoBehaviour
{   
    protected Vector3 position = Vector3.zero;
    protected Quaternion rotation = Quaternion.identity;
    protected Vector3 scale = Vector3.one;
    
    private AABB aabb;

    #region MonoBehaviour
    private void Start()
    {
        aabb = gameObject.AddComponent<AABB>();
        position = transform.position;
        scale = transform.localScale;
        rotation = transform.rotation;
    }

    private void Update()
    {
        position = transform.position;
        scale = transform.localScale;
        rotation = transform.rotation;
    }
    #endregion
    
    #region Getter
    public Vector3 GetPosition() { return position; }
    public Quaternion GetRotation() { return rotation; }
    public Vector3 GetScale() { return scale; }
    public AABB GetAABB() { return aabb; }
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

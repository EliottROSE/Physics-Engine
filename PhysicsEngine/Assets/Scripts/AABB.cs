using System;
using System.Collections.Generic;
using UnityEngine;
public class AABB : MonoBehaviour
{
    private Vector3 minPos;
    private Vector3 maxPos;
    
    private LineRenderer AABBdebugRenderer;
    private List<Vector3> AABBdebugPoints = new List<Vector3>(8);
    
    public Vector3 GetAABBMinPos() { return minPos; }
    public Vector3 GetAABBMaxPos() { return maxPos; }

    #region CustomMethods
    public void SetAABB(Vector3 posistion, Vector3 scale)
    {
        minPos = posistion - (0.5f * (scale));
        maxPos = posistion + (0.5f * (scale));
    }
    public float GetVolume()
    {
        float dx = maxPos.x - minPos.x;
        float dy = maxPos.y - minPos.y;
        float dz = maxPos.z - minPos.z;

        return dx * dy * dz;
    }

    public float GetUnionVolume(AABB aabb1, AABB aabb2)
    {
        float minX = MathF.Min(aabb1.minPos.x, aabb2.minPos.x);
        float minY = MathF.Min(aabb1.minPos.y, aabb2.minPos.y);
        float minZ = MathF.Min(aabb1.minPos.z, aabb2.minPos.z);

        float maxX = MathF.Max(aabb1.maxPos.x, aabb2.maxPos.x);
        float maxY = MathF.Max(aabb1.maxPos.y, aabb2.maxPos.y);
        float maxZ = MathF.Max(aabb1.maxPos.z, aabb2.maxPos.z);

        float dx = maxX - minX;
        float dy = maxY - minY;
        float dz = maxZ - minZ;

        return dx * dy * dz;
    }

    public float GetUnionCost(AABB aabb1, AABB aabb2)
    {
        float unionVolume = GetUnionVolume(aabb1, aabb1);
        float cost = unionVolume - aabb1.GetVolume();
        return cost;
    }
    
    public static bool CheckAABBCollision(AABB A, AABB B)
    {
        Vector3 minA = A.GetAABBMinPos();
        Vector3 maxA = A.GetAABBMaxPos();
        
        Vector3 minB = B.GetAABBMinPos();
        Vector3 maxB = B.GetAABBMaxPos();
        
        float d1x = minB.x - maxA.x;
        float d1y = minB.y - maxA.y;
        float d1z = minB.z - maxA.z;
        
        float d2x = minA.x - maxB.x;
        float d2y = minA.y - maxB.y;
        float d2z = minA.z - maxB.z;
        
        if (d1x > 0f || d1y > 0f || d1z > 0f)
            return false;
        
        if (d2x > 0f || d2y > 0f || d2z > 0f)
            return false;
        
        return true;
    }
    
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

            
            GetDebugPoints(minPos, maxPos);
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

    #region MonoBehaviour

    public void Start()
    {
        SetAABB(transform.position, transform.localScale);
    }

    public void OnDrawGizmosSelected()
    {
        Debug.Log("OnDrawGizmos");
        Gizmos.color = Color.green;

        Vector3 center = transform.position;
        Vector3 size = transform.localScale;
        
        Gizmos.DrawWireCube(center, size);
    }

    #endregion
}

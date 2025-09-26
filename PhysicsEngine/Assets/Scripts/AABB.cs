using System;
using UnityEngine;

public class AABB
{
    private Vector3 minPos;
    private Vector3 maxPos;

    public AABB(Vector3 posistion, Vector3 scale)
    {
        minPos = posistion - (0.5f * (scale));
        maxPos = posistion + (0.5f * (scale));
        
    }
    
    public Vector3 GetAABBMinPos() { return minPos; }
    public Vector3 GetAABBMaxPos() { return maxPos; }

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
}

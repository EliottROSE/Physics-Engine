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
    
    static bool CheckAABBCollision(AABB A, AABB B)
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

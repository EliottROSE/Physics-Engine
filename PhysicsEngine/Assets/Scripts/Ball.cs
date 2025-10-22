using UnityEngine;

public class Ball : MonoBehaviour
{
    [SerializeField] private float ballValue = 10f;

    public void SetBallValue(float newValue)
    {
        ballValue = newValue;
    }
    
    public float GetBallValue()
    {
        return ballValue;
    }
}

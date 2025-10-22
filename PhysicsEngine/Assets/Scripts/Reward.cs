using System.Security.Cryptography;
using UnityEngine;

public class Reward : MonoBehaviour
{
    [SerializeField] private float RewardMultiplier = 1.0f;
    [SerializeField] private CustomCollider CustomCollider;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (!TryGetComponent<CustomCollider>(out CustomCollider))
        {
            Debug.LogError("Reward object requires a CustomCollider component.");
        }
        CustomCollider.EventCustomOnEnter += HandleColliderEnter;
    }

    void Update()
    {
        
    }

    public void HandleColliderEnter(CustomCollider other)
    {
        Ball ball;
        if (other.TryGetComponent(out ball))
        {
            GameManager.Instance.ModifyMoney(RewardMultiplier, ball.GetBallValue());
            Debug.Log("OnCollisionEnter Reward : x" + RewardMultiplier);
            
            PhysicsManager.Instance.RemoveCollider(ball.gameObject);
            Destroy(other.gameObject);
        }
    }
}

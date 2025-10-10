using UnityEngine;

public class Rigidbody : MonoBehaviour
{
    enum BodyType
    {
        Static = 0,
        Kinematic = 1,
        Dynamic = 2
    }

    public const float GRAVITYCONST = -9.81f;

    [SerializeField] private BodyType bodyType = BodyType.Static;

    private CustomCollider collider;

    private float speed; // m.s

    void Start()
    {
        if (!TryGetComponent(out collider))
        {
            Debug.LogErrorFormat("A collider must be attached to ", name);
        }
    }


void Update()
    {
        
    }
}

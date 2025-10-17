using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class CustomRigidbody : MonoBehaviour
{
    public enum BodyType
    {
        Static = 0,
        Kinematic = 1,
        Dynamic = 2
    }

    public const float GRAVITYCONST = -9.81f;

    [SerializeField] private BodyType bodyType = BodyType.Static;
    [SerializeField] private float mass = 1.0f; // kg
    [SerializeField] private float airDensity = 1.225f;
    [SerializeField] private float restitution = 0f;
    [SerializeField] private float staticFriction = 0.6f;
    [SerializeField] private float dynamicFriction = 0.4f;

    private CustomCollider collider;

    private Vector3 velocity = Vector3.zero; // m.s
    private Vector3 center;
    
    public BodyType Type => bodyType;
    public Vector3 Velocity => velocity;
    public Vector3 Center => center;
    public float Restitution => restitution;
    public float StaticFriction => staticFriction;
    public float DynamicFriction => dynamicFriction;
    
    public float GetInverseMass()
    {
        if (bodyType != BodyType.Dynamic || mass <= 0f)
            return 0f;
        
        return 1f / mass;
    }
    
    public void SetVelocity(Vector3 velocity) { this.velocity = velocity; }
    public void AddVelocity(Vector3 deltaV) { velocity += deltaV; }
    
    public void MoveCenter(Vector3 move) { center += move; }
    
    public void AddForce(Vector3 force)
    {
        if (bodyType != BodyType.Dynamic)
            return;
        
        Vector3 acceleration = force / mass;
        velocity += acceleration * Time.fixedDeltaTime;
    }
    
    public void AddImpulse(Vector3 impulse)
    {
        if (bodyType != BodyType.Dynamic)
            return;
        
        Vector3 deltaVelocity = impulse / mass;
        velocity += deltaVelocity;
    }
    
    private void ApplyAirResistance()
    {
        if (velocity.sqrMagnitude < 0.0001f)
            return;
        
        float dragCoefficient = collider.GetDragCoefficient();
        float area = collider.GetCrossSectionalArea(velocity.normalized);
        float speed = velocity.magnitude;

        float dragForceMag = 0.5f * airDensity * speed * speed * dragCoefficient * area;

        Vector3 dragAccel = -(dragForceMag / mass) * velocity.normalized;

        velocity += dragAccel * Time.fixedDeltaTime;
    }

    void Start()
    {
        if (!TryGetComponent(out collider))
        {
            Debug.LogErrorFormat("A collider must be attached to ", name);
        }
        
        restitution = Mathf.Clamp(restitution, 0f, 1f);
        center = transform.position;
    }

    private void FixedUpdate()
    {
        if (bodyType == BodyType.Dynamic)
        {
            velocity.y += GRAVITYCONST * Time.fixedDeltaTime;
            
            ApplyAirResistance();
        }
        
        if (bodyType != BodyType.Static)
            center += velocity * Time.fixedDeltaTime;
    }

    private void LateUpdate()
    {
        transform.position = center;
    }
}

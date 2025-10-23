using System;
using System.Collections.Generic;
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

    // Collision resolution properties
    [SerializeField] private BodyType bodyType = BodyType.Static;
    [SerializeField] private float mass = 1.0f; // kg
    [SerializeField] private float airDensity = 1.225f;
    [SerializeField] private float restitution = 0f;
    [SerializeField] private float staticFriction = 0.6f;
    [SerializeField] private float dynamicFriction = 0.4f;
    
    [SerializeField] private bool autoComputeInertia = true;
    [SerializeField] private float angularDamping = 0.05f;
    [SerializeField] private float maxAngularSpeed = 50f;

    private CustomCollider collider;

    private Vector3 velocity = Vector3.zero; // m.s
    public Vector3 angularVelocity = Vector3.zero; // rad/s
    
    // Principal moments of inertia in local space (Ix, Iy, Iz)
    private Vector3 inertiaTensor = new Vector3(1f, 1f, 1f);
    private Matrix4x4 inverseInertiaTensorLocal = Matrix4x4.zero; // diagonal 3x3
    
    public BodyType Type => bodyType;
    public Vector3 Velocity => velocity;
    public Vector3 AngularVelocity => angularVelocity;
    public Vector3 Center => transform.position;
    public Quaternion Orientation => transform.rotation;
    public Vector3 InertiaTensor => inertiaTensor;
    public float Restitution => restitution;
    public float StaticFriction => staticFriction;
    public float DynamicFriction => dynamicFriction;

    private bool isMoving = false;
    private Vector3 lastLossyScale;
    
    public float GetInverseMass()
    {
        if (bodyType != BodyType.Dynamic || mass <= 0f)
            return 0f;
        
        return 1f / mass;
    }
    
    public void SetVelocity(Vector3 velocity) { this.velocity = velocity; }
    public void AddVelocity(Vector3 deltaV) { velocity += deltaV; }
    
    public void SetAngularVelocity(Vector3 velocity) { this.angularVelocity = velocity; }
    
    public void MoveCenter(Vector3 move) { transform.position += move; }
    
    public void SetInertiaTensor(Vector3 inertia)
    {
        inertiaTensor = new Vector3(
            Mathf.Max(1e-6f, inertia.x),
            Mathf.Max(1e-6f, inertia.y),
            Mathf.Max(1e-6f, inertia.z)
        );

        inverseInertiaTensorLocal = Matrix4x4.zero;
        inverseInertiaTensorLocal.m00 = 1f / InertiaTensor.x;
        inverseInertiaTensorLocal.m11 = 1f / InertiaTensor.y;
        inverseInertiaTensorLocal.m22 = 1f / InertiaTensor.z;
        inverseInertiaTensorLocal.m33 = 1f;
    }
    
    // Returns I^{-1} in world space (3x3 in top-left part of 4x4 matrix)
    public Matrix4x4 GetInverseInertiaTensorWorld()
    {
        // Rotation matrix from local -> world orientation
        Matrix4x4 R = Matrix4x4.Rotate(transform.rotation);
        // Iw^{-1} = R * I_local^{-1} * R^T
        Matrix4x4 invWorld = R * inverseInertiaTensorLocal * R.transpose;
        return invWorld;
    }
    
    public void AddForce(Vector3 force)
    {
        if (bodyType != BodyType.Dynamic) return;
        float m = Mathf.Max(1e-6f, mass);
        velocity += (force / m) * Time.fixedDeltaTime;
    }

    public void AddImpulse(Vector3 impulse)
    {
        if (bodyType != BodyType.Dynamic) return;
        float m = Mathf.Max(1e-6f, mass);
        velocity += impulse / m;
    }
    
    public void AddAngularImpulse(Vector3 deltaOmega)
    {
        if (Type != BodyType.Dynamic) return;
        angularVelocity += deltaOmega;
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
    
    private void ApplyRotationalAirResistance()
    {
        if (angularVelocity.sqrMagnitude < 1e-6f)
            return;

        float rotationalDragCoefficient = collider.GetRotationalDragCoefficient();

        float area = collider.GetCrossSectionalArea(angularVelocity.normalized);
        float radius = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z) * 0.5f;

        float omega = angularVelocity.magnitude;

        Vector3 torque = -0.5f * airDensity * rotationalDragCoefficient * area * radius * radius * omega * angularVelocity.normalized;

        // Δω = I⁻¹ * τ * dt
        Matrix4x4 invInertia = GetInverseInertiaTensorWorld();
        Vector3 angularAccel = new Vector3(
            invInertia.m00 * torque.x + invInertia.m01 * torque.y + invInertia.m02 * torque.z,
            invInertia.m10 * torque.x + invInertia.m11 * torque.y + invInertia.m12 * torque.z,
            invInertia.m20 * torque.x + invInertia.m21 * torque.y + invInertia.m22 * torque.z
        );

        angularVelocity += angularAccel * Time.fixedDeltaTime;
    }
    
    private void ApplyAngularDampingAndClamp(float dt)
    {
        float dampingFactor = Mathf.Exp(-angularDamping * dt);
        angularVelocity *= dampingFactor;

        if (angularVelocity.magnitude < 1e-3f)
            angularVelocity = Vector3.zero;

        float w = angularVelocity.magnitude;
        if (w > maxAngularSpeed && maxAngularSpeed > 0f)
            angularVelocity *= maxAngularSpeed / w;
    }

    private void RecomputeInertiaFromCollider()
    {
        if (!autoComputeInertia || !collider) return;

        float m = Mathf.Max(1e-6f, mass);

        if (collider is CustomBoxCollider)
        {
            Vector3 s = transform.lossyScale;
            float x = Mathf.Abs(s.x);
            float y = Mathf.Abs(s.y);
            float z = Mathf.Abs(s.z);
            Vector3 I = new Vector3(
                (1f / 12f) * m * (y * y + z * z),
                (1f / 12f) * m * (x * x + z * z),
                (1f / 12f) * m * (x * x + y * y)
            );
            SetInertiaTensor(I);
        }
        else if (collider is CustomSphereCollider sphere)
        {
            Vector3 s = transform.lossyScale;
            float r = 0.5f * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            float I = 0.4f * m * r * r; // 2/5 m r^2
            SetInertiaTensor(new Vector3(I, I, I));
        }
    }
    
    public void Integrate(float dt)
    {
        if (Type != BodyType.Dynamic) return;

        // linear integration
        transform.position += Velocity * dt;

        // orientation integration (semi-implicit Euler)
        if (AngularVelocity.sqrMagnitude > 0f)
        {
            Quaternion w = new Quaternion(AngularVelocity.x, AngularVelocity.y, AngularVelocity.z, 0f);
            Quaternion q = Orientation;
            Quaternion qDot = w * q;
            qDot.x *= 0.5f; qDot.y *= 0.5f; qDot.z *= 0.5f; qDot.w *= 0.5f;
            q.x += qDot.x * dt; q.y += qDot.y * dt; q.z += qDot.z * dt; q.w += qDot.w * dt;
            transform.rotation = q.normalized;
        }
    }

    void Awake()
    {
        if (!TryGetComponent(out collider))
        {
            Debug.LogErrorFormat("A collider must be attached to ", name);
        }
        
        restitution = Mathf.Clamp(restitution, 0f, 1f);
        
        RecomputeInertiaFromCollider();
        lastLossyScale = transform.lossyScale;
    }

    private void FixedUpdate()
    {
        if (bodyType == BodyType.Dynamic)
        {
            velocity.y += GRAVITYCONST * Time.fixedDeltaTime;
            ApplyAirResistance();
            ApplyRotationalAirResistance();
            ApplyAngularDampingAndClamp(Time.fixedDeltaTime);
            isMoving = true;
        }

        if (autoComputeInertia && (transform.lossyScale - lastLossyScale).sqrMagnitude > 1e-12f)
        {
            RecomputeInertiaFromCollider();
            lastLossyScale = transform.lossyScale;
        }

        if (bodyType != BodyType.Static)
            isMoving = true;

        Integrate(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        if (isMoving)
            PhysicsManager.Instance.UpdateTree(collider);
        isMoving = false;
    }
}

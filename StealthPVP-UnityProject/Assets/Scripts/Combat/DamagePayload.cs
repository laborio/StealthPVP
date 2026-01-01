using UnityEngine;

/// <summary>
/// Describes a single application of damage so receivers can react with context.
/// </summary>
public struct DamagePayload
{
    public float Amount;
    public Vector3 HitPoint;
    public Vector3 HitNormal;
    public GameObject Source;
    public GameObject Instigator;
    public Collider HitCollider;
    public Vector3 ImpulseDirection;
    public float ImpulseStrength;
}

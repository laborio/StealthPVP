/// <summary>
/// Basic contract for things that can take damage.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// True when the target should ignore further damage (e.g., dead).
    /// </summary>
    bool IsDead { get; }

    void ApplyDamage(DamagePayload payload);
}

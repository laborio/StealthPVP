using System;

public static class TargetableEvents
{
    public static event Action<Targetable> OnTargetDeath;

    public static void RaiseTargetDeath(Targetable target)
    {
        OnTargetDeath?.Invoke(target);
    }
}

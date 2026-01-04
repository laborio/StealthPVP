public enum ContextActionHintType
{
    Default,
    Pickup
}

public interface IContextualActionHint
{
    ContextActionHintType HintType { get; }
}

using System;

namespace Terraari.Common.StateMachine;

public struct TransitionCondition(Func<bool> predicate)
{
    public required Func<bool> Predicate = predicate;
    public bool IsMet => Predicate();
}

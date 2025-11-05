using System.Linq;

namespace Terraari.Common.StateMachine;

public struct Transition<TContext>
    where TContext : class
{
    public IState<TContext> To;
    public TransitionCondition[] Conditions;

    public bool ShouldTransition => Conditions.All(condition => condition.IsMet);
}

using System.Linq;
using Terraria;

namespace Terraari.Common.StateMachine;

public struct Transition<TContext>
    where TContext : class
{
    public IState<TContext> To;
    public Condition[] Conditions;

    public bool ShouldTransition()
    {
        return Conditions.All(condition => condition.IsMet());
    }
}

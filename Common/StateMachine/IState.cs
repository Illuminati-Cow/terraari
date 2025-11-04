using System.Collections.Generic;
using System.Linq;

namespace Terraari.Common.StateMachine;

/// <summary>
/// Represents a state in a state machine.
/// </summary>
public interface IState<TContext>
    where TContext : class
{
    List<Transition<TContext>> Transitions { get; }

    /// <summary>
    /// Perform the actions required for entering the current state.
    /// </summary>
    /// <param name="from">The state from which the transition is occurring.</param>
    public void Enter(IState<TContext> from);

    /// <summary>
    /// Perform the actions required for exiting the current state.
    /// </summary>
    /// <param name="to">The state to which the transition is occurring.</param>
    public void Exit(IState<TContext> to);

    /// <summary>
    /// Perform the actions of this behavior.
    /// </summary>
    public void Tick(TContext context);

    /// <summary>
    /// Determines if a state transition should occur based on the defined conditions.
    /// </summary>
    /// <returns>Returns the target state if a valid transition is found, otherwise null.</returns>
    public IState<TContext> ShouldTransition()
    {
        return Transitions.FirstOrDefault(transition => transition.ShouldTransition()).To;
    }
}

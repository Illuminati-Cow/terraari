using System;
using System.Collections.Generic;

namespace Terraari.Common.StateMachine;

public class StateMachine<TContext>
    where TContext : class
{
    public List<IState<TContext>> States { get; } = [];

    public IState<TContext> CurrentState { get; private set; }
    public IState<TContext> PreviousState { get; private set; }

    public StateMachine(ICollection<IState<TContext>> states, int startingStateIndex = 0)
    {
        States.AddRange(states);
        CurrentState = States[startingStateIndex];
    }

    /// <summary>
    /// Executes the current state's logic and then evaluates state transitions.
    /// </summary>
    /// <param name="context">The context used to execute the current state's logic and evaluate transitions.</param>
    public void Tick(TContext context)
    {
        CurrentState.Tick(context);
        IState<TContext> newState = CurrentState.ShouldTransition();
        if (newState == null)
            return;
        if (newState == CurrentState)
        {
            Console.WriteLine("State machine tried to transition to itself!");
            return;
        }
        Transition(newState, context);
    }

    private void Transition(IState<TContext> to, TContext context)
    {
        PreviousState = CurrentState;
        CurrentState?.Exit(to, context);
        CurrentState = to;
        CurrentState.Enter(PreviousState, context);
    }

    /// <summary>
    /// Retrieves the serialized representation of the current state in the state machine.
    /// </summary>
    /// <returns>An integer representing the index of the current state within the list of states.</returns>
    public int GetSerializedState()
    {
        return States.IndexOf(CurrentState);
    }

    /// <summary>
    /// Retrieves the serialized representation of the specified state in the state machine.
    /// </summary>
    /// <param name="state">The state for which the serialized representation is being retrieved.</param>
    /// <returns>An integer representing the index of the specified state within the list of states.</returns>
    public int GetSerializedState(IState<TContext> state)
    {
        return States.IndexOf(state);
    }
}

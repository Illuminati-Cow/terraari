using System;
using System.Collections.Generic;

namespace Terraari.Common.StateMachine;

public class StateMachine<TContext>
    where TContext : class
{
    public List<IState<TContext>> States { get; } = [];

    private IState<TContext> currentState;
    public IState<TContext> CurrentState
    {
        get => currentState;
        private set
        {
            currentState?.Exit(value);
            currentState = value;
            currentState?.Enter(PreviousState);
            PreviousState = value;
        }
    }
    public IState<TContext> PreviousState { get; private set; }

    public StateMachine(ICollection<IState<TContext>> states, int startingStateIndex = 0)
    {
        States.AddRange(states);
        CurrentState = States[startingStateIndex];
    }

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
        CurrentState = newState;
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

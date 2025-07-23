using System.Collections.Generic;
using UnityEngine;

public class State
{
    protected FSM m_fsm;
    public State(FSM fsm)
    {
        m_fsm = fsm;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }
}

public class FSM
{
    protected Dictionary<int, State> m_states = new();
    protected State m_currentState;

    public FSM() { }

    public void Add(int key, State state)
    {
        m_states.Add(key, state);
    }

    public State GetState(int key)
    {
        return m_states[key];
    }

    public void SetCurrentState(State state)
    {
        if (m_currentState != null)
        {
            Debug.Log("Exiting state: " + m_currentState);
            m_currentState.Exit();
        }

        m_currentState = state;

        if (m_currentState != null)
        {
            Debug.Log("Entering state: " + m_currentState);
            m_currentState.Enter();
        }
    }

    public void Update()
    {
        m_currentState?.Update();
    }

    public void FixedUpdate()
    {
        m_currentState?.FixedUpdate();
    }
}

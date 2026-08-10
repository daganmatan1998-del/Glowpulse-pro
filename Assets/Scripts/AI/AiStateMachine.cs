using System;
using UnityEngine;

namespace Glowpulse.AI
{
    /// <summary>
    /// One behaviour in a state machine. States are stateless singletons: all
    /// per-character data lives on the owner, so one instance of each state is
    /// shared by every character of that type and the AI allocates nothing at
    /// runtime.
    /// </summary>
    public abstract class AiState<T> where T : class
    {
        public abstract string Name { get; }

        public virtual void Enter(T owner) { }

        public virtual void Tick(T owner, float deltaTime) { }

        public virtual void Exit(T owner) { }
    }

    /// <summary>
    /// A small state machine. Transitions are requested from inside a state and
    /// applied at the end of the tick, so a state never has the ground pulled out
    /// from under it mid-update.
    /// </summary>
    public sealed class AiStateMachine<T> where T : class
    {
        private readonly T _owner;
        private AiState<T> _current;
        private AiState<T> _pending;
        private float _enteredAt;

        /// <summary>Raised on every transition, with the previous and new state names.</summary>
        public event Action<string, string> Changed;

        public AiStateMachine(T owner)
        {
            _owner = owner;
        }

        public AiState<T> Current => _current;

        public string CurrentName => _current != null ? _current.Name : "none";

        /// <summary>Seconds spent in the current state.</summary>
        public float TimeInState => _current == null ? 0f : Time.time - _enteredAt;

        public bool Is(AiState<T> state) => ReferenceEquals(_current, state);

        /// <summary>Queues a transition. It takes effect at the end of the current tick.</summary>
        public void Change(AiState<T> next)
        {
            if (next == null || ReferenceEquals(next, _current)) return;
            _pending = next;
        }

        /// <summary>Transitions immediately, without waiting for the tick to finish.</summary>
        public void ChangeNow(AiState<T> next)
        {
            if (next == null || ReferenceEquals(next, _current)) return;

            string from = CurrentName;
            _current?.Exit(_owner);
            _current = next;
            _pending = null;
            _enteredAt = Time.time;
            _current.Enter(_owner);
            Changed?.Invoke(from, _current.Name);
        }

        public void Tick(float deltaTime)
        {
            _current?.Tick(_owner, deltaTime);

            if (_pending == null) return;
            AiState<T> next = _pending;
            _pending = null;
            ChangeNow(next);
        }
    }
}

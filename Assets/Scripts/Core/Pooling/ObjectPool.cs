using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.Core.Pooling
{
    /// <summary>
    /// Reuses GameObjects instead of instantiating and destroying them.
    /// Combat spawns effects constantly; doing that with Instantiate/Destroy
    /// produces a steady stream of garbage and the frame hitches that come with
    /// it, which is exactly what ruins the feel of a fast fighting game.
    /// </summary>
    public sealed class ObjectPool
    {
        private readonly Stack<GameObject> _idle;
        private readonly List<GameObject> _live = new List<GameObject>(32);
        private readonly GameObject _prototype;
        private readonly Transform _parent;
        private readonly int _maxSize;

        public int IdleCount => _idle.Count;
        public int LiveCount => _live.Count;

        /// <summary>
        /// Wraps a prototype object. The prototype is deactivated and kept as the
        /// template; it is never handed out itself.
        /// </summary>
        public ObjectPool(GameObject prototype, Transform parent, int prewarm = 4, int maxSize = 64)
        {
            _prototype = prototype;
            _parent = parent;
            _maxSize = Mathf.Max(1, maxSize);
            _idle = new Stack<GameObject>(prewarm);

            _prototype.SetActive(false);
            if (parent != null) _prototype.transform.SetParent(parent, false);

            for (int i = 0; i < prewarm; i++) _idle.Push(CreateInstance());
        }

        public GameObject Rent(Vector3 position, Quaternion rotation)
        {
            GameObject go = _idle.Count > 0 ? _idle.Pop() : CreateInstance();

            // A pooled object can be destroyed out from under us on scene teardown.
            if (go == null) go = CreateInstance();

            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            _live.Add(go);
            return go;
        }

        public void Return(GameObject go)
        {
            if (go == null) return;

            _live.Remove(go);
            go.SetActive(false);

            if (_idle.Count >= _maxSize)
            {
                Object.Destroy(go);
                return;
            }

            if (_parent != null) go.transform.SetParent(_parent, false);
            _idle.Push(go);
        }

        /// <summary>Returns everything currently in use. Called between encounters.</summary>
        public void ReturnAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Return(_live[i]);
        }

        private GameObject CreateInstance()
        {
            GameObject go = Object.Instantiate(_prototype, _parent);
            go.name = _prototype.name;
            go.SetActive(false);
            return go;
        }
    }
}

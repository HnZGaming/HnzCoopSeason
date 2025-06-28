using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.ModAPI;

namespace HnzUtils
{
    public sealed class SceneEntityObserver<T> where T : class, IMyEntity
    {
        public event Action<T> OnEntityAdded;
        public event Action<T> OnEntityRemoved;

        public void Load()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAddCallback;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemoveCallback;
        }

        public void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAddCallback;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemoveCallback;
        }

        public void EnumerateScene()
        {
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            foreach (var e in entities)
            {
                OnEntityAddCallback(e);
            }
        }

        void OnEntityAddCallback(IMyEntity obj)
        {
            var e = obj as T;
            if (e != null)
            {
                OnEntityAdded?.Invoke(e);
            }
        }

        void OnEntityRemoveCallback(IMyEntity obj)
        {
            var e = obj as T;
            if (e != null)
            {
                OnEntityRemoved?.Invoke(e);
            }
        }
    }
}
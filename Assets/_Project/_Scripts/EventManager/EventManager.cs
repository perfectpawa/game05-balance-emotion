using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EventManager
{
    public static class GenericEventManager
    {
        private static readonly Dictionary<string, object> _events = new();
        
        public static void StartListening<T>(string eventName, UnityAction<T> callback)
        {
            if (_events.TryGetValue(eventName, out var existEvent))
            {
                if(existEvent is UnityEvent<T> unityEvent)
                {
                    unityEvent.AddListener(callback);
                }
                else
                {
                    Debug.LogError($"Event \"{eventName}\" is already registered with a different type, but not \"{typeof(T)}\"");
                }
            }
            else
            {
                var newEvent = new UnityEvent<T>();
                newEvent.AddListener(callback);
                _events.Add(eventName, newEvent);
            }
        }
        
        public static void StopListening<T>(string eventName, UnityAction<T> callback)
        {
            if (!_events.TryGetValue(eventName, out var existingEvent) ||
                existingEvent is not UnityEvent<T> unityEvent) return;
            
            unityEvent.RemoveListener(callback);
                
            if (unityEvent.GetPersistentEventCount() == 0)
            {
                _events.Remove(eventName);
            }
        }
        
        public static void EmitEvent<T>(string eventName, T data)
        {
            if (!_events.TryGetValue(eventName, out var existingEvent))
            {
                Debug.LogError($"EventManager: \"{eventName}\" not found.");
                return;
            }
            
            if (existingEvent is UnityEvent<T> unityEvent)
            {
                unityEvent.Invoke(data);
            }
            else
            {
                Debug.LogError($"EventManager: Event '{eventName}' has different parameter type.");
            }
        }
        
        public static void StartListening(string eventName, UnityAction callback)
        {
            if (_events.TryGetValue(eventName, out var existEvent))
            {
                if(existEvent is UnityEvent unityEvent)
                {
                    unityEvent.AddListener(callback);
                }
                else
                {
                    Debug.LogError($"Event \"{eventName}\" is already registered without data props.");
                }
            }
            else
            {
                var newEvent = new UnityEvent();
                newEvent.AddListener(callback);
                _events.Add(eventName, newEvent);
            }
        }
        
        public static void StopListening(string eventName, UnityAction callback)
        {
            if (!_events.TryGetValue(eventName, out var existingEvent) ||
                existingEvent is not UnityEvent unityEvent) return;
            
            unityEvent.RemoveListener(callback);
                
            if (unityEvent.GetPersistentEventCount() == 0)
            {
                _events.Remove(eventName);
            }
        }
        
        public static void EmitEvent(string eventName)
        {
            if (!_events.TryGetValue(eventName, out var existingEvent))
            {
                Debug.LogError($"EventManager: \"{eventName}\" not found.");
                return;
            }
            
            if (existingEvent is UnityEvent unityEvent)
            {
                unityEvent.Invoke();
            }
            else
            {
                Debug.LogError($"EventManager: Event '{eventName}' is registered with no data props.");
            }
        }
        
        public static void Clear()
        {
            _events.Clear();
        }
    }
}
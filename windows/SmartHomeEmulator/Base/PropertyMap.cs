// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Generic typed property value. Equivalent to Android's PropertyValue&lt;T&gt;.
    /// </summary>
    public sealed class PropertyValue
    {
        public string Name  { get; }
        public object Value { get; }

        public PropertyValue(string name, object value)
        {
            Name  = name;
            Value = value;
        }

        public T GetValue<T>()
        {
            if (Value is T typed) return typed;
            // Try numeric conversions common in the KS protocol
            try { return (T)Convert.ChangeType(Value, typeof(T)); }
            catch { return default(T); }
        }

        public override string ToString() => $"PropertyValue{{name={Name}, value={Value}}}";

        public override bool Equals(object obj)
        {
            if (!(obj is PropertyValue other)) return false;
            return Name == other.Name && Equals(Value, other.Value);
        }

        public override int GetHashCode() => HashCode(Name, Value);

        private static int HashCode(params object[] items)
        {
            int h = 17;
            foreach (var item in items) h = h * 31 + (item?.GetHashCode() ?? 0);
            return h;
        }
    }

    /// <summary>
    /// A simple key→PropertyValue dictionary. Equivalent to Android's PropertyMap/BasicPropertyMap.
    /// </summary>
    public class PropertyMap
    {
        protected readonly Dictionary<string, PropertyValue> _map
            = new Dictionary<string, PropertyValue>();

        public void Put(string name, object value)
        {
            _map[name] = new PropertyValue(name, value);
        }

        public void Put(PropertyValue pv)
        {
            _map[pv.Name] = pv;
        }

        public PropertyValue Get(string name)
        {
            _map.TryGetValue(name, out var v);
            return v;
        }

        public T Get<T>(string name)
        {
            if (_map.TryGetValue(name, out var v))
                return v.GetValue<T>();
            return default(T);
        }

        public T Get<T>(string name, T defaultValue)
        {
            if (_map.TryGetValue(name, out var v))
                return v.GetValue<T>();
            return defaultValue;
        }

        public bool ContainsKey(string name) => _map.ContainsKey(name);

        public IEnumerable<PropertyValue> GetAll()
        {
            return _map.Values;
        }

        public void PutAll(IEnumerable<PropertyValue> values)
        {
            foreach (var v in values) Put(v);
        }

        public virtual void Clear()
        {
            _map.Clear();
        }

        public PropertyMap Clone()
        {
            var clone = new PropertyMap();
            clone.PutAll(GetAll());
            return clone;
        }
    }

    /// <summary>Read-only wrapper around a PropertyMap.</summary>
    public class ReadOnlyPropertyMap : PropertyMap
    {
        private readonly PropertyMap _inner;
        public ReadOnlyPropertyMap(PropertyMap inner) { _inner = inner; }

        public new T Get<T>(string name) => _inner.Get<T>(name);
        public new T Get<T>(string name, T defaultValue) => _inner.Get<T>(name, defaultValue);
        public new PropertyValue Get(string name) => _inner.Get(name);
    }

    /// <summary>
    /// A two-stage property map: changes are staged in a "pending" layer and
    /// committed (applied to the backing map + change notification) when Commit() is called.
    /// Equivalent to Android's StageablePropertyMap.
    /// </summary>
    public class StageablePropertyMap : PropertyMap
    {
        private readonly PropertyMap _staged = new PropertyMap();
        public event Action<IList<PropertyValue>> OnCommit;

        public new void Put(string name, object value)
        {
            _staged.Put(name, value);
        }

        public void Commit()
        {
            var changed = new List<PropertyValue>();
            foreach (var pv in _staged.GetAll())
            {
                var existing = Get(pv.Name);
                if (existing == null || !existing.Equals(pv))
                {
                    base.Put(pv);
                    changed.Add(pv);
                }
            }
            // Clear staged
            _staged.Clear();

            if (changed.Count > 0)
                OnCommit?.Invoke(changed);
        }
    }
}

// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Abstract device context – owns the RX/TX property maps, child management,
    /// and the device object it wraps.
    /// Equivalent to Android's DeviceContextBase.
    /// </summary>
    public abstract class DeviceContextBase : DeviceStatePollee
    {
        public const int PARSE_ERROR_MALFORMED_PACKET = -2;
        public const int PARSE_ERROR_UNKNOWN          = -1;
        public const int PARSE_OK_NONE                =  0;
        public const int PARSE_OK_PEER_DETECTED       =  1;
        public const int PARSE_OK_STATE_UPDATED       =  2;
        public const int PARSE_OK_ACTION_PERFORMED    =  3;
        public const int PARSE_OK_ERROR_RECEIVED      =  4;

        protected readonly PropertyMap         _baseProps  = new PropertyMap();
        protected readonly StageablePropertyMap _rxProps   = new StageablePropertyMap();
        protected readonly PropertyMap         _reqProps   = new PropertyMap();

        private readonly List<DeviceContextBase> _children = new List<DeviceContextBase>();
        private readonly object _childLock = new object();

        private HomeDevice _device;

        protected DeviceContextBase(Dictionary<string, object> defaultProps)
        {
            if (defaultProps != null)
            {
                foreach (var kv in defaultProps)
                    _baseProps.Put(kv.Key, kv.Value);
            }
            _rxProps.OnCommit += OnRxCommit;
        }

        public void SetDevice(HomeDevice device) { _device = device; }
        public HomeDevice GetDevice() => _device;

        public PropertyMap GetReadPropertyMap()
        {
            // Merge base + rx
            var merged = _baseProps.Clone();
            foreach (var pv in _rxProps.GetAll()) merged.Put(pv);
            return merged;
        }

        public abstract HomeAddress GetAddress();
        public abstract HomeAddress CreateAddress(string addr);

        // ---- Packet parsing entry point ----
        public abstract int ParsePacket(IHomePacket packet);

        // ---- Stream attach/detach ----
        public virtual void OnAttachedToStream()  { RequestUpdate(); }
        public virtual void OnDetachedFromStream() { }

        // ---- DeviceStatePollee ----
        public override void RequestUpdate(PropertyMap props = null)
        {
            // Derived classes implement actual packet sending
        }

        public override long GetUpdateTime() => _lastUpdateMs;

        // ---- Property change notifications ----
        private void OnRxCommit(IList<PropertyValue> changed)
        {
            _lastUpdateMs = (long)Environment.TickCount & 0x7FFFFFFF;
            _device?.NotifyPropertyChanged(changed);
        }

        protected void CommitPropertyChanges(StageablePropertyMap map)
        {
            map.Commit();
        }

        protected void OnErrorOccurred(int error)
        {
            _device?.NotifyErrorOccurred(error);
        }

        // ---- Children ----
        public void AddChild(DeviceContextBase child)
        {
            lock (_childLock) { if (!_children.Contains(child)) _children.Add(child); }
        }

        public void RemoveChild(DeviceContextBase child)
        {
            lock (_childLock) { _children.Remove(child); }
        }

        public void RemoveAllChildren()
        {
            lock (_childLock) { _children.Clear(); }
        }

        public List<DeviceContextBase> GetChildren()
        {
            lock (_childLock) { return new List<DeviceContextBase>(_children); }
        }

        public List<T> GetChildren<T>() where T : DeviceContextBase
        {
            lock (_childLock)
            {
                var result = new List<T>();
                foreach (var c in _children)
                    if (c is T t) result.Add(t);
                return result;
            }
        }

        public T GetChildAt<T>(int index) where T : DeviceContextBase
        {
            lock (_childLock)
            {
                int i = 0;
                foreach (var c in _children)
                {
                    if (c is T t)
                    {
                        if (i == index) return t;
                        i++;
                    }
                }
                return null;
            }
        }

        public bool IsMaster() => !_baseProps.Get<bool>(HomeDevice.PROP_IS_SLAVE);
        public bool IsSlave()  => _baseProps.Get<bool>(HomeDevice.PROP_IS_SLAVE);
    }
}

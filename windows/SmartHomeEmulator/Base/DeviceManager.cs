// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Manages a set of HomeDevices keyed by address.
    /// Equivalent to Android's DeviceManager.
    /// </summary>
    public class DeviceManager
    {
        private readonly Dictionary<string, HomeDevice> _devices
            = new Dictionary<string, HomeDevice>();
        private readonly object _lock = new object();

        public virtual bool AddDevice(HomeDevice device)
        {
            if (device == null) return false;
            string addr = device.GetAddress();
            lock (_lock)
            {
                if (_devices.ContainsKey(addr)) return false;
                _devices[addr] = device;
                return true;
            }
        }

        public virtual void RemoveDevice(HomeDevice device)
        {
            if (device == null) return;
            lock (_lock) { _devices.Remove(device.GetAddress()); }
        }

        public HomeDevice GetDevice(string address)
        {
            lock (_lock)
            {
                _devices.TryGetValue(address, out var d);
                return d;
            }
        }

        public bool ContainsDevice(HomeDevice device)
        {
            if (device == null) return false;
            lock (_lock) { return _devices.ContainsKey(device.GetAddress()); }
        }

        public virtual List<HomeDevice> GetAllDevices()
        {
            lock (_lock) { return new List<HomeDevice>(_devices.Values); }
        }

        public virtual void ClearAllDevices()
        {
            lock (_lock) { _devices.Clear(); }
        }
    }
}

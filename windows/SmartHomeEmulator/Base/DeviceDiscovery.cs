// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Discovers devices by sending ping packets and listening for responses.
    /// Equivalent to Android's DeviceDiscovery.
    /// </summary>
    public class DeviceDiscovery
    {
        public delegate void DeviceFoundHandler(HomeAddress address, IHomePacket packet);
        public event DeviceFoundHandler DeviceFound;

        protected HomeAddress _lastPollAddress;
        private bool _running;

        public virtual bool IsRunning() => _running;

        public void Start(List<HomeDevice> candidates)
        {
            _running = true;
            _lastPollAddress = null;
        }

        public void Stop()
        {
            _running = false;
        }

        protected virtual void PingDevice(HomeDevice device)
        {
            // Override in derived class to send a characteristic request packet.
        }

        public void OnParsePacket(HomeAddress address, IHomePacket packet)
        {
            _lastPollAddress = address;
            DeviceFound?.Invoke(address, packet);
        }
    }
}

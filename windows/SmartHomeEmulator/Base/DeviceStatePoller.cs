// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Tracks polling phases for a device (NAPPING / WORKING).
    /// Equivalent to Android's DeviceStatePollee.
    /// </summary>
    public abstract class DeviceStatePollee
    {
        public static class Phase
        {
            public const int NAPPING = 0;
            public const int WORKING = 1;
        }

        protected int  _pollPhase    = Phase.NAPPING;
        protected long _pollInterval = 5000; // ms
        protected long _lastUpdateMs = 0;

        public abstract void RequestUpdate(PropertyMap props = null);

        public virtual void SetPollPhase(int phase, long intervalMs)
        {
            _pollPhase    = phase;
            _pollInterval = intervalMs;
        }

        public virtual long GetUpdateTime() => _lastUpdateMs;
    }

    /// <summary>
    /// Periodically polls a list of DeviceStatePollee objects.
    /// Equivalent to Android's DeviceStatePoller.
    /// </summary>
    public class DeviceStatePoller
    {
        private const long DefaultIntervalMs = 5000;

        private readonly object _lock = new object();
        private List<DeviceStatePollee> _pollees = new List<DeviceStatePollee>();
        private System.Threading.Timer  _timer;
        private bool _repeative;

        public void Start(bool repeative, IList<DeviceStatePollee> pollees)
        {
            lock (_lock)
            {
                _repeative = repeative;
                _pollees   = new List<DeviceStatePollee>(pollees);
                _timer?.Dispose();
                _timer = new System.Threading.Timer(OnTick, null,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(DefaultIntervalMs));
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        public void AddPollee(DeviceStatePollee pollee)
        {
            lock (_lock) { if (!_pollees.Contains(pollee)) _pollees.Add(pollee); }
        }

        public void RemovePollee(DeviceStatePollee pollee)
        {
            lock (_lock) { _pollees.Remove(pollee); }
        }

        private void OnTick(object state)
        {
            List<DeviceStatePollee> snapshot;
            lock (_lock) { snapshot = new List<DeviceStatePollee>(_pollees); }

            long now = (long)Environment.TickCount & 0x7FFFFFFF;
            foreach (var pollee in snapshot)
            {
                long elapsed = now - pollee.GetUpdateTime();
                int  phase   = elapsed > DefaultIntervalMs * 3
                    ? DeviceStatePollee.Phase.NAPPING
                    : DeviceStatePollee.Phase.WORKING;

                pollee.SetPollPhase(phase, DefaultIntervalMs);
                pollee.RequestUpdate();
            }
        }
    }
}

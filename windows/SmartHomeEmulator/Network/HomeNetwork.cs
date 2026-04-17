// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

using SmartHomeEmulator.Base;
using SmartHomeEmulator.Session;
using SmartHomeEmulator.Streams;
using SmartHomeEmulator.Protocol;

namespace SmartHomeEmulator.Network
{
    /// <summary>
    /// Top-level orchestrator: holds the MainContext + StreamProcessor,
    /// starts/stops the emulation session.
    /// Equivalent to Android's HomeNetwork.
    /// </summary>
    public class HomeNetwork
    {
        private const string Tag = "HomeNetwork";

        public enum State { Stopped, Starting, Running, Error }

        public delegate void StateChangedHandler(State newState);
        public event StateChangedHandler StateChanged;

        private readonly KDMainContext   _mainContext;
        private StreamProcessor          _streamProcessor;
        private volatile State           _state = State.Stopped;

        public HomeNetwork()
        {
            _mainContext = new KDMainContext();
        }

        public State CurrentState => _state;

        public KDMainContext MainContext => _mainContext;

        /// <summary>Start emulation over the provided session (COM port).</summary>
        public bool Start(INetworkSession session)
        {
            if (_state == State.Running || _state == State.Starting)
            {
                Debug.WriteLine($"[{Tag}] Already running, ignoring Start()");
                return false;
            }

            SetState(State.Starting);

            _streamProcessor = new StreamProcessor(OnStreamError);

            bool started = _streamProcessor.StartStream(session);
            if (!started)
            {
                Debug.WriteLine($"[{Tag}] Failed to start stream");
                SetState(State.Error);
                return false;
            }

            _mainContext.AttachStream(_streamProcessor);
            SetState(State.Running);

            Debug.WriteLine($"[{Tag}] Started");
            return true;
        }

        /// <summary>Stop emulation.</summary>
        public void Stop()
        {
            if (_state == State.Stopped) return;

            _mainContext.DetachStream();
            _streamProcessor?.StopStream();
            _streamProcessor = null;

            SetState(State.Stopped);
            Debug.WriteLine($"[{Tag}] Stopped");
        }

        // ---- Device management ----

        public bool AddDevice(Dictionary<string, object> props)
        {
            var device = _mainContext.CreateDevice(props);
            if (device == null) return false;
            return _mainContext.AddDevice(device);
        }

        public void RemoveDevice(HomeDevice device)
        {
            _mainContext.RemoveDevice(device);
        }

        public List<HomeDevice> GetAllDevices()
        {
            return _mainContext.GetAllDevices();
        }

        // ---- Error handling ----

        private void OnStreamError()
        {
            Debug.WriteLine($"[{Tag}] Stream error occurred");
            SetState(State.Error);
        }

        private void SetState(State newState)
        {
            _state = newState;
            try { StateChanged?.Invoke(newState); }
            catch (Exception ex) { Debug.WriteLine($"[{Tag}] StateChanged error: {ex.Message}"); }
        }
    }
}

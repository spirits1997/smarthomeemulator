// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using SmartHomeEmulator.Streams;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Manages device contexts, routes received packets to them, and interfaces with StreamProcessor.
    /// Equivalent to Android's MainContext.
    /// </summary>
    public abstract class MainContext : DeviceManager, StreamProcessor.IClient
    {
        private const string Tag = "MainContext";
        private const int RxBufferSize = 1024;

        protected StreamProcessor _streamProcessor;

        // RX byte accumulation buffer
        private readonly byte[] _rxBuffer    = new byte[RxBufferSize];
        private int              _rxCount     = 0;
        private readonly object  _rxLock      = new object();
        private Timer            _clearTimer;

        protected MainContext() { }

        public void AttachStream(StreamProcessor sp)
        {
            lock (_rxLock) { _rxCount = 0; }

            _streamProcessor = sp;
            _streamProcessor.AddClient(this);

            foreach (var device in GetAllDevices())
                device.Dc.OnAttachedToStream();
        }

        public void DetachStream()
        {
            _clearTimer?.Dispose();
            _clearTimer = null;

            if (_streamProcessor != null)
            {
                foreach (var device in GetAllDevices())
                    device.Dc.OnDetachedFromStream();

                _streamProcessor.RemoveClient(this);
                _streamProcessor = null;
            }

            lock (_rxLock) { _rxCount = 0; }
        }

        // ---- StreamProcessor.IClient ----

        public void ProcessPacket(byte[] data, int length)
        {
            lock (_rxLock)
            {
                if (length > RxBufferSize - _rxCount)
                {
                    Debug.WriteLine($"[{Tag}] RX buffer overflow, clearing");
                    _rxCount = 0;
                }
                Array.Copy(data, 0, _rxBuffer, _rxCount, length);
                _rxCount += length;

                // Process all complete packets
                ProcessBuffer();
            }
        }

        private void ProcessBuffer()
        {
            int pos = 0;
            while (pos < _rxCount)
            {
                int remaining = _rxCount - pos;
                try
                {
                    int consumed = ParsePacket(_rxBuffer, pos, remaining);
                    if (consumed <= 0) break;  // need more data
                    pos += consumed;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{Tag}] parse error: {ex.Message}");
                    pos++;  // skip one byte and try again
                }
            }

            // Compact buffer
            if (pos > 0)
            {
                _rxCount -= pos;
                if (_rxCount > 0)
                    Array.Copy(_rxBuffer, pos, _rxBuffer, 0, _rxCount);
            }
        }

        // ---- Device management overrides ----

        public override bool AddDevice(HomeDevice device)
        {
            bool added = base.AddDevice(device);
            if (added && _streamProcessor != null)
                device.Dc.OnAttachedToStream();
            return added;
        }

        public override void RemoveDevice(HomeDevice device)
        {
            base.RemoveDevice(device);
            if (_streamProcessor != null)
                device.Dc.OnDetachedFromStream();
        }

        // ---- Packet sending ----

        public void SendPacket(DeviceContextBase dc, byte[] packetBytes)
        {
            if (_streamProcessor != null)
            {
                LogTx(packetBytes);
                _streamProcessor.SendPacket(packetBytes);
            }
        }

        // ---- Abstract interface ----

        /// <summary>
        /// Parse one (or more) packets from the buffer starting at offset.
        /// Returns number of bytes consumed, or 0 if more data is needed.
        /// </summary>
        protected abstract int ParsePacket(byte[] buffer, int offset, int length);

        public abstract HomeDevice CreateDevice(Dictionary<string, object> defaultProps);
        public abstract DeviceDiscovery GetDeviceDiscovery();

        // ---- Logging ----

        private static void LogTx(byte[] data)
        {
            Debug.WriteLine("TX: " + BitConverter.ToString(data).Replace("-", " "));
        }
    }
}

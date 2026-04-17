// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

using SmartHomeEmulator.Session;

namespace SmartHomeEmulator.Streams
{
    /// <summary>
    /// Manages the RX/TX threads and routes received packets to registered clients.
    /// Equivalent to Android's StreamProcessor.
    /// </summary>
    public class StreamProcessor : IStreamCallback
    {
        private const string Tag = "StreamProcessor";

        public interface IClient
        {
            void ProcessPacket(byte[] data, int length);
        }

        private readonly Action       _errorRunnable;
        private readonly List<IClient> _clients = new List<IClient>();

        private INetworkSession _session;
        private StreamRxThread  _rxThread;
        private StreamTxThread  _txThread;
        private bool            _isRunning;

        public StreamProcessor(Action errorRunnable)
        {
            _errorRunnable = errorRunnable;
        }

        public void AddClient(IClient client)    { lock (_clients) { _clients.Add(client); } }
        public void RemoveClient(IClient client) { lock (_clients) { _clients.Remove(client); } }

        public bool StartStream(INetworkSession session)
        {
            _session = session;

            if (!_session.Open())
            {
                Debug.WriteLine($"[{Tag}] Can't open session!");
                return false;
            }

            var inputStream  = _session.InputStream;
            var outputStream = _session.OutputStream;

            if (inputStream == null || outputStream == null)
            {
                Debug.WriteLine($"[{Tag}] Null streams!");
                return false;
            }

            _rxThread = new StreamRxThread(inputStream,  this);
            _txThread = new StreamTxThread(outputStream, this);

            _rxThread.Start();
            _txThread.Start();

            _isRunning = true;
            Debug.WriteLine($"[{Tag}] started");
            return true;
        }

        public void StopStream()
        {
            _rxThread?.RequestStop();
            _txThread?.RequestStop();
            _rxThread = null;
            _txThread = null;

            _session?.Close();
            _session = null;

            _isRunning = false;
            Debug.WriteLine($"[{Tag}] stopped");
        }

        public bool IsRunning => _isRunning;

        public void SendPacket(byte[] data)
        {
            _txThread?.AddPacket(data);
        }

        // ---- IStreamCallback ----

        public void OnPacketReceived(byte[] data, int length)
        {
            List<IClient> snapshot;
            lock (_clients) { snapshot = new List<IClient>(_clients); }
            foreach (var client in snapshot)
                client.ProcessPacket(data, length);
        }

        public void OnErrorOccurred()
        {
            // Post error handling to be invoked on the UI thread (or calling thread)
            // The HomeNetwork will stop and retry.
            StopStream();
            _errorRunnable?.Invoke();
        }
    }
}

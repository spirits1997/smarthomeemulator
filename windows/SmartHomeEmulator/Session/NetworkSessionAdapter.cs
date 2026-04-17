// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.IO;
using System.Threading;

namespace SmartHomeEmulator.Session
{
    /// <summary>
    /// Adapter base class that bridges between the serial communication layer and the
    /// upper protocol layer via a paired in-memory stream.
    /// Equivalent to Android's NetworkSessionAdapter + SessionAdapterDelegate.
    /// </summary>
    public class NetworkSessionAdapter : INetworkSession
    {
        private const int BufferSize = 4096;
        private readonly AdapterInputStream  _inputStream;
        private readonly AdapterOutputStream _outputStream;

        public NetworkSessionAdapter()
        {
            _inputStream  = new AdapterInputStream(BufferSize);
            _outputStream = new AdapterOutputStream(this);
        }

        // ---- INetworkSession ----

        public virtual bool Open()
        {
            _inputStream.Reset();
            return OnOpen();
        }

        public virtual void Close()
        {
            OnClose();
            _inputStream.Close();
        }

        public Stream InputStream  => _inputStream;
        public Stream OutputStream => _outputStream;

        // ---- Overridable hooks ----

        protected virtual bool OnOpen()  { return true; }
        protected virtual void OnClose() { }

        /// <summary>Called by the output stream when upper layer writes bytes.</summary>
        protected internal virtual void OnWrite(byte[] data) { }

        // ---- Called by concrete subclasses when data arrives from hardware ----

        /// <summary>Push bytes received from the physical port into the read buffer.</summary>
        public void PutData(byte[] data)
        {
            _inputStream.AddData(data);
        }

        // ================================================================
        // Inner stream: readable pipe that AdapterInputStream provides
        // ================================================================
        private sealed class AdapterInputStream : Stream
        {
            private readonly byte[] _buf;
            private int _writePos;
            private int _readPos;
            private int _count;
            private readonly object _lock = new object();
            private bool _closed;

            public AdapterInputStream(int capacity)
            {
                _buf = new byte[capacity];
            }

            public void Reset()
            {
                lock (_lock)
                {
                    _writePos = _readPos = _count = 0;
                    _closed = false;
                }
            }

            public void AddData(byte[] data)
            {
                lock (_lock)
                {
                    foreach (byte b in data)
                    {
                        if (_count < _buf.Length)
                        {
                            _buf[_writePos] = b;
                            _writePos = (_writePos + 1) % _buf.Length;
                            _count++;
                        }
                        // else: buffer full, drop byte
                    }
                    Monitor.PulseAll(_lock);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                lock (_lock)
                {
                    while (_count == 0 && !_closed)
                        Monitor.Wait(_lock, 500);

                    if (_closed && _count == 0) return 0;

                    int read = Math.Min(count, _count);
                    for (int i = 0; i < read; i++)
                    {
                        buffer[offset + i] = _buf[_readPos];
                        _readPos = (_readPos + 1) % _buf.Length;
                    }
                    _count -= read;
                    return read;
                }
            }

            public override int ReadByte()
            {
                lock (_lock)
                {
                    while (_count == 0 && !_closed)
                        Monitor.Wait(_lock, 500);

                    if (_closed && _count == 0) return -1;

                    byte b = _buf[_readPos];
                    _readPos = (_readPos + 1) % _buf.Length;
                    _count--;
                    return b;
                }
            }

            public override void Close()
            {
                lock (_lock)
                {
                    _closed = true;
                    Monitor.PulseAll(_lock);
                }
                base.Close();
            }

            public override bool CanRead  => true;
            public override bool CanSeek  => false;
            public override bool CanWrite => false;
            public override long Length   => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush()  { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        // ================================================================
        // Inner stream: writable pipe that calls OnWrite on the adapter
        // ================================================================
        private sealed class AdapterOutputStream : Stream
        {
            private readonly NetworkSessionAdapter _adapter;

            public AdapterOutputStream(NetworkSessionAdapter adapter)
            {
                _adapter = adapter;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                byte[] data = new byte[count];
                Array.Copy(buffer, offset, data, 0, count);
                _adapter.OnWrite(data);
            }

            public override void WriteByte(byte value)
            {
                _adapter.OnWrite(new[] { value });
            }

            public override bool CanRead  => false;
            public override bool CanSeek  => false;
            public override bool CanWrite => true;
            public override long Length   => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush()  { }
            public override int  Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}

// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;

namespace SmartHomeEmulator.Base
{
    /// <summary>
    /// Schedule descriptor for repeating packet sends.
    /// Equivalent to Android's PacketSchedule.
    /// </summary>
    public class PacketSchedule
    {
        public byte[]  PacketData     { get; }
        public long    RepeatCount    { get; }   // 0 = infinity
        public long    RepeatInterval { get; }   // ms between repeats
        public bool    AllowSameRx    { get; }

        public Action<PacketSchedule>       ExitCallback  { get; }
        public Action<PacketSchedule, int>  ErrorCallback { get; }

        private PacketSchedule(Builder b)
        {
            PacketData     = b._packetData;
            RepeatCount    = b._repeatCount;
            RepeatInterval = b._repeatInterval;
            AllowSameRx    = b._allowSameRx;
            ExitCallback   = b._exitCallback;
            ErrorCallback  = b._errorCallback;
        }

        public class Builder
        {
            internal byte[]  _packetData;
            internal long    _repeatCount    = 1;
            internal long    _repeatInterval = 0;
            internal bool    _allowSameRx    = false;
            internal Action<PacketSchedule>      _exitCallback;
            internal Action<PacketSchedule, int> _errorCallback;

            public Builder(byte[] packetData) { _packetData = packetData; }

            public Builder SetRepeatCount(long count)        { _repeatCount    = count; return this; }
            public Builder SetRepeatInterval(long intervalMs){ _repeatInterval = intervalMs; return this; }
            public Builder SetAllowSameRx(bool allow)        { _allowSameRx    = allow; return this; }
            public Builder SetExitCallback(Action<PacketSchedule> cb)       { _exitCallback  = cb; return this; }
            public Builder SetErrorCallback(Action<PacketSchedule, int> cb) { _errorCallback = cb; return this; }

            public PacketSchedule Build() => new PacketSchedule(this);
        }
    }
}

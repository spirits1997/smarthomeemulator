// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Diagnostics;
using System.Text;

namespace SmartHomeEmulator.Protocol
{
    /// <summary>
    /// Represents a KS X 4506 device address (device-id + sub-id pair).
    /// Equivalent to Android's KSAddress.
    /// </summary>
    public class KSAddress : Base.HomeAddress
    {
        // ---- Sub-ID interpretation ----
        public struct DeviceSubId
        {
            private readonly int _value;
            public DeviceSubId(int value) { _value = value; }
            public int Value() => _value;

            // Single device: low nibble is 1..E
            public bool IsSingle()        => (_value & 0x0F) >= 1 && (_value & 0x0F) <= 0x0E && (_value & 0xF0) == 0;
            // Full-of-group: group nibble != 0, single nibble == F
            public bool IsFull()          => (_value & 0x0F) == 0x0F && (_value & 0xF0) == 0;
            // Single within a group: group nibble != 0, single nibble is 1..E
            public bool IsSingleOfGroup() => (_value & 0x0F) >= 1 && (_value & 0x0F) <= 0x0E && (_value & 0xF0) != 0;
            // Full-of-group: group nibble != 0, single nibble == F
            public bool IsFullOfGroup()   => (_value & 0x0F) == 0x0F && (_value & 0xF0) != 0;
            // hasSingle = single or single-of-group
            public bool HasSingle()       => IsSingle() || IsSingleOfGroup();
            // hasFull   = full or full-of-group
            public bool HasFull()         => IsFull() || IsFullOfGroup();
            // isAll is a broad match (0x0F means "all singles")
            public bool IsAll()           => _value == 0x0F;
            // has group info
            public bool HasGroup()        => (_value & 0xF0) != 0;

            public override string ToString() => $"0x{_value:X2}";
        }

        private readonly int _deviceId;
        private readonly DeviceSubId _subId;

        public KSAddress(int deviceId, int subId)
        {
            _deviceId = deviceId;
            _subId    = new DeviceSubId(subId);
        }

        public KSAddress(string addressStr)
        {
            // Format: "XX.YY" (hex device-id . hex sub-id)
            var parts = addressStr.Split('.');
            if (parts.Length >= 2)
            {
                _deviceId = Convert.ToInt32(parts[0], 16);
                _subId    = new DeviceSubId(Convert.ToInt32(parts[1], 16));
            }
        }

        public int         GetDeviceId()  => _deviceId;
        public DeviceSubId GetDeviceSubId() => _subId;

        public override string DeviceAddress => ToAddressString();

        public string ToAddressString() => $"{_deviceId:X2}.{_subId.Value():X2}";

        public static DeviceSubId ToDeviceSubId(int raw) => new DeviceSubId(raw);

        public override string ToString() => ToAddressString();
        public override bool Equals(object obj)
        {
            if (!(obj is KSAddress other)) return false;
            return _deviceId == other._deviceId && _subId.Value() == other._subId.Value();
        }
        public override int GetHashCode() => _deviceId * 256 + _subId.Value();
    }
}

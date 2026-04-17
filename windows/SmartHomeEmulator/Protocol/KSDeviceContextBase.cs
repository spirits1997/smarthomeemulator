// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;

using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    /// <summary>
    /// Base class for all KS X 4506 device contexts.
    /// Equivalent to Android's KSDeviceContextBase.
    /// </summary>
    public abstract class KSDeviceContextBase : DeviceContextBase
    {
        // ---- Command codes ----
        public const int CMD_STATUS_REQ          = 0x01;
        public const int CMD_STATUS_RSP          = 0x81;
        public const int CMD_CHARACTERISTIC_REQ  = 0x0F;
        public const int CMD_CHARACTERISTIC_RSP  = 0x8F;
        public const int CMD_SINGLE_CONTROL_REQ  = 0x41;
        public const int CMD_SINGLE_CONTROL_RSP  = 0xC1;
        public const int CMD_GROUP_CONTROL_REQ   = 0x42;

        // ---- Capability flags ----
        public const int CAP_STATUS_SINGLE  = 1 << 0;
        public const int CAP_STATUS_MULTI   = 1 << 1;
        public const int CAP_CHARAC_SINGLE  = 1 << 2;
        public const int CAP_CHARAC_MULTI   = 1 << 3;

        protected readonly KSMainContext _mainContext;
        private bool _characteristicRetrieved;

        protected KSDeviceContextBase(KSMainContext mainContext, Dictionary<string, object> defaultProps)
            : base(defaultProps)
        {
            _mainContext = mainContext;
        }

        protected virtual int GetCapabilities()
            => CAP_STATUS_SINGLE | CAP_STATUS_MULTI | CAP_CHARAC_SINGLE | CAP_CHARAC_MULTI;

        protected bool IsCapableOf(int caps) => (GetCapabilities() & caps) == caps;

        public int GetDeviceId() => ((KSAddress)GetAddress()).GetDeviceId();
        public KSAddress.DeviceSubId GetDeviceSubId() => ((KSAddress)GetAddress()).GetDeviceSubId();

        public bool IsDetected() => _characteristicRetrieved;

        protected bool IsSingleDevice()
        {
            var subId = GetDeviceSubId();
            return subId.IsSingle() || subId.IsSingleOfGroup();
        }

        // ---- HomeAddress ----

        public override HomeAddress GetAddress()
        {
            string addr = _baseProps.Get<string>(HomeDevice.PROP_ADDR) ?? "00.01";
            return new KSAddress(addr);
        }

        public override HomeAddress CreateAddress(string addr) => new KSAddress(addr);

        // ---- Parse entry point ----

        public override int ParsePacket(IHomePacket packet)
        {
            if (!(packet is KSPacket ksp)) return PARSE_OK_NONE;

            // Capability check
            if (!CheckCapabilityByPacket(ksp)) return PARSE_OK_NONE;

            // In slave mode single device skips combined packets
            if (IsSlave())
            {
                var pktSubId = KSAddress.ToDeviceSubId(ksp.DeviceSubId);
                var devSubId = GetDeviceSubId();
                if (pktSubId.HasFull() && ksp.DeviceSubId != devSubId.Value())
                    return PARSE_OK_NONE;
            }

            return ParsePayload(ksp, _rxProps);
        }

        protected virtual int ParsePayload(KSPacket packet, StageablePropertyMap outProps)
        {
            switch (packet.CommandType)
            {
                case CMD_STATUS_REQ:         return ParseStatusReq(packet, outProps);
                case CMD_STATUS_RSP:         return ParseStatusRsp(packet, outProps);
                case CMD_CHARACTERISTIC_REQ: return ParseCharacteristicReq(packet, outProps);
                case CMD_CHARACTERISTIC_RSP:
                    int res = ParseCharacteristicRsp(packet, outProps);
                    if (res >= PARSE_OK_NONE) _characteristicRetrieved = true;
                    return res;
                case CMD_SINGLE_CONTROL_REQ: return ParseSingleControlReq(packet, outProps);
                case CMD_SINGLE_CONTROL_RSP: return ParseSingleControlRsp(packet, outProps);
                default: return PARSE_OK_NONE;
            }
        }

        // ---- Overridable parse methods ----
        protected virtual int ParseStatusReq(KSPacket p, StageablePropertyMap o)        => PARSE_OK_NONE;
        protected virtual int ParseStatusRsp(KSPacket p, StageablePropertyMap o)        => PARSE_OK_NONE;
        protected virtual int ParseCharacteristicReq(KSPacket p, StageablePropertyMap o)=> PARSE_OK_NONE;
        protected virtual int ParseCharacteristicRsp(KSPacket p, StageablePropertyMap o)=> PARSE_OK_NONE;
        protected virtual int ParseSingleControlReq(KSPacket p, StageablePropertyMap o) => PARSE_OK_NONE;
        protected virtual int ParseSingleControlRsp(KSPacket p, StageablePropertyMap o) => PARSE_OK_NONE;

        // ---- Packet creation helpers ----
        protected KSPacket CreatePacket(int cmd, params byte[] data)
        {
            int devId = GetDeviceId();
            int subId = GetDeviceSubId().Value();
            return CreatePacket(devId, subId, cmd, data);
        }

        protected KSPacket CreatePacket(int devId, int subId, int cmd, byte[] data)
        {
            // Multi-status/characteristic requests target 0x?F
            int adjustedSub = subId;
            if (cmd == CMD_STATUS_REQ && IsCapableOf(CAP_STATUS_MULTI))
                adjustedSub |= 0x0F;
            if (cmd == CMD_CHARACTERISTIC_REQ && IsCapableOf(CAP_CHARAC_MULTI))
                adjustedSub |= 0x0F;

            return new KSPacket
            {
                DeviceId    = devId,
                DeviceSubId = adjustedSub,
                CommandType = cmd,
                Data        = data ?? Array.Empty<byte>(),
            };
        }

        protected void SendPacket(KSPacket packet)
        {
            if (packet != null)
                _mainContext.SendPacket(this, packet.ToBytes());
        }

        // ---- Capability checks ----
        private bool CheckCapabilityByPacket(KSPacket p)
        {
            var sub  = KSAddress.ToDeviceSubId(p.DeviceSubId);
            int cmd  = p.CommandType;
            if (cmd == CMD_STATUS_REQ || cmd == CMD_STATUS_RSP)
            {
                if (sub.HasSingle() && IsCapableOf(CAP_STATUS_SINGLE)) return true;
                if (sub.HasFull()   && IsCapableOf(CAP_STATUS_MULTI))  return true;
                return false;
            }
            if (cmd == CMD_CHARACTERISTIC_REQ || cmd == CMD_CHARACTERISTIC_RSP)
            {
                if (sub.HasSingle() && IsCapableOf(CAP_CHARAC_SINGLE)) return true;
                if (sub.HasFull()   && IsCapableOf(CAP_CHARAC_MULTI))  return true;
                return false;
            }
            return true;
        }
    }
}

// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    public static class LightProps
    {
        private const string PROP_PREFIX = "lgt.";
        public const string PROP_CUR_DIM_LEVEL = PROP_PREFIX + "dim.level";
        public const string PROP_DIM_SUPPORTED = PROP_PREFIX + "dim.supported";
        public const string PROP_MAX_DIM_LEVEL = PROP_PREFIX + "dim.max_level";
    }

    public class KSLight : KSDeviceContextBase
    {
        public const int CMD_LIGHT_ALL_CONTROL_REQ = 0x43;
        protected int _totalCountInGroup = 0;

        public KSLight(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                _rxProps.Put(HomeDevice.PROP_ONOFF, false);
                _rxProps.Put(LightProps.PROP_DIM_SUPPORTED, false);
                _rxProps.Put(LightProps.PROP_CUR_DIM_LEVEL, 0);
                _rxProps.Put(LightProps.PROP_MAX_DIM_LEVEL, 5);
                _rxProps.Commit();
            }
        }

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            var data = new List<byte>(); data.Add(0);
            MakeStatusRspData(packet, outProps, data);
            SendPacket(CreatePacket(CMD_STATUS_RSP, data.ToArray()));
            return PARSE_OK_STATE_UPDATED;
        }

        protected virtual void MakeStatusRspData(KSPacket req, StageablePropertyMap outProps, List<byte> outData)
        {
            var thisSubId = GetDeviceSubId();
            if (thisSubId.IsSingle() || thisSubId.IsSingleOfGroup())
            {
                outData.Add(MakeSingleLightStateByte(GetReadPropertyMap()));
            }
            else if (thisSubId.IsFull() || thisSubId.IsFullOfGroup())
            {
                foreach (var child in GetChildren<KSLight>())
                    outData.Add(MakeSingleLightStateByte(child.GetReadPropertyMap()));
            }
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }

            if (KSAddress.ToDeviceSubId(packet.DeviceSubId).IsSingle())
            {
                ParseSingleLightStateByte(packet.Data[1] & 0xFF, outProps);
            }
            else
            {
                var addr  = (KSAddress)GetAddress();
                int index = addr.GetDeviceSubId().Value() & 0x0F;
                if (index != 0 && index != 0x0F && index < packet.Data.Length)
                    ParseSingleLightStateByte(packet.Data[index] & 0xFF, outProps);
            }
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseCharacteristicReq(KSPacket packet, StageablePropertyMap outProps)
        {
            var data = new List<byte>(); data.Add(0);
            MakeCharacteristicRspData(packet, outProps, data);
            SendPacket(CreatePacket(CMD_CHARACTERISTIC_RSP, data.ToArray()));
            return PARSE_OK_STATE_UPDATED;
        }

        protected virtual void MakeCharacteristicRspData(KSPacket req, StageablePropertyMap outProps, List<byte> outData)
        {
            int normalCount = 0, dimmableCount = 0, dimmableFlags = 0;
            var thisSubId = GetDeviceSubId();
            if (thisSubId.IsSingle() || thisSubId.IsSingleOfGroup())
            {
                bool dim = GetReadPropertyMap().Get<bool>(LightProps.PROP_DIM_SUPPORTED);
                normalCount   = dim ? 0 : 1;
                dimmableCount = dim ? 1 : 0;
            }
            else if (thisSubId.IsFull() || thisSubId.IsFullOfGroup())
            {
                foreach (var child in GetChildren<KSLight>())
                {
                    int idx = normalCount + dimmableCount;
                    if (child.GetReadPropertyMap().Get<bool>(LightProps.PROP_DIM_SUPPORTED))
                    { dimmableFlags |= 1 << idx; dimmableCount++; }
                    else normalCount++;
                }
            }
            outData.Add((byte)normalCount);
            outData.Add((byte)dimmableCount);
            outData.Add((byte)dimmableFlags);
        }

        protected override int ParseCharacteristicRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 4) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }
            int normalCount   = packet.Data[1] & 0xFF;
            int dimmableCount = packet.Data[2] & 0xFF;
            _totalCountInGroup = normalCount + dimmableCount;
            outProps.Put(LightProps.PROP_DIM_SUPPORTED, dimmableCount > 0);
            outProps.Commit();
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseSingleControlReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;
            ParseSingleLightStateByte(packet.Data[0] & 0xFF, outProps);
            outProps.Commit();
            var data = new List<byte>(); data.Add(0);
            MakeStatusRspData(packet, outProps, data);
            SendPacket(CreatePacket(CMD_SINGLE_CONTROL_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }

        // ---- Helper encode/decode ----
        private static byte MakeSingleLightStateByte(PropertyMap props)
        {
            bool on    = props.Get<bool>(HomeDevice.PROP_ONOFF);
            int  level = props.Get<int>(LightProps.PROP_CUR_DIM_LEVEL);
            int  max   = props.Get(LightProps.PROP_MAX_DIM_LEVEL) != null
                       ? props.Get<int>(LightProps.PROP_MAX_DIM_LEVEL, 5) : 5;
            if (!on) return 0;
            if (level == 0) return 0x01;  // on, no dim
            return (byte)(0x01 | ((level & 0xF) << 1));
        }

        private static void ParseSingleLightStateByte(int state, StageablePropertyMap outProps)
        {
            bool on    = (state & 0x01) != 0;
            int  level = (state >> 1) & 0xF;
            outProps.Put(HomeDevice.PROP_ONOFF, on);
            outProps.Put(LightProps.PROP_CUR_DIM_LEVEL, level);
        }
    }
}

// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    // ---- Thermostat property names (equivalent to device/Thermostat.java) ----
    public static class ThermostatProps
    {
        private const string PROP_PREFIX = "ts.";
        public static class Function
        {
            public const long HEATING        = 1L << 0;
            public const long COOLING        = 1L << 1;
            public const long OUTING_SETTING = 1L << 2;
            public const long HOTWATER_ONLY  = 1L << 3;
            public const long RESERVED_MODE  = 1L << 4;
        }
        public const string PROP_SUPPORTED_FUNCTIONS = PROP_PREFIX + "function.supports";
        public const string PROP_FUNCTION_STATES     = PROP_PREFIX + "function.states";
        public const string PROP_MIN_TEMPERATURE     = PROP_PREFIX + "temperature.min";
        public const string PROP_MAX_TEMPERATURE     = PROP_PREFIX + "temperature.max";
        public const string PROP_TEMP_RESOLUTION     = PROP_PREFIX + "temperature.resolution";
        public const string PROP_SETTING_TEMPERATURE = PROP_PREFIX + "temperature.setting";
        public const string PROP_CURRENT_TEMPERATURE = PROP_PREFIX + "temperature.current";
    }

    /// <summary>
    /// KS X 4506 Thermostat device context.
    /// Equivalent to Android's KSThermostat.
    /// </summary>
    public class KSThermostat : KSDeviceContextBase
    {
        public const int CMD_HEATING_STATE_REQ   = 0x43;
        public const int CMD_HEATING_STATE_RSP   = 0xC3;
        public const int CMD_TEMPERATURE_REQ     = 0x44;
        public const int CMD_TEMPERATURE_RSP     = 0xC4;
        public const int CMD_OUTING_SETTING_REQ  = 0x45;
        public const int CMD_OUTING_SETTING_RSP  = 0xC5;
        public const int CMD_RESERVED_MODE_REQ   = 0x46;
        public const int CMD_RESERVED_MODE_RSP   = 0xC6;
        public const int CMD_HOTWATER_ONLY_REQ   = 0x47;
        public const int CMD_HOTWATER_ONLY_RSP   = 0xC7;

        private int   _manufacturerId = 0;
        private int   _tempDetectType = 0;
        private float _minTemperature = 0.0f;
        private float _maxTemperature = 40.0f;
        private bool  _supportHalfDegree = false;
        private int   _controllerCountInGroup = 0;

        public KSThermostat(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props)
        {
            if (IsSlave())
            {
                long supportedFunctions = 0;
                supportedFunctions |= ThermostatProps.Function.HEATING;
                supportedFunctions |= ThermostatProps.Function.OUTING_SETTING;
                supportedFunctions |= ThermostatProps.Function.HOTWATER_ONLY;
                supportedFunctions |= ThermostatProps.Function.RESERVED_MODE;
                _rxProps.Put(ThermostatProps.PROP_SUPPORTED_FUNCTIONS, supportedFunctions);
                _rxProps.Put(ThermostatProps.PROP_MIN_TEMPERATURE, _minTemperature);
                _rxProps.Put(ThermostatProps.PROP_MAX_TEMPERATURE, _maxTemperature);
                _rxProps.Put(ThermostatProps.PROP_TEMP_RESOLUTION, _supportHalfDegree ? 0.5f : 1.0f);
                _rxProps.Put(ThermostatProps.PROP_SETTING_TEMPERATURE, 20.0f);
                _rxProps.Put(ThermostatProps.PROP_CURRENT_TEMPERATURE, 20.0f);
                _rxProps.Put(ThermostatProps.PROP_FUNCTION_STATES, 0L);
                _rxProps.Commit();
            }
        }

        protected override int GetCapabilities() => CAP_STATUS_MULTI | CAP_CHARAC_MULTI;

        protected override int ParsePayload(KSPacket packet, StageablePropertyMap outProps)
        {
            switch (packet.CommandType)
            {
                case CMD_HEATING_STATE_REQ:  return ParseHeatingStateReq(packet, outProps);
                case CMD_TEMPERATURE_REQ:    return ParseTemperatureReq(packet, outProps);
                case CMD_RESERVED_MODE_REQ:  return ParseReservedModeReq(packet, outProps);
                case CMD_OUTING_SETTING_REQ: return ParseOutingSettingReq(packet, outProps);
                case CMD_HOTWATER_ONLY_REQ:  return ParseHotwaterOnlyReq(packet, outProps);
                case CMD_HEATING_STATE_RSP:
                case CMD_TEMPERATURE_RSP:
                case CMD_RESERVED_MODE_RSP:
                case CMD_OUTING_SETTING_RSP:
                case CMD_HOTWATER_ONLY_RSP:
                    int res = ParseStatusRsp(packet, outProps);
                    return res < PARSE_OK_NONE ? res : PARSE_OK_ACTION_PERFORMED;
                default: return base.ParsePayload(packet, outProps);
            }
        }

        // ---- Status ----

        protected override int ParseStatusReq(KSPacket packet, StageablePropertyMap outProps)
        {
            var devAddress = (KSAddress)GetAddress();
            if (!devAddress.GetDeviceSubId().IsFullOfGroup())
                return PARSE_OK_NONE;

            var data = new List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_STATUS_RSP, data.ToArray()));
            return PARSE_OK_STATE_UPDATED;
        }

        protected virtual void MakeStatusRspData(PropertyMap props, List<byte> outData)
        {
            outData.Add(0); // no error

            int heatingState  = 0;
            int outingSetting = 0;
            int reservedMode  = 0;
            int hotwaterOnly  = 0;
            int devIndex      = 0;

            foreach (var child in GetChildren<KSThermostat>())
            {
                var cp = child.GetReadPropertyMap();
                long states = cp.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
                if ((states & ThermostatProps.Function.HEATING)        != 0) heatingState  |= 1 << devIndex;
                if ((states & ThermostatProps.Function.OUTING_SETTING) != 0) outingSetting |= 1 << devIndex;
                if ((states & ThermostatProps.Function.RESERVED_MODE)  != 0) reservedMode  |= 1 << devIndex;
                if ((states & ThermostatProps.Function.HOTWATER_ONLY)  != 0) hotwaterOnly  |= 1 << devIndex;
                devIndex++;
            }

            outData.Add((byte)heatingState);
            outData.Add((byte)outingSetting);
            outData.Add((byte)reservedMode);
            outData.Add((byte)hotwaterOnly);

            foreach (var child in GetChildren<KSThermostat>())
            {
                var cp      = child.GetReadPropertyMap();
                float tempRes = cp.Get<float>(ThermostatProps.PROP_TEMP_RESOLUTION, 1.0f);
                float minT    = cp.Get<float>(ThermostatProps.PROP_MIN_TEMPERATURE,  0.0f);
                float maxT    = cp.Get<float>(ThermostatProps.PROP_MAX_TEMPERATURE, 40.0f);
                float setT    = cp.Get<float>(ThermostatProps.PROP_SETTING_TEMPERATURE, 20.0f);
                float curT    = cp.Get<float>(ThermostatProps.PROP_CURRENT_TEMPERATURE, 20.0f);
                outData.Add(EncodeTemp(setT, minT, maxT, tempRes));
                outData.Add(EncodeTemp(curT, minT, maxT, tempRes));
            }
        }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 5) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }

            int controllerCount = (packet.Data.Length - 5) / 2;
            if (controllerCount <= 0) controllerCount = 0;
            if (controllerCount > 8)  controllerCount = 8;

            var devAddress = (KSAddress)GetAddress();
            if (!devAddress.GetDeviceSubId().IsSingle() && !devAddress.GetDeviceSubId().IsSingleOfGroup())
                return PARSE_OK_NONE;

            int devIndex = (devAddress.GetDeviceSubId().Value() & 0x0F) - 1;
            if (devIndex < 0 || devIndex > controllerCount - 1) return PARSE_OK_NONE;

            bool heating    = (packet.Data[1] & (1 << devIndex)) != 0;
            bool outing     = (packet.Data[2] & (1 << devIndex)) != 0;
            bool reserved   = (packet.Data[3] & (1 << devIndex)) != 0;
            bool hotwater   = (packet.Data[4] & (1 << devIndex)) != 0;

            long newStates = 0;
            if (heating)  newStates |= ThermostatProps.Function.HEATING;
            if (outing)   newStates |= ThermostatProps.Function.OUTING_SETTING;
            if (reserved) newStates |= ThermostatProps.Function.RESERVED_MODE;
            if (hotwater) newStates |= ThermostatProps.Function.HOTWATER_ONLY;

            outProps.Put(ThermostatProps.PROP_FUNCTION_STATES, newStates);

            int tempOffset = 5 + devIndex * 2;
            float tempRes = outProps.Get<float>(ThermostatProps.PROP_TEMP_RESOLUTION, 1.0f);
            float minT    = outProps.Get<float>(ThermostatProps.PROP_MIN_TEMPERATURE, 0.0f);
            float maxT    = outProps.Get<float>(ThermostatProps.PROP_MAX_TEMPERATURE, 40.0f);
            float setT    = DecodeTemp(packet.Data[tempOffset],     minT, maxT, tempRes);
            float curT    = DecodeTemp(packet.Data[tempOffset + 1], minT, maxT, tempRes);

            outProps.Put(ThermostatProps.PROP_SETTING_TEMPERATURE, setT);
            outProps.Put(ThermostatProps.PROP_CURRENT_TEMPERATURE, curT);
            outProps.Commit();

            return PARSE_OK_STATE_UPDATED;
        }

        // ---- Characteristic ----

        protected override int ParseCharacteristicReq(KSPacket packet, StageablePropertyMap outProps)
        {
            var devAddress = (KSAddress)GetAddress();
            if (!devAddress.GetDeviceSubId().IsFullOfGroup())
                return PARSE_OK_NONE;

            var data = new List<byte>();
            data.Add(0); // no error
            data.Add((byte)_manufacturerId);
            data.Add((byte)_tempDetectType);

            var props = GetReadPropertyMap();
            data.Add((byte)(int)props.Get<float>(ThermostatProps.PROP_MAX_TEMPERATURE, 40.0f));
            data.Add((byte)(int)props.Get<float>(ThermostatProps.PROP_MIN_TEMPERATURE, 0.0f));

            int data5 = 0;
            long supported = props.Get<long>(ThermostatProps.PROP_SUPPORTED_FUNCTIONS);
            if ((supported & ThermostatProps.Function.OUTING_SETTING) != 0) data5 |= 1 << 1;
            if ((supported & ThermostatProps.Function.HOTWATER_ONLY)  != 0) data5 |= 1 << 2;
            if ((supported & ThermostatProps.Function.RESERVED_MODE)  != 0) data5 |= 1 << 3;
            float tempRes = props.Get<float>(ThermostatProps.PROP_TEMP_RESOLUTION, 1.0f);
            if (Math.Abs(tempRes - 0.5f) < 0.01f) data5 |= 1 << 4;
            data.Add((byte)data5);
            data.Add((byte)GetChildren<KSThermostat>().Count);

            SendPacket(CreatePacket(CMD_CHARACTERISTIC_RSP, data.ToArray()));
            return PARSE_OK_STATE_UPDATED;
        }

        protected override int ParseCharacteristicRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;
            int error = packet.Data[0] & 0xFF;
            if (error != 0) { OnErrorOccurred(HomeDevice.Error.UNKNOWN); return PARSE_OK_ERROR_RECEIVED; }

            if (packet.Data.Length >= 6)
            {
                _manufacturerId  = packet.Data[1] & 0xFF;
                _tempDetectType  = packet.Data[2] & 0xFF;
                _maxTemperature  = packet.Data[3] & 0xFF;
                _minTemperature  = packet.Data[4] & 0xFF;
                int flags        = packet.Data[5] & 0xFF;
                bool outingSupp  = (flags & (1 << 1)) != 0;
                bool hotwaterSupp= (flags & (1 << 2)) != 0;
                bool reservedSupp= (flags & (1 << 3)) != 0;
                _supportHalfDegree = (flags & (1 << 4)) != 0;
                if (packet.Data.Length >= 7)
                    _controllerCountInGroup = packet.Data[6] & 0xFF;

                long supported = ThermostatProps.Function.HEATING;
                if (outingSupp)   supported |= ThermostatProps.Function.OUTING_SETTING;
                if (hotwaterSupp) supported |= ThermostatProps.Function.HOTWATER_ONLY;
                if (reservedSupp) supported |= ThermostatProps.Function.RESERVED_MODE;

                outProps.Put(ThermostatProps.PROP_SUPPORTED_FUNCTIONS, supported);
                outProps.Put(ThermostatProps.PROP_MIN_TEMPERATURE,     _minTemperature);
                outProps.Put(ThermostatProps.PROP_MAX_TEMPERATURE,     _maxTemperature);
                outProps.Put(ThermostatProps.PROP_TEMP_RESOLUTION,     _supportHalfDegree ? 0.5f : 1.0f);
                outProps.Commit();
            }
            return PARSE_OK_STATE_UPDATED;
        }

        // ---- Control request parsing (slave mode) ----

        private int ParseHeatingStateReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 1) return PARSE_ERROR_MALFORMED_PACKET;

            // Update state and respond with current status
            long states = outProps.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
            bool heat   = (packet.Data[0] & 0xFF) != 0;
            if (heat) states |= ThermostatProps.Function.HEATING;
            else      states &= ~ThermostatProps.Function.HEATING;
            outProps.Put(ThermostatProps.PROP_FUNCTION_STATES, states);
            outProps.Commit();

            // Build response same as status response
            var data = new List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_HEATING_STATE_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }

        private int ParseTemperatureReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            if (packet.Data.Length < 2) return PARSE_ERROR_MALFORMED_PACKET;

            float tempRes = outProps.Get<float>(ThermostatProps.PROP_TEMP_RESOLUTION, 1.0f);
            float minT    = outProps.Get<float>(ThermostatProps.PROP_MIN_TEMPERATURE, 0.0f);
            float maxT    = outProps.Get<float>(ThermostatProps.PROP_MAX_TEMPERATURE, 40.0f);
            float setT    = DecodeTemp(packet.Data[0], minT, maxT, tempRes);
            outProps.Put(ThermostatProps.PROP_SETTING_TEMPERATURE, setT);
            outProps.Commit();

            var data = new List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_TEMPERATURE_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }

        private int ParseReservedModeReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            long states = outProps.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
            bool on = (packet.Data.Length > 0) && (packet.Data[0] & 0xFF) != 0;
            if (on) states |= ThermostatProps.Function.RESERVED_MODE;
            else    states &= ~ThermostatProps.Function.RESERVED_MODE;
            outProps.Put(ThermostatProps.PROP_FUNCTION_STATES, states);
            outProps.Commit();
            var data = new List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_RESERVED_MODE_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }

        private int ParseOutingSettingReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            long states = outProps.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
            bool on = (packet.Data.Length > 0) && (packet.Data[0] & 0xFF) != 0;
            if (on) states |= ThermostatProps.Function.OUTING_SETTING;
            else    states &= ~ThermostatProps.Function.OUTING_SETTING;
            outProps.Put(ThermostatProps.PROP_FUNCTION_STATES, states);
            outProps.Commit();
            var data = new List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_OUTING_SETTING_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }

        private int ParseHotwaterOnlyReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            long states = outProps.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
            bool on = (packet.Data.Length > 0) && (packet.Data[0] & 0xFF) != 0;
            if (on) states |= ThermostatProps.Function.HOTWATER_ONLY;
            else    states &= ~ThermostatProps.Function.HOTWATER_ONLY;
            outProps.Put(ThermostatProps.PROP_FUNCTION_STATES, states);
            outProps.Commit();
            var data = new List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_HOTWATER_ONLY_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }

        // ---- Temperature encoding helpers ----
        private static byte EncodeTemp(float temp, float min, float max, float res)
        {
            float clamped = Math.Max(min, Math.Min(max, temp));
            if (Math.Abs(res - 0.5f) < 0.01f) return (byte)(int)(clamped * 2.0f + 0.5f);
            return (byte)(int)(clamped + 0.5f);
        }

        private static float DecodeTemp(byte raw, float min, float max, float res)
        {
            float val = (Math.Abs(res - 0.5f) < 0.01f) ? (raw & 0xFF) * 0.5f : (raw & 0xFF);
            return Math.Max(min, Math.Min(max, val));
        }
    }
}

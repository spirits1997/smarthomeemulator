// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System.Collections.Generic;
using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    /// <summary>
    /// Non-standard extension of KSThermostat used by KyungDong (KD) devices.
    /// Equivalent to Android's KDThermostat.
    /// Key difference: power-off uses a separate command (0x50) rather than the standard control.
    /// </summary>
    public class KDThermostat : KSThermostat
    {
        public const int CMD_POWER_OFF_REQ = 0x50;

        public KDThermostat(KSMainContext ctx, Dictionary<string, object> props)
            : base(ctx, props) { }

        protected override int ParseStatusRsp(KSPacket packet, StageablePropertyMap outProps)
        {
            int result = base.ParseStatusRsp(packet, outProps);
            if (result == PARSE_OK_STATE_UPDATED)
            {
                // HACK: No explicit power-off field – derive ONOFF from whether any function is active.
                long states = outProps.Get<long>(ThermostatProps.PROP_FUNCTION_STATES);
                outProps.Put(HomeDevice.PROP_ONOFF, states != 0L);
                outProps.Commit();
            }
            return result;
        }

        protected override int ParsePayload(KSPacket packet, StageablePropertyMap outProps)
        {
            if (packet.CommandType == CMD_POWER_OFF_REQ)
                return ParsePowerOffReq(packet, outProps);
            return base.ParsePayload(packet, outProps);
        }

        private int ParsePowerOffReq(KSPacket packet, StageablePropertyMap outProps)
        {
            if (!IsSlave()) return PARSE_OK_NONE;
            bool powerOff = (packet.Data.Length > 0) && (packet.Data[0] & 0xFF) != 0;
            outProps.Put(HomeDevice.PROP_ONOFF, !powerOff);
            if (powerOff)
            {
                outProps.Put(ThermostatProps.PROP_FUNCTION_STATES, 0L);
            }
            outProps.Commit();
            var data = new System.Collections.Generic.List<byte>();
            MakeStatusRspData(GetReadPropertyMap(), data);
            SendPacket(CreatePacket(CMD_STATUS_RSP, data.ToArray()));
            return PARSE_OK_ACTION_PERFORMED;
        }
    }

    /// <summary>
    /// KD-extended main context – replaces KSThermostat (0x36) with KDThermostat.
    /// Equivalent to Android's KDMainContext.
    /// </summary>
    public class KDMainContext : KSMainContext2
    {
        public KDMainContext() : base()
        {
            // Override thermostat with KD-extended version
            _addressToFactory[0x36] = (ctx, props) => new KDThermostat(ctx, props);
        }
    }
}

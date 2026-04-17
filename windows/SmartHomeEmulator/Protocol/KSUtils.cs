// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

namespace SmartHomeEmulator.Protocol
{
    public static class KSUtils
    {
        /// <summary>Convert a temperature byte (units of 0.5 °C) to float °C.</summary>
        public static float ParseTemperature(int raw, bool halfDegreeSupport)
        {
            if (halfDegreeSupport)
                return raw * 0.5f;
            return raw;
        }

        /// <summary>Convert float °C to temperature byte.</summary>
        public static int EncodeTemperature(float celsius, bool halfDegreeSupport)
        {
            if (halfDegreeSupport)
                return (int)(celsius * 2.0f + 0.5f);
            return (int)(celsius + 0.5f);
        }

        /// <summary>Count the number of set bits in a byte value.</summary>
        public static int PopCount(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }
    }
}

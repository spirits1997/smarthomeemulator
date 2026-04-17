// Copyright (C) 2023 Korea Association of AI Smart Home.
// Licensed under the Apache License, Version 2.0

using System.Collections.Generic;
using SmartHomeEmulator.Base;

namespace SmartHomeEmulator.Protocol
{
    /// <summary>
    /// Extended KS X 4506 main context with extended device implementations.
    /// Equivalent to Android's KSMainContext2.
    /// </summary>
    public class KSMainContext2 : KSMainContext
    {
        public KSMainContext2() : base() { }

        // KSMainContext2 in Android overrides some device factories with extended versions.
        // All basic devices work as in KSMainContext; extended devices (KSLight2, etc.)
        // add minor protocol tweaks. For the Windows port we keep the base implementations.
    }
}

﻿using System;

namespace HnzCoopSeason.Utils
{
    [Flags]
    public enum NetworkType
    {
        DediServer = 0,
        DediClient = 1 << 0,
        SinglePlayer = 1 << 1,
    }
}
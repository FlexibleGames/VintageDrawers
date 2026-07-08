using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace VintageDrawers
{
    public class DrawerConfig
    {
        [AllowNull]
        public static DrawerConfig Current { get; set; }

        public int LabelInfoMaxRenderDistanceInBlocks { get; set; } = 50;
    }
}

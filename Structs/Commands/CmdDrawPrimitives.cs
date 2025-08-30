﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SVGA3dCmdDrawPrimitives
    {
        public uint cid;
        public uint numVertexDecls;
        public uint numRanges;
    }
}

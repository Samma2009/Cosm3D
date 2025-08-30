using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SVGA3dCopyBox
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;
        public uint h;
        public uint d;
        public uint srcx;
        public uint srcy;
        public uint srcz;
    }
}

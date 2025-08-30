using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SVGA3dPrimitiveRange
    {
        public SVGA3dPrimitiveType primType;
        public uint primitiveCount;
        public SVGA3dArray indexArray;
        public uint indexWidth;
        public int indexBias;
    }
}

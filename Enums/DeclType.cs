﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    public enum SVGA3dDeclType
    {
        SVGA3D_DECLTYPE_FLOAT1 = 0,
        SVGA3D_DECLTYPE_FLOAT2 = 1,
        SVGA3D_DECLTYPE_FLOAT3 = 2,
        SVGA3D_DECLTYPE_FLOAT4 = 3,
        SVGA3D_DECLTYPE_D3DCOLOR = 4,
        SVGA3D_DECLTYPE_UBYTE4 = 5,
        SVGA3D_DECLTYPE_SHORT2 = 6,
        SVGA3D_DECLTYPE_SHORT4 = 7,
        SVGA3D_DECLTYPE_UBYTE4N = 8,
        SVGA3D_DECLTYPE_SHORT2N = 9,
        SVGA3D_DECLTYPE_SHORT4N = 10,
        SVGA3D_DECLTYPE_USHORT2N = 11,
        SVGA3D_DECLTYPE_USHORT4N = 12,
        SVGA3D_DECLTYPE_UDEC3 = 13,
        SVGA3D_DECLTYPE_DEC3N = 14,
        SVGA3D_DECLTYPE_FLOAT16_2 = 15,
        SVGA3D_DECLTYPE_FLOAT16_4 = 16,
        SVGA3D_DECLTYPE_MAX,
    }
}

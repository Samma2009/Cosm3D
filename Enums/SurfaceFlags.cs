﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    public enum SVGA3dSurfaceFlags : uint
    {
        SVGA3D_SURFACE_CUBEMAP = (1 << 0),
        SVGA3D_SURFACE_HINT_STATIC = (1 << 1),
        SVGA3D_SURFACE_HINT_DYNAMIC = (1 << 2),
        SVGA3D_SURFACE_HINT_INDEXBUFFER = (1 << 3),
        SVGA3D_SURFACE_HINT_VERTEXBUFFER = (1 << 4),
        SVGA3D_SURFACE_HINT_TEXTURE = (1 << 5),
        SVGA3D_SURFACE_HINT_RENDERTARGET = (1 << 6),
        SVGA3D_SURFACE_HINT_DEPTHSTENCIL = (1 << 7),
        SVGA3D_SURFACE_HINT_WRITEONLY = (1 << 8),
        SVGA3D_SURFACE_MASKABLE_ANTIALIAS = (1 << 9),
        SVGA3D_SURFACE_AUTOGENMIPMAPS = (1 << 10),
    }
}

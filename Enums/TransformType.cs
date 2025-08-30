﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    public enum SVGA3dTransformType
    {
        SVGA3D_TRANSFORM_INVALID = 0,
        SVGA3D_TRANSFORM_WORLD = 1,
        SVGA3D_TRANSFORM_VIEW = 2,
        SVGA3D_TRANSFORM_PROJECTION = 3,
        SVGA3D_TRANSFORM_TEXTURE0 = 4,
        SVGA3D_TRANSFORM_TEXTURE1 = 5,
        SVGA3D_TRANSFORM_TEXTURE2 = 6,
        SVGA3D_TRANSFORM_TEXTURE3 = 7,
        SVGA3D_TRANSFORM_TEXTURE4 = 8,
        SVGA3D_TRANSFORM_TEXTURE5 = 9,
        SVGA3D_TRANSFORM_TEXTURE6 = 10,
        SVGA3D_TRANSFORM_TEXTURE7 = 11,
        SVGA3D_TRANSFORM_WORLD1 = 12,
        SVGA3D_TRANSFORM_WORLD2 = 13,
        SVGA3D_TRANSFORM_WORLD3 = 14,
        SVGA3D_TRANSFORM_MAX
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMwareSvgaII3D
{
    public enum SVGA3dDeclMethod
    {
        SVGA3D_DECLMETHOD_DEFAULT = 0,
        SVGA3D_DECLMETHOD_PARTIALU,
        SVGA3D_DECLMETHOD_PARTIALV,
        SVGA3D_DECLMETHOD_CROSSUV,
        SVGA3D_DECLMETHOD_UV,
        SVGA3D_DECLMETHOD_LOOKUP,
        SVGA3D_DECLMETHOD_LOOKUPPRESAMPLED,
    }
}

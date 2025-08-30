﻿using Cosmos.Core;
using Cosmos.HAL.Drivers.Video.SVGAII;
using Cosmos.HAL;
using Cosmos.System;
using Cosmos.System.Graphics;

namespace VMwareSvgaII3D
{
    public unsafe class VMWareSVGAII3D
    {
        public bool Is3DEnabled;
        public uint HW3DVer;

        public VMWareSVGAII3D()
        {
            device = PCI.GetDevice(VendorID.VMWare, DeviceID.SVGAIIAdapter);
            device.EnableMemory(true);
            uint basePort = device.BaseAddressBar[0].BaseAddress;
            indexPort = (ushort)(basePort + (uint)IOPortOffset.Index);
            valuePort = (ushort)(basePort + (uint)IOPortOffset.Value);

            WriteRegister(Register.ID, (uint)ID.V2);
            if (ReadRegister(Register.ID) != (uint)ID.V2)
            {
                return;
            }

            videoMemory = new MemoryBlock(ReadRegister(Register.FrameBufferStart), ReadRegister(Register.VRamSize));
            capabilities = ReadRegister(Register.Capabilities);

            InitializeFIFO();
        }

        #region Methods

        uint contextid = 0;

        uint GetNextContextId() => ++contextid;


        uint surfaceid = 0;

        uint GetNextSurfaceId() => ++surfaceid;

        private bool fifoFenceSupported = false;
        private uint guestFenceCounter = 1;

        private void SyncToFence(uint fence)
        {
            if (fifoFenceSupported && fifoMemory != null)
            {
                while (ReadFifo3D(Register3D.SVGA_FIFO_FENCE) < fence) { }
            }
            else
            {
                WriteRegister(Register.Sync, 1);
                while (ReadRegister(Register.Busy) != 0) { }
            }
        }

        private uint InsertFence()
        {
            uint fence = ++guestFenceCounter;

            if (fifoFenceSupported && fifoMemory != null)
            {
                WriteFifo3D(Register3D.SVGA_FIFO_FENCE, fence);
            }
            else
            {
                WriteRegister(Register.Sync, fence);
            }

            return fence;
        }

        public void* ReserveFIFO3D(uint cmd, uint cmdSize)
        {
            SVGA3dCmdHeader* header;

            header = (SVGA3dCmdHeader*)ReserveFIFO((uint)sizeof(SVGA3dCmdHeader) + cmdSize);
            header->id = cmd;
            header->size = cmdSize;

            return &header[1];
        }

        void BeginSurfaceDefinition(
            uint sid,
            SVGA3dSurfaceFlags flags,
            SVGA3dSurfaceFormat format,
            ref uint* faces,
            out SVGA3dSize* mipSizes,
            uint numMipSizes)
        {
            SVGA3dCmdDefineSurface* cmd = (SVGA3dCmdDefineSurface*)ReserveFIFO3D(
                (uint)FIFOCommand.DEFINE_SURFACE,
                (uint)sizeof(SVGA3dCmdDefineSurface) + (uint)(numMipSizes * sizeof(SVGA3dSize)));

            cmd->sid = sid;
            cmd->flags = flags;
            cmd->format = format;

            faces = &cmd->face[0];
            mipSizes = (SVGA3dSize*)&cmd[1];

            MemoryOperations.Fill((byte*)faces, 0, sizeof(uint) * 6);
            MemoryOperations.Fill((byte*)mipSizes, 0, sizeof(SVGA3dSize) * (int)numMipSizes);
        }


        public SVGA3dSurfaceImageId DefineSurface(uint width, uint height, SVGA3dSurfaceFormat format)
        {
            uint sid = GetNextSurfaceId();
            SVGA3dSize* mipSizes;
            uint* faces = null;

            BeginSurfaceDefinition(sid, 0, format, ref faces, out mipSizes, 1);

            faces[0] = 1;
            mipSizes[0].width = width;
            mipSizes[0].height = height;
            mipSizes[0].depth = 1;

            WaitForFifo();

            return new() { sid = sid, face = 0, mipmap = 0 };
        }

        public void SetRenderTarget(uint cid, SVGA3dRenderTargetType type, SVGA3dSurfaceImageId target)
        {
            SVGA3dCmdSetRenderTarget* cmd;
            cmd = (SVGA3dCmdSetRenderTarget*)ReserveFIFO3D((uint)FIFOCommand.SET_RENDER_TARGET, (uint)sizeof(SVGA3dCmdSetRenderTarget));
            cmd->cid = cid;
            cmd->type = type;
            cmd->target = target;
            WaitForFifo();
        }

        public void SetViewport(uint cid, SVGA3dRect rect)
        {
            SVGA3dCmdSetViewport* cmd;
            cmd = (SVGA3dCmdSetViewport*)ReserveFIFO3D((uint)FIFOCommand.SET_VIEWPORT, (uint)sizeof(SVGA3dCmdSetViewport));
            cmd->cid = cid;
            cmd->rect = rect;
            WaitForFifo();
        }

        public void SetDepthRange(uint cid, float min, float max)
        {
            SVGA3dCmdSetZRange* cmd;
            cmd = (SVGA3dCmdSetZRange*)ReserveFIFO3D((uint)FIFOCommand.SET_ZRANGE, (uint)sizeof(SVGA3dCmdSetZRange));
            cmd->cid = cid;
            cmd->range.min = min;
            cmd->range.max = max;
            WaitForFifo();
        }

        void BeginClear3D(
            uint cid,
            ClearFlags flags,
            uint color,
            float dpeth,
            uint stencil,
            SVGA3dRect** rects,
            uint numRects)
        {
            SVGA3dCmdClear* cmd;
            cmd = (SVGA3dCmdClear*)ReserveFIFO3D((uint)FIFOCommand.CLEAR, (uint)sizeof(SVGA3dCmdClear) + (uint)(numRects * sizeof(SVGA3dRect)));

            cmd->cid = cid;
            cmd->flag = flags;
            cmd->color = color;
            cmd->depth = dpeth;
            cmd->stencil = stencil;
            *rects = (SVGA3dRect*)&cmd[1];
        }

        public void Clear3D(uint cid, ClearFlags flags, SVGA3dRect ClearRect, uint color = 0, float dpeth = 1, uint stencil = 0)
        {
            SVGA3dRect* rect;

            BeginClear3D(cid, flags, color, depth, stencil, &rect, 1);
            rect->x = ClearRect.x;
            rect->y = ClearRect.y;
            rect->w = ClearRect.w;
            rect->h = ClearRect.h;
            WaitForFifo();
        }

        void BeginPresent(uint sid, SVGA3dCopyRect** rects, uint numRects)
        {
            SVGA3dCmdPresent* cmd;
            cmd = (SVGA3dCmdPresent*)ReserveFIFO3D((uint)FIFOCommand.PRESENT, (uint)sizeof(SVGA3dCmdPresent) + (uint)(numRects * sizeof(SVGA3dCopyRect)));
            cmd->sid = sid;
            *rects = (SVGA3dCopyRect*)&cmd[1];
        }

        uint lastfence = 1;

        public void Present(SVGA3dSurfaceImageId image, SVGA3dRect PresentRect)
        {
            SVGA3dCopyRect* rect;

            SyncToFence(lastfence);

            BeginPresent(image.sid, &rect, 1);
            MemoryOperations.Fill((byte*)rect, 0, sizeof(SVGA3dCopyRect));
            rect->x = PresentRect.x;
            rect->y = PresentRect.y;
            rect->w = PresentRect.w;
            rect->h = PresentRect.h;
            WaitForFifo();

            lastfence = InsertFence();
        }

        void BeginSurfaceDMA(
            SVGA3dGuestImage* guestImage,
            SVGA3dSurfaceImageId* hostImage,
            SVGA3dTransferType transfer,
            SVGA3dCopyBox** boxes,
            uint numBoxes)
        {
            SVGA3dCmdSurfaceDMA* cmd;
            uint boxesSize = (uint)sizeof(SVGA3dCopyBox) * numBoxes;

            cmd = (SVGA3dCmdSurfaceDMA*)ReserveFIFO3D((uint)FIFOCommand.SURFACE_DMA, (uint)sizeof(SVGA3dCmdSurfaceDMA) + boxesSize);

            cmd->guest = *guestImage;
            cmd->host = *hostImage;
            cmd->transfer = transfer;
            *boxes = (SVGA3dCopyBox*)&cmd[1];

            MemoryOperations.Fill((byte*)*boxes, 0, (int)boxesSize);
        }


        void SurfaceDMA2D(
            uint sid,
            SVGAGuestPtr* guestPtr,
            SVGA3dTransferType transfer,
            uint width,
            uint height)
        {
            SVGA3dCopyBox* boxes;
            SVGA3dGuestImage guestImage;
            SVGA3dSurfaceImageId hostImage = new() { sid = sid };

            guestImage.ptr = *guestPtr;
            guestImage.pitch = 0;

            BeginSurfaceDMA(&guestImage, &hostImage, transfer, &boxes, 1);
            boxes[0].w = width;
            boxes[0].h = height;
            boxes[0].d = 1;
            WaitForFifo();
        }

        public uint CreateStaticArrayBuffer<T>(T[] data) where T : unmanaged
        {
            uint size = (uint)(data.Length * sizeof(T));

            // Define the buffer surface (same as before)
            uint sid = DefineSurface(size, 1, SVGA3dSurfaceFormat.SVGA3D_BUFFER).sid;

            // Allocate a framebuffer-backed region and get guest pointer
            SVGAGuestPtr gPtr;
            void* buffer = SVGA3DUtil_AllocDMABuffer(size, out gPtr);

            // Copy data directly into framebuffer-backed memory (no extra staging)
            fixed (T* pData = &data[0])
            {
                MemoryOperations.Copy((byte*)buffer, (byte*)pData, (int)size);
            }

            // Tell the host to read from that guest pointer (host will read BAR1 + gPtr.offset)
            SurfaceDMA2D(sid, &gPtr, SVGA3dTransferType.SVGA3D_WRITE_HOST_VRAM, size, 1);

            return sid;
        }


        public uint TestDebugBuffer()
        {
            void* buffer;
            SVGAGuestPtr gPtr;
            uint sid = DefineSurface(1280, 720, SVGA3dSurfaceFormat.SVGA3D_A8R8G8B8).sid;

            buffer = SVGA3DUtil_AllocDMABuffer(1280 * 720 * 4, out gPtr);

            MemoryOperations.Fill((byte*)buffer, 0xff00ff, (int)(1280 * 720 * 4));

            SurfaceDMA2D(sid, &gPtr, SVGA3dTransferType.SVGA3D_WRITE_HOST_VRAM, 1280, 720);

            return sid;
        }

        public SVGA3dSurfaceImageId DefineSurfaceFromImage(Image image)
        {
            void* buffer;
            SVGAGuestPtr gPtr;
            uint sid = DefineSurface(image.Width, image.Height, SVGA3dSurfaceFormat.SVGA3D_A8R8G8B8).sid;

            buffer = SVGA3DUtil_AllocDMABuffer(image.Width * image.Height * sizeof(int), out gPtr);

            fixed (int* rawDataPtr = &image.RawData[0])
            {
                MemoryOperations.Copy((byte*)buffer, (byte*)rawDataPtr, image.RawData.Length * sizeof(int));
            }

            SurfaceDMA2D(sid, &gPtr, SVGA3dTransferType.SVGA3D_WRITE_HOST_VRAM, image.Width, image.Height);

            return new() { sid = sid, face = 0, mipmap = 0 };
        }

        void BeginSetRenderState(uint cid, SVGA3dRenderState** states, uint numstates)
        {
            SVGA3dCmdSetRenderState* cmd;
            cmd = (SVGA3dCmdSetRenderState*)ReserveFIFO3D((uint)FIFOCommand.SETRENDERSTATE, (uint)(sizeof(SVGA3dCmdSetRenderState) + sizeof(SVGA3dRenderState) * numstates));

            cmd->cid = cid;

            *states = (SVGA3dRenderState*)&cmd[1];
        }

        public void SetRenderState(uint cid, SVGA3dRenderState[] states)
        {
            SVGA3dRenderState* rs;
            BeginSetRenderState(cid, &rs, (uint)states.Length);

            fixed (SVGA3dRenderState* statesPtr = &states[0])
            {
                MemoryOperations.Copy((byte*)rs, (byte*)statesPtr, sizeof(SVGA3dRenderState) * states.Length);
            }

            WaitForFifo();
        }

        void BeginSetTextureState(uint cid, SVGA3dTextureState** states, uint numStates)
        {
            SVGA3dCmdSetTextureState* cmd;
            cmd = (SVGA3dCmdSetTextureState*)ReserveFIFO3D((uint)FIFOCommand.SETTEXTURESTATE, (uint)(sizeof(SVGA3dCmdSetTextureState) + sizeof(SVGA3dTextureState) * numStates));
            cmd->cid = cid;

            *states = (SVGA3dTextureState*)&cmd[1];
        }

        public void SetTextureState(uint cid, SVGA3dTextureState[] states)
        {
            SVGA3dTextureState* ts;
            BeginSetTextureState(cid, &ts, (uint)states.Length);

            fixed (SVGA3dTextureState* statesPtr = &states[0])
            {
                MemoryOperations.Copy((byte*)ts, (byte*)statesPtr, sizeof(SVGA3dTextureState) * states.Length);
            }

            WaitForFifo();
        }

        void BeginDrawPrimitives(
            uint cid,
            SVGA3dVertexDecl** decls,
            uint numVertexDecls,
            SVGA3dPrimitiveRange** ranges,
            uint numRanges)
        {
            SVGA3dCmdDrawPrimitives* cmd;
            SVGA3dVertexDecl* declArray;
            SVGA3dPrimitiveRange* rangeArray;
            uint declSize = (uint)sizeof(SVGA3dVertexDecl) * numVertexDecls;
            uint rangeSize = (uint)sizeof(SVGA3dPrimitiveRange) * numRanges;

            cmd = (SVGA3dCmdDrawPrimitives*)ReserveFIFO3D((uint)FIFOCommand.DRAW_PRIMITIVES, (uint)sizeof(SVGA3dCmdDrawPrimitives) + declSize + rangeSize);

            cmd->cid = cid;
            cmd->numVertexDecls = numVertexDecls;
            cmd->numRanges = numRanges;

            declArray = (SVGA3dVertexDecl*)&cmd[1];
            rangeArray = (SVGA3dPrimitiveRange*)&declArray[numVertexDecls];

            MemoryOperations.Fill((byte*)declArray, 0, (int)declSize);
            MemoryOperations.Fill((byte*)rangeArray, 0, (int)rangeSize);

            *decls = declArray;
            *ranges = rangeArray;
        }

        public void DrawPrimitives(
            uint cid,
            SVGA3dVertexDecl[] decls,
            SVGA3dPrimitiveRange[] ranges)
        {
            SVGA3dVertexDecl* vdecls;
            SVGA3dPrimitiveRange* pranges;
            BeginDrawPrimitives(cid, &vdecls, (uint)decls.Length, &pranges, (uint)ranges.Length);

            //for (int i = 0; i < decls.Length; i++)
            //    vdecls[i] = decls[i];

            //for (int i = 0; i < ranges.Length; i++)
            //    pranges[i] = ranges[i];

            fixed (SVGA3dVertexDecl* statesPtr = &decls[0])
            {
                MemoryOperations.Copy((byte*)vdecls, (byte*)statesPtr, sizeof(SVGA3dVertexDecl) * decls.Length);
            }
            fixed (SVGA3dPrimitiveRange* statesPtr = &ranges[0])
            {
                MemoryOperations.Copy((byte*)pranges, (byte*)statesPtr, sizeof(SVGA3dPrimitiveRange) * ranges.Length);
            }

            WaitForFifo();
        }

        void InternalSetTransform(uint cid, SVGA3dTransformType type, float* matrix)
        {
            SVGA3dCmdSetTransform* cmd;
            cmd = (SVGA3dCmdSetTransform*)ReserveFIFO3D((uint)FIFOCommand.SETTRANSFORM, (uint)sizeof(SVGA3dCmdSetTransform));
            cmd->cid = cid;
            cmd->type = type;

            MemoryOperations.Copy((byte*)&cmd->matrix[0], (byte*)matrix, sizeof(float) * 16);
            WaitForFifo();
        }

        public void SetTransform<T>(uint cid, SVGA3dTransformType type, T matrix4x4)
        {
            if (sizeof(T) == 16 * sizeof(float))
                InternalSetTransform(cid, type, (float*)&matrix4x4);
            else
                throw new ArgumentException("Matrix must be 4x4 float");
        }

        uint shaderidVS = 0;
        uint shaderidPS = 0;

        uint GetNextShaderId(SVGA3dShaderType type)
        {
            switch (type)
            {
                case SVGA3dShaderType.SVGA3D_SHADERTYPE_VS: return shaderidVS++;
                case SVGA3dShaderType.SVGA3D_SHADERTYPE_PS: return shaderidPS++;

                default: return 0;
            }
        }

        public uint DefineShader(uint cid, SVGA3dShaderType type, byte[] bytecode)
        {
            SVGA3dCmdDefineShader* cmd;

            cmd = (SVGA3dCmdDefineShader*)ReserveFIFO3D((uint)FIFOCommand.SHADER_DEFINE, (uint)sizeof(SVGA3dCmdDefineShader) + (uint)bytecode.Length);

            var shid = GetNextShaderId(type);

            cmd->cid = cid;
            cmd->shid = shid;
            cmd->type = type;

            fixed (byte* bytecodePtr = &bytecode[0])
            {
                MemoryOperations.Copy((byte*)&cmd[1], bytecodePtr, bytecode.Length);
            }

            WaitForFifo();

            return shid;
        }

        public void SetShader(uint cid, SVGA3dShaderType type, uint shid)
        {
            SVGA3dCmdSetShader* cmd;

            cmd = (SVGA3dCmdSetShader*)ReserveFIFO3D((uint)FIFOCommand.SET_SHADER, (uint)sizeof(SVGA3dCmdSetShader));
            cmd->cid = cid;
            cmd->type = type;
            cmd->shid = shid;
            WaitForFifo();
        }

        public void SetShaderUniform<T>(uint cid, uint reg, SVGA3dShaderType type, SVGA3dShaderConstType ctype, params T[] value) where T : unmanaged
        {
            for (int i = 0; i < value.Length; i++)
            {
                SVGA3dCmdSetShaderConst* cmd;

                cmd = (SVGA3dCmdSetShaderConst*)ReserveFIFO3D((uint)FIFOCommand.SET_SHADER_CONST, (uint)sizeof(SVGA3dCmdSetShaderConst));
                cmd->cid = cid;
                cmd->reg = reg + (uint)i;
                cmd->type = type;
                cmd->ctype = ctype;

                T v = value[i];

                switch (ctype)
                {
                    case SVGA3dShaderConstType.SVGA3D_CONST_TYPE_FLOAT:
                    case SVGA3dShaderConstType.SVGA3D_CONST_TYPE_INT:
                        MemoryOperations.Copy((byte*)&cmd->values, (byte*)&v, sizeof(T));
                        break;
                    case SVGA3dShaderConstType.SVGA3D_CONST_TYPE_BOOL:
                        MemoryOperations.Fill((byte*)&cmd->values, 0, sizeof(T));
                        cmd->values[0] = *(uint*)&v;
                        break;
                    default:
                        throw new Exception("Unknown shader const type");
                }

                WaitForFifo();
            }
        }

        public uint DefineContext()
        {
            uint cid = GetNextContextId();

            SVGA3dCmdDefineContext* cmd;
            cmd = (SVGA3dCmdDefineContext*)ReserveFIFO3D((uint)FIFOCommand.DEFINE_CONTEXT, (uint)sizeof(SVGA3dCmdDefineContext));
            cmd->cid = cid;

            WaitForFifo();
            return cid;
        }

        private static SVGAGuestPtr nextPtr = new SVGAGuestPtr { gmrId = 0, offset = 0 };

        // Add near top of class:
        private const uint SVGA_GMR_FRAMEBUFFER = 0xFFFFFFFEu; // (uint)-2

        public unsafe void* SVGA3DUtil_AllocDMABuffer(uint size, out SVGAGuestPtr ptr)
        {
            // 4-byte alignment
            uint alignedSize = (size + 3u) & ~3u;

            // make sure device supports GMR/framebuffer as a guest pointer
            // Capability.Gmr is your capability bit you already read into `capabilities`
            if ((capabilities & (uint)Capability.Gmr) == 0)
            {
                // Fallback: keep previous behavior (may still work if you implement a real GMR)
                // But warn (or throw) because using framebuffer GMR won't be available.
                throw new InvalidOperationException("SVGA device does not support GMR — cannot allocate framebuffer-backed guest pointer.");
            }

            // Check available space in BAR1 (videoMemory)
            if (nextPtr.offset + alignedSize > videoMemory.Size)
            {
                throw new OutOfMemoryException("Not enough VRAM for framebuffer-backed buffer");
            }

            // Build the guest pointer to refer to the framebuffer GMR and offset
            ptr = new SVGAGuestPtr
            {
                gmrId = SVGA_GMR_FRAMEBUFFER,    // tells host: this guest pointer refers to BAR1/framebuffer
                offset = nextPtr.offset          // byte offset into BAR1
            };

            // the buffer pointer you can write to
            void* buffer = (void*)(videoMemory.Base + nextPtr.offset);

            // advance allocation cursor
            nextPtr.offset += alignedSize;

            return buffer;
        }


        /// <summary>
        /// Initialize FIFO.
        /// </summary>
        public void InitializeFIFO()
        {
            fifoMemory = new MemoryBlock(ReadRegister(Register.MemStart), ReadRegister(Register.MemSize));
            fifoMemory[(uint)FIFO.Min] = (uint)Register.FifoNumRegisters * sizeof(uint);
            fifoMemory[(uint)FIFO.Max] = fifoMemory.Size;
            fifoMemory[(uint)FIFO.NextCmd] = fifoMemory[(uint)FIFO.Min];
            fifoMemory[(uint)FIFO.Stop] = fifoMemory[(uint)FIFO.Min];

            if (((capabilities & 0x00008000) != 0) &&
                ((capabilities & (uint)Capability.Cap3D) != 0) &&
                (fifoMemory[(uint)FIFO.Min] > ((uint)Register3D.SVGA_FIFO_3D_HWVERSION << 2)))
            {
                WriteFifo3D(Register3D.SVGA_FIFO_3D_HWVERSION, ((2u) << 16) | (1u & 0xFFu));

                Kernel.PrintDebug("3D enabled " + capabilities);
                Is3DEnabled = true;

                if ((capabilities & (1 << 8)) != 0)
                {
                    HW3DVer = ReadFifo3D(Register3D.SVGA_FIFO_3D_HWVERSION_REVISED);

                    if (HW3DVer < (((2u) << 16) | (0u & 0xFFu)))
                        Is3DEnabled = false;
                }
                else
                    Is3DEnabled = false;
            }
            else
            {
                Kernel.PrintDebug("3D not enabled " + capabilities);
                Is3DEnabled = false;
            }


            WriteRegister((Register)1, 1);
            WriteRegister(Register.ConfigDone, 1);
        }

        private uint ReadFifo3D(Register3D reg)
        {
            return fifoMemory[(uint)reg << 2];
        }

        private void WriteFifo3D(Register3D reg, uint value)
        {
            fifoMemory[(uint)reg << 2] = value;
        }


        /// <summary>
        /// Set video mode.
        /// </summary>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="depth">Depth.</param>
        public void SetMode(uint width, uint height, uint depth = 32)
        {
            //Disable the Driver before writing new values and initiating it again to avoid a memory exception
            //Disable();

            // Depth is color depth in bytes.
            this.depth = depth / 8;
            this.width = width;
            this.height = height;
            WriteRegister(Register.Width, width);
            WriteRegister(Register.Height, height);
            WriteRegister(Register.BitsPerPixel, depth);
            Enable();
            InitializeFIFO();

            FrameSize = ReadRegister(Register.FrameBufferSize);
            FrameOffset = ReadRegister(Register.FrameBufferOffset);
        }

        /// <summary>
        /// Write register.
        /// </summary>
        /// <param name="register">A register.</param>
        /// <param name="value">A value.</param>
        public void WriteRegister(Register register, uint value)
        {
            IOPort.Write32(indexPort, (uint)register);
            IOPort.Write32(valuePort, value);
        }

        /// <summary>
        /// Read register.
        /// </summary>
        /// <param name="register">A register.</param>
        /// <returns>uint value.</returns>
        public uint ReadRegister(Register register)
        {
            IOPort.Write32(indexPort, (uint)register);
            return IOPort.Read32(valuePort);
        }

        /// <summary>
        /// Get FIFO.
        /// </summary>
        /// <param name="cmd">FIFO command.</param>
        /// <returns>uint value.</returns>
        public uint GetFIFO(FIFO cmd) => fifoMemory[(uint)cmd];

        /// <summary>
        /// Set FIFO.
        /// </summary>
        /// <param name="cmd">Command.</param>
        /// <param name="value">Value.</param>
        /// <returns></returns>
        public uint SetFIFO(FIFO cmd, uint value) => fifoMemory[(uint)cmd] = value;

        /// <summary>
        /// Wait for FIFO.
        /// </summary>
        public void WaitForFifo()
        {
            WriteRegister(Register.Sync, 1);
            while (ReadRegister(Register.Busy) != 0) { }
        }

        /// <summary>
        /// Write to FIFO.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteToFifo(uint value)
        {
            if (GetFIFO(FIFO.NextCmd) == GetFIFO(FIFO.Max) - 4 && GetFIFO(FIFO.Stop) == GetFIFO(FIFO.Min) ||
                GetFIFO(FIFO.NextCmd) + 4 == GetFIFO(FIFO.Stop))
            {
                WaitForFifo();
            }

            SetFIFO((FIFO)GetFIFO(FIFO.NextCmd), value);
            SetFIFO(FIFO.NextCmd, GetFIFO(FIFO.NextCmd) + 4);

            if (GetFIFO(FIFO.NextCmd) == GetFIFO(FIFO.Max))
            {
                SetFIFO(FIFO.NextCmd, GetFIFO(FIFO.Min));
            }
        }

        public void* ReserveFIFO(uint bytes)
        {
            uint next = GetFIFO(FIFO.NextCmd);
            uint stop = GetFIFO(FIFO.Stop);
            uint min = GetFIFO(FIFO.Min);
            uint max = GetFIFO(FIFO.Max);

            uint space;
            if (next >= stop)
            {
                space = (max - next) + (stop - min);
            }
            else
            {
                space = stop - next;
            }

            // Wait if not enough contiguous space
            while (space < bytes)
            {
                WaitForFifo(); // give the SVGA device time to consume FIFO
                next = GetFIFO(FIFO.NextCmd);
                stop = GetFIFO(FIFO.Stop);
                if (next >= stop)
                {
                    space = (max - next) + (stop - min);
                }
                else
                {
                    space = stop - next;
                }
            }

            // Make sure contiguous region fits before end of buffer
            if (next + bytes > max)
            {
                // wrap to beginning of buffer
                SetFIFO(FIFO.NextCmd, min);
                next = min;
            }

            // Compute pointer into memory block
            void* ptr = (void*)(fifoMemory.Base + next); // hypothetical: Address property gives base address

            // Advance NEXT_CMD
            uint newNext = next + bytes;
            SetFIFO(FIFO.NextCmd, (newNext == max ? min : newNext));

            return ptr;
        }


        /// <summary>
        /// Update FIFO.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        public void Update(uint x, uint y, uint width, uint height)
        {
            WriteToFifo((uint)FIFOCommand.Update);
            WriteToFifo(x);
            WriteToFifo(y);
            WriteToFifo(width);
            WriteToFifo(height);
            WaitForFifo();
        }

        /// <summary>
        /// Update video memory.
        /// </summary>
        public void DoubleBufferUpdate()
        {
            videoMemory.MoveDown(FrameOffset, FrameSize, FrameSize);
            Update(0, 0, width, height);
        }

        /// <summary>
        /// Set pixel.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="color">Color.</param>
        /// <exception cref="Exception">Thrown on memory access violation.</exception>
        public void SetPixel(uint x, uint y, uint color)
        {
            videoMemory[(y * width + x) * depth + FrameSize] = color;
        }

        /// <summary>
        /// Get pixel.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <returns>uint value.</returns>
        /// <exception cref="Exception">Thrown on memory access violation.</exception>
        public uint GetPixel(uint x, uint y)
        {
            return videoMemory[(y * width + x) * depth + FrameSize];
        }

        /// <summary>
        /// Clear screen to specified color.
        /// </summary>
        /// <param name="color">Color.</param>
        /// <exception cref="Exception">Thrown on memory access violation.</exception>
        /// <exception cref="NotImplementedException">Thrown if VMWare SVGA 2 has no rectange copy capability</exception>
        public void Clear(uint color)
        {
            videoMemory.Fill(FrameSize, FrameSize, color);
        }

        /// <summary>
        /// Copy rectangle.
        /// </summary>
        /// <param name="x">Source X coordinate.</param>
        /// <param name="y">Source Y coordinate.</param>
        /// <param name="newX">Destination X coordinate.</param>
        /// <param name="newY">Destination Y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <exception cref="NotImplementedException">Thrown if VMWare SVGA 2 has no rectange copy capability</exception>
        public void Copy(uint x, uint y, uint newX, uint newY, uint width, uint height)
        {
            if ((capabilities & (uint)Capability.RectCopy) != 0)
            {
                WriteToFifo((uint)FIFOCommand.RECT_COPY);
                WriteToFifo(x);
                WriteToFifo(y);
                WriteToFifo(newX);
                WriteToFifo(newY);
                WriteToFifo(width);
                WriteToFifo(height);
                WaitForFifo();
            }
            else
            {
                throw new NotImplementedException("VMWareSVGAII Copy()");
            }
        }

        /// <summary>
        /// Fill rectangle.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        /// <param name="color">Color.</param>
        /// <exception cref="Exception">Thrown on memory access violation.</exception>
        /// <exception cref="NotImplementedException">Thrown if VMWare SVGA 2 has no rectange copy capability</exception>
        public void Fill(uint x, uint y, uint width, uint height, uint color)
        {
            if ((capabilities & (uint)Capability.RectFill) != 0)
            {
                WriteToFifo((uint)FIFOCommand.RECT_FILL);
                WriteToFifo(color);
                WriteToFifo(x);
                WriteToFifo(y);
                WriteToFifo(width);
                WriteToFifo(height);
                WaitForFifo();
            }
            else
            {
                if ((capabilities & (uint)Capability.RectCopy) != 0)
                {
                    // fill first line and copy it to all other
                    uint xTarget = x + width;
                    uint yTarget = y + height;

                    for (uint xTmp = x; xTmp < xTarget; xTmp++)
                    {
                        SetPixel(xTmp, y, color);
                    }
                    // refresh first line for copy process
                    Update(x, y, width, 1);
                    for (uint yTmp = y + 1; yTmp < yTarget; yTmp++)
                    {
                        Copy(x, y, x, yTmp, width, 1);
                    }
                }
                else
                {
                    uint xTarget = x + width;
                    uint yTarget = y + height;
                    for (uint xTmp = x; xTmp < xTarget; xTmp++)
                    {
                        for (uint yTmp = y; yTmp < yTarget; yTmp++)
                        {
                            SetPixel(xTmp, yTmp, color);
                        }
                    }
                    Update(x, y, width, height);
                }
            }
        }

        /// <summary>
        /// Define cursor.
        /// </summary>
        public void DefineCursor()
        {
            WaitForFifo();
            WriteToFifo((uint)FIFOCommand.DEFINE_CURSOR);
            WriteToFifo(0); // ID
            WriteToFifo(0); // Hotspot X
            WriteToFifo(0); // Hotspot Y
            WriteToFifo(2);
            WriteToFifo(2);
            WriteToFifo(1);
            WriteToFifo(1);

            for (int i = 0; i < 4; i++)
            {
                WriteToFifo(0);
            }

            for (int i = 0; i < 4; i++)
            {
                WriteToFifo(0xFFFFFF);
            }

            WaitForFifo();
        }

        /// <summary>
        /// Define alpha cursor.
        /// </summary>
        public void DefineAlphaCursor(uint width, uint height, int[] data)
        {
            WaitForFifo();
            WriteToFifo((uint)FIFOCommand.DEFINE_ALPHA_CURSOR);
            WriteToFifo(0); // ID
            WriteToFifo(0); // Hotspot X
            WriteToFifo(0); // Hotspot Y
            WriteToFifo(width); // Width
            WriteToFifo(height); // Height

            for (int i = 0; i < data.Length; i++)
            {
                WriteToFifo((uint)data[i]);
            }

            WaitForFifo();
        }

        /// <summary>
        /// Enable the SVGA Driver, only needed after Disable() has been called.
        /// </summary>
        public void Enable()
        {
            WriteRegister(Register.Enable, 1);
        }

        /// <summary>
        /// Disable the SVGA Driver, returns to text mode.
        /// </summary>
        public void Disable()
        {
            WriteRegister(Register.Enable, 0);
        }

        /// <summary>
        /// Sets the cursor position and draws it.
        /// </summary>
        /// <param name="visible">Visible.</param>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        public void SetCursor(bool visible, uint x, uint y)
        {
            WriteRegister(Register.CursorOn, (uint)(visible ? 1 : 0));
            WriteRegister(Register.CursorX, x);
            WriteRegister(Register.CursorY, y);
            WriteRegister(Register.CursorCount, ReadRegister(Register.CursorCount) + 1);
        }

        #endregion

        #region Fields

        /// <summary>
        /// Index port.
        /// </summary>
        private readonly ushort indexPort;
        /// <summary>
        /// Value port.
        /// </summary>
        private readonly ushort valuePort;

        /// <summary>
        /// Video memory block.
        /// </summary>
        public readonly MemoryBlock videoMemory;

        /// <summary>
        /// FIFO memory block.
        /// </summary>
        private MemoryBlock fifoMemory;

        /// <summary>
        /// PCI device.
        /// </summary>
        private readonly PCIDevice device;

        /// <summary>
        /// Height.
        /// </summary>
        private uint height;

        /// <summary>
        /// Width.
        /// </summary>
        private uint width;

        /// <summary>
        /// Depth.
        /// </summary>
        private uint depth;

        /// <summary>
        /// Capabilities.
        /// </summary>
        public readonly uint capabilities;

        public uint FrameSize;
        public uint FrameOffset;

        #endregion


    }
}

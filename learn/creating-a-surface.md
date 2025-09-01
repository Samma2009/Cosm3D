# Creating a context

A surface is an image that the gpu has access to, and that we can render to.

We will need two surfaces, one for Color and one for Depth.

```csharp
SVGA3dSurfaceImageId Color, Depth;

protected override void BeforeRun()
{
    // ...

    Color = canv.driver.DefineSurface(1280, 720, SVGA3dSurfaceFormat.SVGA3D_X8R8G8B8);
    Depth = canv.driver.DefineSurface(1280, 720, SVGA3dSurfaceFormat.SVGA3D_Z_D16);
}
```

A `SVGA3dSurfaceImageId` is the object type that contains the id of our surfaces, similarly to the canvas, our surfaces need a width and height and a format, the format in the case of our `Color` surface is `SVGA3D_X8R8G8B8`, indicating a 24 bit RGB color component with an unused 8 bit component. Our `Depth` surface is of format `SVGA3D_Z_D16`, indicating a 16 bit Depth component.

To render to the surfaces that we just made, we first have to set them as render targets.

```csharp
SVGA3dSurfaceImageId Color, Depth;

protected override void BeforeRun()
{
    // ...

    Color = canv.driver.DefineSurface(1280, 720, SVGA3dSurfaceFormat.SVGA3D_X8R8G8B8);
    Depth = canv.driver.DefineSurface(1280, 720, SVGA3dSurfaceFormat.SVGA3D_Z_D16);

    canv.driver.SetRenderTarget(context, SVGA3dRenderTargetType.Color, Color);
    canv.driver.SetRenderTarget(context, SVGA3dRenderTargetType.Depth, Depth);
}
```

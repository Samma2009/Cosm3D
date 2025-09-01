# Creating a context

Now that Cosmos and Cosm3D are setted up we can start coding.

The first thing to do is creating a 3D VMwareSVGAII canvas.

```csharp
namespace LearnCosm3D
{
    public class Kernel : Sys.Kernel
    {

        SVGAII3DCanvas canv;

        protected override void BeforeRun()
        {
            canv = new SVGAII3DCanvas(new Mode(1280,720,ColorDepth.ColorDepth32));
        }

        protected override void Run()
        {
        }
    }
}
```

the constructor for `SVGAII3DCanvas` takes one arument, that argument is a `Mode`.

a `Mode` is the resolution that we want to use. here we are creating a mode that is 1280x720 pixels in size and that has 32 bits of color depth.

Next up is defining a Context.

```csharp
public class Kernel : Sys.Kernel
{

    SVGAII3DCanvas canv;
    uint context;

    protected override void BeforeRun()
    {
        canv = new SVGAII3DCanvas(new Mode(1280,720,ColorDepth.ColorDepth32));
        context = canv.driver.DefineContext();
    }

    protected override void Run()
    {
    }
}
```

A context is an id that is used to distunguish between different 3D environments

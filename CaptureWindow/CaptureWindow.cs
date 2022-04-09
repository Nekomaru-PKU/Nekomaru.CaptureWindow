using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.DirectX;

using SharpDX;
using SharpDX.WIC;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using DXGIDevice    = SharpDX.DXGI.Device3;
using D3D11Device   = SharpDX.Direct3D11.Device;
using D3D11MapFlags = SharpDX.Direct3D11.MapFlags;

public static class CaptureWindow {
    public static void Capture(IntPtr WindowHandle, string OutputFileName) {
        var captureItem = CreateCaptureItemFromHWND(WindowHandle);
        using var d3dDevice = new D3D11Device(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport);
        using var device = CreateDirect3DDeviceFromSharpDXDevice(d3dDevice);
        using var framePool = Direct3D11CaptureFramePool.Create(
            device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            captureItem.Size);
        var session = framePool.CreateCaptureSession(captureItem);
        session.StartCapture();
        Direct3D11CaptureFrame frameTemp = null;
        while (frameTemp == null)
            frameTemp = framePool.TryGetNextFrame();
        using var frame  = frameTemp;
        using var bitmap = ToTexture2D(frame.Surface);
        using var stream = File.Open(OutputFileName, FileMode.Create);
        SaveTexture2DToPNG(
            bitmap,
            stream,
            d3dDevice.ImmediateContext);
    }

    static private readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [ComImport]
    [ComVisible(true)]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop {
        IntPtr CreateForWindow(
            [In] IntPtr window,
            [In] ref Guid iid);
    }

    private static GraphicsCaptureItem CreateCaptureItemFromHWND(IntPtr hwnd) {
        var factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
        var interop = (IGraphicsCaptureItemInterop)factory;
        var itemPointer = interop.CreateForWindow(hwnd, GraphicsCaptureItemGuid);
        var item = Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
        Marshal.Release(itemPointer);
        return item;
    }
    
    [ComImport]
    [ComVisible(true)]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDirect3DDxgiInterfaceAccess {
        IntPtr GetInterface([In] ref Guid iid);
    };

    static private readonly Guid ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    [DllImport(
        "d3d11.dll",
        EntryPoint          = "CreateDirect3D11DeviceFromDXGIDevice",
        CharSet             = CharSet.Unicode,
        SetLastError        = true,
        ExactSpelling       = true,
        CallingConvention   = CallingConvention.StdCall)]
    static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport(
        "d3d11.dll",
        EntryPoint          = "CreateDirect3D11SurfaceFromDXGISurface",
        CharSet             = CharSet.Unicode,
        SetLastError        = true,
        ExactSpelling       = true,
        CallingConvention   = CallingConvention.StdCall)]
    static extern UInt32 CreateDirect3D11SurfaceFromDXGISurface(IntPtr dxgiSurface, out IntPtr graphicsSurface);

    public static IDirect3DDevice CreateDirect3DDeviceFromSharpDXDevice(D3D11Device d3dDevice) {
        IDirect3DDevice device = null;
        using var dxgiDevice = d3dDevice.QueryInterface<DXGIDevice>();
        var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);
        if (hr == 0) {
            device = Marshal.GetObjectForIUnknown(pUnknown) as IDirect3DDevice;
            Marshal.Release(pUnknown);
        }
        else {
            Marshal.ThrowExceptionForHR((int)hr);
        }
        return device;
    }

    public static Texture2D ToTexture2D(IDirect3DSurface surface) {
        var access = (IDirect3DDxgiInterfaceAccess)surface;
        var d3dPointer = access.GetInterface(ID3D11Texture2D);
        var d3dSurface = new Texture2D(d3dPointer);
        return d3dSurface;
    }

    public static void SaveTexture2DToPNG(
        Texture2D     texture,
        Stream        stream,
        DeviceContext d3dContext) {
        using var textureCopy = new Texture2D(d3dContext.Device, new Texture2DDescription {
            Width = texture.Description.Width,
            Height = texture.Description.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = texture.Description.Format,
            Usage = ResourceUsage.Staging,
            SampleDescription = new SampleDescription(1, 0),
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            OptionFlags = ResourceOptionFlags.None
        });
        d3dContext.CopyResource(texture, textureCopy);

        var dataBox = d3dContext.MapSubresource(
            textureCopy,
            0,
            0,
            MapMode.Read,
            D3D11MapFlags.None,
            out var dataStream);
        var dataRectangle = new DataRectangle {
            DataPointer = dataStream.DataPointer,
            Pitch = dataBox.RowPitch
        };

        using var imagingFactory = new ImagingFactory();
        using var bitmap = new Bitmap(
            imagingFactory,
            texture.Description.Width,
            texture.Description.Height,
            PixelFormat.Format32bppBGRA,
            dataRectangle);
        stream.Position = 0;

        using var bitmapEncoder = new PngBitmapEncoder(imagingFactory, stream);
        using var bitmapFrameEncode = new BitmapFrameEncode(bitmapEncoder);
        bitmapFrameEncode.Initialize();
        bitmapFrameEncode.SetSize(bitmap.Size.Width, bitmap.Size.Height);
        var pixelFormat = PixelFormat.FormatDontCare;
        bitmapFrameEncode.SetPixelFormat(ref pixelFormat);
        bitmapFrameEncode.WriteSource(bitmap);
        bitmapFrameEncode.Commit();
        bitmapEncoder.Commit();

        d3dContext.UnmapSubresource(texture, 0);
    }
}

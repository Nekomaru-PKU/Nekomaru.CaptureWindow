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

namespace Nekomaru.CaptureWindowInternal;

internal static class Capturing {
    public static void CaptureWindowToPng(
        IntPtr window,
        string outputFileName) {
        if (! outputFileName.ToLower().EndsWith(".png"))
            throw new ArgumentException("Output file must be a PNG file.");
        using var stream  = File.Open(outputFileName, FileMode.Create);
        using var device  = new D3D11Device(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport);
        using var texture = CaptureWindowToTexture2D(window, device);
        SaveTexture2DToPNG(texture, stream, device.ImmediateContext);
    }

    private static Texture2D CaptureWindowToTexture2D(
        IntPtr      window,
        D3D11Device d3D11Device) {
        var       captureItem = CreateCaptureItemForWindow(window);
        using var device      = CreateID3DDeviceFromD3D11Device(d3D11Device);
        using var framePool   = Direct3D11CaptureFramePool.Create(
            device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            captureItem.Size);
        var session = framePool.CreateCaptureSession(captureItem);
        session.StartCapture();
        Direct3D11CaptureFrame frameTemp = null;
        while (frameTemp == null)
            frameTemp = framePool.TryGetNextFrame();
        using var frame = frameTemp;
        return CreateTexture2DFromID3DSurface(frame.Surface);
    }

    [ComImport]
    [ComVisible(true)]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
    }

    private static readonly Guid GraphicsCaptureItemGuid =
        new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    private static GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr window) {
        var factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
        var interop = (IGraphicsCaptureItemInterop)factory;
        var itemPointer = interop.CreateForWindow(window, GraphicsCaptureItemGuid);
        var item = Marshal.GetObjectForIUnknown(itemPointer) as GraphicsCaptureItem;
        Marshal.Release(itemPointer);
        return item;
    }

    [DllImport("d3d11.dll",
        EntryPoint        = "CreateDirect3D11DeviceFromDXGIDevice",
        CharSet           = CharSet.Unicode,
        SetLastError      = true,
        ExactSpelling     = true,
        CallingConvention = CallingConvention.StdCall)]
    private static extern UInt32 CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);

    private static IDirect3DDevice CreateID3DDeviceFromD3D11Device(D3D11Device d3dDevice) {
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

    [ComImport]
    [ComVisible(true)]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDirect3DDxgiInterfaceAccess {
        IntPtr GetInterface([In] ref Guid iid);
    };
    
    private static readonly Guid ID3D11Texture2D =
        new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    private static Texture2D CreateTexture2DFromID3DSurface(IDirect3DSurface surface) {
        var access = (IDirect3DDxgiInterfaceAccess)surface;
        var d3dPointer = access.GetInterface(ID3D11Texture2D);
        var d3dSurface = new Texture2D(d3dPointer);
        return d3dSurface;
    }

    private static void SaveTexture2DToPNG(
        Texture2D     texture,
        Stream        stream,
        DeviceContext d3dContext) {
        using var textureCopy = new Texture2D(
            d3dContext.Device,
            new Texture2DDescription {
                Width     = texture.Description.Width,
                Height    = texture.Description.Height,
                Format    = texture.Description.Format,
                Usage     = ResourceUsage.Staging,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                CpuAccessFlags    = CpuAccessFlags.Read,
                OptionFlags       = ResourceOptionFlags.None,
                BindFlags         = BindFlags.None,
            });
        d3dContext.CopyResource(texture, textureCopy);

        var dataBox = d3dContext.MapSubresource(
            textureCopy,
            0,
            0,
            MapMode.Read,
            D3D11MapFlags.None,
            out var dataStream);

        using var imagingFactory = new ImagingFactory();
        using var bitmap = new Bitmap(
            imagingFactory,
            texture.Description.Width,
            texture.Description.Height,
            PixelFormat.Format32bppBGRA,
            new DataRectangle {
                DataPointer = dataStream.DataPointer,
                Pitch       = dataBox.RowPitch
            });
        stream.Position = 0;

        using var bitmapEncoder     = new PngBitmapEncoder(imagingFactory, stream);
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

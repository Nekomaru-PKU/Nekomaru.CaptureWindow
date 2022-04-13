using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using static System.Runtime.InteropServices.Marshal;
using PInvoke;
using static PInvoke.User32;
using static PInvoke.DwmApi;
using static PInvoke.DwmApi.DWMWINDOWATTRIBUTE;

namespace Nekomaru.CaptureWindowInternal;

internal static class Cropping {
    public static void CropPngToWindow(IntPtr window, string fileName) {
        Debug.Assert(IsImageCaptured(window, fileName));
        CropPng(fileName, GetWindowCropRect(window));
    }

    public static void CropPngToWindowClientArea(IntPtr window, string fileName) {
        Debug.Assert(IsImageCaptured(window, fileName));
        CropPng(fileName, GetWindowClientAreaCropRect(window));
    }

    private static bool IsImageCaptured(IntPtr window, string fileName) {
        using var image = Image.FromFile(fileName);
        GetWindowRect(window, out var windowRect);
        return
            image.Width  == windowRect.right  - windowRect.left &&
            image.Height == windowRect.bottom - windowRect.top;
    }

    private static Rectangle GetWindowCropRect(IntPtr window) {
        var boundsRect = GetWindowBoundsRect(window);
        return new Rectangle(
            0,
            0,
            boundsRect.right  - boundsRect.left,
            boundsRect.bottom - boundsRect.top);
    }

    private static Rectangle GetWindowClientAreaCropRect(IntPtr window) {
        var boundsRect = GetWindowBoundsRect(window);
        var boundsOrigin = new POINT() {
            x = boundsRect.left,
            y = boundsRect.top
        };

        GetClientRect(window, out var clientRect);
        var clientOrigin = new POINT();
        ClientToScreen(window, ref clientOrigin);

        return new Rectangle(
            clientOrigin.x - boundsOrigin.x,
            clientOrigin.y - boundsOrigin.y,
            clientRect.right  - clientRect.left,
            clientRect.bottom - clientRect.top);
    }

    private static RECT GetWindowBoundsRect(IntPtr window) {
        var dataPointer = AllocHGlobal(SizeOf(typeof(RECT)));
        DwmGetWindowAttribute(
            window,
            DWMWA_EXTENDED_FRAME_BOUNDS,
            dataPointer,
            SizeOf(typeof(RECT)));
        var data = PtrToStructure<RECT>(dataPointer);
        FreeHGlobal(dataPointer);
        return data;
    }

    private static void CropPng(string fileName, Rectangle cropRect) {
        using var bitmapNew = new Bitmap(
            cropRect.Width,
            cropRect.Height);
        using (var bitmapOld = (Bitmap)Image.FromFile(fileName)) {
            using var graphics = Graphics.FromImage(bitmapNew);
            graphics.DrawImage(
                bitmapOld,
                new Rectangle(0, 0, bitmapNew.Width, bitmapNew.Height),
                cropRect,
                GraphicsUnit.Pixel);
        }
        bitmapNew.Save(fileName, ImageFormat.Png);
    }
}

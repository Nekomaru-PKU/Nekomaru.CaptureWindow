using System;
using Nekomaru.CaptureWindowInternal;
namespace Nekomaru;
public static class CaptureWindow {
    public static void CaptureWindowToPng(
        IntPtr window,
        string outputFileName) {
        Capturing.CaptureWindowToPng(window, outputFileName);
        Cropping .CropPngToWindow   (window, outputFileName);
    }

    public static void CaptureWindowClientAreaToPng(
        IntPtr window,
        string outputFileName) {
        Capturing.CaptureWindowToPng       (window, outputFileName);
        Cropping .CropPngToWindowClientArea(window, outputFileName);
    }
}

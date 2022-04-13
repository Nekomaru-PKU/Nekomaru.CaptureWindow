using System;
using System.Reflection;
using static Nekomaru.CaptureWindow;

if (args.Length == 2) {
    if (args[1].ToLower().EndsWith(".png")) {
        CaptureWindowClientAreaToPng((IntPtr)int.Parse(args[0]), args[1]);
        return 0;
    }
    else {
        Console.Error.WriteLine("Output file must be a PNG file.");
        return -1;
    }
}
else {
    Console.Error.WriteLine(
        "Usage: {0}.exe <windowHandle> <outputFileName>",
        Assembly.GetExecutingAssembly().GetName().Name);
    return -1;
}

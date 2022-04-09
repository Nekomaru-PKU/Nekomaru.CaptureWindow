using System;
if (args.Length == 2)
    CaptureWindow.Capture((IntPtr)int.Parse(args[0]), args[1]);
else
    Console.WriteLine("Usage: CaptureWindow.Cli.exe <windowHandle> <outputFileName>");

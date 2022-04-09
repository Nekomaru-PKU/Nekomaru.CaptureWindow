using System;
if (args.Length != 2) {
    Console.WriteLine("Usage: CaptureWindow.Cli.exe <windowHandle> <outputFileName>");
    Environment.Exit(-1);
}
if (! args[1].ToLower().EndsWith(".png")) {
    Console.WriteLine("Error: Output file must be a PNG file.");
    Environment.Exit(-1);
}
CaptureWindow.SaveToPNG((IntPtr)int.Parse(args[0]), args[1]);

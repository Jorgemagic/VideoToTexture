using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VideoToTexture.FFmpeg
{
    public class FFmpegBinariesHelper
    {
        internal static void RegisterFFmpegBinaries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var current = Environment.CurrentDirectory;
                var probe = Path.Combine("FFmpeg", "bin", Environment.Is64BitProcess ? "x64" : "x86");
                DynamicallyLoadedBindings.LibrariesPath = Path.Combine(current, probe);               
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                DynamicallyLoadedBindings.LibrariesPath = "/lib/x86_64-linux-gnu/";
            else
                throw new NotSupportedException(); // fell free add support for platform of your choose
        }
    }
}
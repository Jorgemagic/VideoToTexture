using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VideoToTexture.FFmpeg
{
    /// <summary>
    /// Provides a helper for registering FFmpeg binary paths dynamically based on the operating system.
    /// </summary>
    public class FFmpegBinariesHelper
    {
        /// <summary>
        /// Registers the appropriate FFmpeg binary path depending on the detected operating system.
        /// </summary>
        /// <remarks>
        /// - On Windows, it sets the path dynamically based on the process architecture (x64 or x86).
        /// - On Linux, it assumes the default FFmpeg library path.
        /// - On unsupported platforms, an exception is thrown.
        /// </remarks>
        /// <exception cref="NotSupportedException">Thrown if the operating system is not supported.</exception>
        public static void RegisterFFmpegBinaries()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Determine the current working directory
                var executionPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                // Construct the FFmpeg binaries path based on the process architecture
                var probe = Path.Combine("runtimes", "win-x64", "native");

                // Assign the dynamically constructed path to the FFmpeg library loader
                DynamicallyLoadedBindings.LibrariesPath = Path.Combine(executionPath, probe);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Assign the default library path for FFmpeg on Linux
                DynamicallyLoadedBindings.LibrariesPath = "/lib/x86_64-linux-gnu/";
            }
            else
            {
                // Unsupported platform: prompt the developer to extend support if needed
                throw new NotSupportedException();
            }
        }
    }
}

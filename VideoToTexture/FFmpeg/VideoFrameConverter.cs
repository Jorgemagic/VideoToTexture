using FFmpeg.AutoGen.Abstractions;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VideoToTexture.FFmpeg
{
    /// <summary>
    /// Provides functionality for converting video frames between different sizes and pixel formats using FFmpeg.
    /// </summary>
    public unsafe class VideoFrameConverter : IDisposable
    {
        private readonly IntPtr convertedFrameBufferPtr;
        private readonly Size destinationSize;
        private readonly byte_ptr4 dstData;
        private readonly int4 dstLinesize;
        private readonly SwsContext* pConvertContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoFrameConverter"/> class.
        /// </summary>
        /// <param name="sourceSize">The size of the source video frame.</param>
        /// <param name="sourcePixelFormat">The pixel format of the source frame.</param>
        /// <param name="destinationSize">The target size of the converted frame.</param>
        /// <param name="destinationPixelFormat">The pixel format of the converted frame.</param>
        /// <exception cref="ApplicationException">Thrown when the conversion context cannot be initialized.</exception>
        public VideoFrameConverter(Size sourceSize, AVPixelFormat sourcePixelFormat, Size destinationSize, AVPixelFormat destinationPixelFormat)
        {
            this.destinationSize = destinationSize;

            // Create and initialize the FFmpeg scaling context for conversion
            this.pConvertContext = ffmpeg.sws_getContext(
                    sourceSize.Width,
                    sourceSize.Height,
                    sourcePixelFormat,
                    destinationSize.Width,
                    destinationSize.Height,
                    destinationPixelFormat,
                    ffmpeg.SWS_FAST_BILINEAR,
                    null,
                    null,
                    null);
            if (this.pConvertContext == null)
            {
                throw new ApplicationException("Could not initialize the conversion context.");
            }

            // Allocate memory for the converted frame buffer
            var convertedFrameBufferSize = ffmpeg.av_image_get_buffer_size(
                        destinationPixelFormat,
                        destinationSize.Width,
                        destinationSize.Height,
                        1);
            this.convertedFrameBufferPtr = Marshal.AllocHGlobal(convertedFrameBufferSize);
            this.dstData = new byte_ptr4();
            this.dstLinesize = new int4();

            // Fill the destination frame buffer arrays
            ffmpeg.av_image_fill_arrays(
                ref this.dstData,
                ref this.dstLinesize,
                (byte*)this.convertedFrameBufferPtr,
                destinationPixelFormat,
                destinationSize.Width,
                destinationSize.Height,
                1);
        }

        /// <summary>
        /// Releases the unmanaged resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            Marshal.FreeHGlobal(this.convertedFrameBufferPtr);
            ffmpeg.sws_freeContext(this.pConvertContext);
        }

        /// <summary>
        /// Converts a source video frame to the destination format and size.
        /// </summary>
        /// <param name="sourceFrame">The input video frame to be converted.</param>
        /// <returns>A new <see cref="AVFrame"/> representing the converted frame.</returns>
        public AVFrame Convert(AVFrame sourceFrame)
        {
            // Perform the frame conversion using FFmpeg
            ffmpeg.sws_scale(
                this.pConvertContext,
                sourceFrame.data,
                sourceFrame.linesize,
                0,
                sourceFrame.height,
                this.dstData,
                this.dstLinesize);

            // Create a new AVFrame with the converted data
            var data = new byte_ptr8();
            data.UpdateFrom(this.dstData);
            var linesize = new int8();
            linesize.UpdateFrom(this.dstLinesize);

            return new AVFrame
            {
                data = data,
                linesize = linesize,
                width = this.destinationSize.Width,
                height = this.destinationSize.Height,
            };
        }
    }
}


using FFmpeg.AutoGen.Abstractions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VideoToTexture.FFmpeg
{
    /// <summary>
    /// Handles video stream decoding using FFmpeg, supporting hardware acceleration and metadata retrieval.
    /// </summary>
    public unsafe class VideoStreamDecoder : IDisposable
    {
        private readonly AVCodecContext* pCodecContext;
        private readonly AVFormatContext* pFormatContext;
        private readonly AVFrame* pFrame;
        private readonly AVPacket* pPacket;
        private readonly AVFrame* receivedFrame;
        private readonly int streamIndex;

        /// <summary>
        /// Gets the name of the codec used for decoding.
        /// </summary>
        public string CodecName { get; }

        /// <summary>
        /// Gets the size of the video frame.
        /// </summary>
        public Size FrameSize { get; }

        /// <summary>
        /// Gets the pixel format of the video frame.
        /// </summary>
        public AVPixelFormat PixelFormat { get; }

        /// <summary>
        /// Gets the frame rate of the video.
        /// </summary>
        public float FrameRate
        {
            get
            {
                if (this.pFormatContext != null)
                {
                    var frameRate = pFormatContext->streams[0]->avg_frame_rate;
                    if (frameRate.den > 0)
                    {
                        return frameRate.num / (float)frameRate.den;
                    }
                    else
                    {
                        return 30.0f;
                    }
                }

                return -1;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoStreamDecoder"/> class.
        /// </summary>
        /// <param name="videoPath">The path to the video file.</param>
        /// <param name="hWDeviceType">The hardware acceleration device type.</param>
        public VideoStreamDecoder(string videoPath, AVHWDeviceType hWDeviceType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
        {
            this.pFormatContext = ffmpeg.avformat_alloc_context();
            this.receivedFrame = ffmpeg.av_frame_alloc();
            var pFormatContext = this.pFormatContext;

            int ret = ffmpeg.avformat_open_input(&pFormatContext, videoPath, null, null);
            if (ret < 0)
            {
                throw new Exception($"FFmpeg: Failed to open input file: {videoPath}. Please verify the file path and format.");
            }

            ret = ffmpeg.avformat_find_stream_info(this.pFormatContext, null);
            if (ret < 0)
            {
                throw new Exception($"FFmpeg: Failed to retrieve stream information from the input file. Ensure the file is valid and not corrupted.");
            }

            AVCodec* codec = null;
            ret = ffmpeg.av_find_best_stream(this.pFormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &codec, 0);
            if (ret < 0)
            {
                throw new Exception($"FFmpeg: No valid video stream found in the input file. Check if the file contains a supported video stream.");
            }

            this.streamIndex = ret;

            this.pCodecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (this.pCodecContext == null)
            {
                throw new Exception("FFmpeg: Failed to allocate memory for the codec context.");
            }

            if (hWDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
            {
                ret = ffmpeg.av_hwdevice_ctx_create(&this.pCodecContext->hw_device_ctx, hWDeviceType, null, null, 0);
                if (ret < 0)
                {
                    throw new Exception($"FFmpeg: Failed to create hardware device context for device type {hWDeviceType}. Verify that your hardware supports this acceleration. ");
                }
            }

            ret = ffmpeg.avcodec_parameters_to_context(this.pCodecContext, this.pFormatContext->streams[this.streamIndex]->codecpar);
            if (ret < 0)
            {
                throw new Exception($"FFmpeg: Failed to copy codec parameters to the decoder context. The stream's codec parameters may be invalid. ");
            }

            ret = ffmpeg.avcodec_open2(this.pCodecContext, codec, null);
            if (ret < 0)
            {
                throw new Exception($"FFmpeg: Failed to open codec {ffmpeg.avcodec_get_name(codec->id)}. Ensure that the codec is supported and correctly installed.");
            }

            this.CodecName = ffmpeg.avcodec_get_name(codec->id);
            this.FrameSize = new Size(this.pCodecContext->width, this.pCodecContext->height);
            this.PixelFormat = this.pCodecContext->pix_fmt;

            this.pPacket = ffmpeg.av_packet_alloc();
            this.pFrame = ffmpeg.av_frame_alloc();
        }

        /// <summary>
        /// Releases resources used by the decoder.
        /// </summary>
        public void Dispose()
        {
            var pFrame = this.pFrame;
            ffmpeg.av_frame_free(&pFrame);

            var pPacket = this.pPacket;
            ffmpeg.av_packet_free(&pPacket);

            ////ffmpeg.avcodec_close(_pCodecContext);
            var pCodecContext = this.pCodecContext;
            ffmpeg.avcodec_free_context(&pCodecContext);

            var pFormatContext = this.pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);
        }

        /// <summary>
        /// Attempts to decode the next video frame.
        /// </summary>
        /// <param name="frame">The output decoded frame.</param>
        /// <param name="loop">Whether to loop the video when it reaches the end.</param>
        /// <returns>True if a frame was successfully decoded, otherwise false.</returns>
        public bool TryDecodeNextFrame(out AVFrame frame, bool loop)
        {
            ffmpeg.av_frame_unref(this.pFrame);
            ffmpeg.av_frame_unref(this.receivedFrame);
            int error;

            do
            {
                try
                {
                    do
                    {
                        ffmpeg.av_packet_unref(this.pPacket);
                        error = ffmpeg.av_read_frame(this.pFormatContext, this.pPacket);

                        if (loop && error < 0)
                        {
                            this.Reset();
                        }

                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *this.pFrame;
                            return false;
                        }

                        if (error < 0)
                        {
                            throw new Exception($"FFmpeg: Failed to read frame from input.");
                        }
                    }
                    while (this.pPacket->stream_index != this.streamIndex);

                    error = ffmpeg.avcodec_send_packet(this.pCodecContext, this.pPacket);
                    if (error < 0)
                    {
                        throw new Exception($"FFmpeg: Failed to send packet to decoder.");
                    }
                }
                finally
                {
                    ffmpeg.av_packet_unref(this.pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(this.pCodecContext, this.pFrame);

                if (error == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                {
                    continue;
                }
                else if (error < 0)
                {
                    throw new Exception($"FFmpeg: Failed to receive frame from decoder.");
                }

                break;
            }
            while (true);

            if (this.pCodecContext->hw_device_ctx != null)
            {
                error = ffmpeg.av_hwframe_transfer_data(this.receivedFrame, this.pFrame, 0);
                if (error < 0)
                {
                    throw new Exception($"FFmpeg: Failed to transfer hardware frame data.");
                }

                frame = *this.receivedFrame;
            }
            else
            {
                frame = *this.pFrame;
            }

            return true;
        }

        /// <summary>
        /// Retrieves metadata from the video context.
        /// </summary>
        /// <returns>A dictionary containing metadata key-value pairs.</returns>
        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();

            while ((tag = ffmpeg.av_dict_get(pFormatContext->metadata, string.Empty, tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                if (key != null && value != null)
                {
                    result.Add(key, value);
                }
            }

            return result;
        }

        /// <summary>
        /// Resets the video stream to the beginning.
        /// </summary>
        public void Reset()
        {
            ffmpeg.av_seek_frame(this.pFormatContext, this.streamIndex, 0, ffmpeg.AVSEEK_FLAG_BACKWARD);
        }
    }
}

using Evergine.Common.Graphics;
using Evergine.Components.Graphics3D;
using Evergine.Framework;
using Evergine.Framework.Graphics.Materials;
using FFmpeg.AutoGen;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VideoToTexture.FFmpeg;

namespace VideoToTexture.Components
{
    public class VideoPlayer : Behavior
    {
        [BindService]
        private GraphicsContext graphicsContext;

        [BindComponent]
        private MaterialComponent materialComponent = null;

        public string VideoPath { get; set; }

        public bool Autoplay { get; set; }

        public bool Loop { get; set; }

        private Texture screenTexture;
        
        private VideoStreamDecoder videoStreamDecoder;
        private VideoFrameConverter videoFrameConverter;
        private AVHWDeviceType hwdevice = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2;
        private int frameNumber;
        private TimeSpan elapsedTime = TimeSpan.Zero;
        private TimeSpan targetInterval;

        private bool playing = false;

        protected override bool OnAttached()
        {
            var result = base.OnAttached();

            if (!Application.Current.IsEditor)
            {                                                
                // FFMPEG
                FFmpegBinariesHelper.RegisterFFmpegBinaries();

                Debug.WriteLine("Current directory: " + Environment.CurrentDirectory);
                Debug.WriteLine("Running in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");
                Debug.WriteLine($"FFmpeg version info: {ffmpeg.av_version_info()}");
                Debug.WriteLine($"LIBAVFORMAT Version: {ffmpeg.LIBAVFORMAT_VERSION_MAJOR}.{ffmpeg.LIBAVFORMAT_VERSION_MINOR}");
            }

            this.playing = this.Autoplay;

            return result;
        }

        protected unsafe override void Update(TimeSpan gameTime)
        {
            if (this.playing)
            {
                if (this.videoStreamDecoder == null)
                {
                    string filePath = Path.Combine(Environment.CurrentDirectory, "Content", this.VideoPath);
                    this.videoStreamDecoder = new VideoStreamDecoder(filePath, this.hwdevice);

                    Debug.WriteLine($"codec name: {this.videoStreamDecoder.CodecName}");

                    var info = this.videoStreamDecoder.GetContextInfo();
                    info.ToList().ForEach(x => Debug.WriteLine($"{x.Key} = {x.Value}"));

                    StandardMaterial material = new StandardMaterial(this.materialComponent.Material);
                    var textureDesc = new TextureDescription()
                    {
                        Type = TextureType.Texture2D,
                        Width = (uint)this.videoStreamDecoder.FrameSize.Width,
                        Height = (uint)this.videoStreamDecoder.FrameSize.Height,
                        Depth = 1,
                        ArraySize = 1,
                        Faces = 1,
                        Usage = ResourceUsage.Default,
                        CpuAccess = ResourceCpuAccess.None,
                        Flags = TextureFlags.ShaderResource,
                        Format = PixelFormat.R8G8B8A8_UNorm,
                        MipLevels = (uint)1,
                        SampleCount = TextureSampleCount.None,
                    };
                    this.screenTexture = graphicsContext.Factory.CreateTexture(ref textureDesc);
                    material.BaseColorTexture = this.screenTexture;

                    var sourceSize = this.videoStreamDecoder.FrameSize;
                    var sourcePixelFormat = this.hwdevice == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE
                        ? this.videoStreamDecoder.PixelFormat
                        : this.GetHWPixelFormat(this.hwdevice);
                    var destinationSize = sourceSize;
                    var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_RGBA;
                    this.videoFrameConverter = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat);

                    this.frameNumber = 0;
                    var fps = this.videoStreamDecoder.FrameRate;
                    this.targetInterval = TimeSpan.FromSeconds(1.0f / fps);
                }

                elapsedTime += gameTime;

                if (elapsedTime >= targetInterval)
                {               
                    while(elapsedTime >= targetInterval)
                    {
                        elapsedTime-= targetInterval;
                    }

                    if (this.videoStreamDecoder.TryDecodeNextFrame(out var frame, this.Loop))
                    {
                        this.frameNumber++;
                        var convertedFrame = this.videoFrameConverter.Convert(frame);

                        this.graphicsContext.UpdateTextureData(this.screenTexture,
                                                               (nint)convertedFrame.data[0],
                                                               (uint)(convertedFrame.width * convertedFrame.height * 4),
                                                               0);
                    }
                }
            }
        }

        public void Play(bool loop = false)
        {
            this.playing = true;
            this.Loop = loop;
        }

        public void Stop()
        {
            if (this.playing)
            {
                this.playing = false;
                this.videoStreamDecoder.Reset();
            }
        }

        public void Pause()
        {
            this.playing = false;
        }

        private AVPixelFormat GetHWPixelFormat(AVHWDeviceType hWDevice)
        {
            return hWDevice switch
            {
                AVHWDeviceType.AV_HWDEVICE_TYPE_NONE => AVPixelFormat.AV_PIX_FMT_NONE,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU => AVPixelFormat.AV_PIX_FMT_VDPAU,
                AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA => AVPixelFormat.AV_PIX_FMT_CUDA,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI => AVPixelFormat.AV_PIX_FMT_VAAPI,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2 => AVPixelFormat.AV_PIX_FMT_NV12,
                AVHWDeviceType.AV_HWDEVICE_TYPE_QSV => AVPixelFormat.AV_PIX_FMT_QSV,
                AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX => AVPixelFormat.AV_PIX_FMT_VIDEOTOOLBOX,
                AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA => AVPixelFormat.AV_PIX_FMT_NV12,
                AVHWDeviceType.AV_HWDEVICE_TYPE_DRM => AVPixelFormat.AV_PIX_FMT_DRM_PRIME,
                AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL => AVPixelFormat.AV_PIX_FMT_OPENCL,
                AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC => AVPixelFormat.AV_PIX_FMT_MEDIACODEC,
                _ => AVPixelFormat.AV_PIX_FMT_NONE
            };
        }
    }
}

using Evergine.Common.Graphics;
using Evergine.Components.Graphics3D;
using Evergine.Framework;
using Evergine.Framework.Graphics.Materials;
using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLoaded;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VideoToTexture.FFmpeg;

namespace VideoToTexture.Components
{
    public class VideoPlayer : Behavior
    {
        public enum DeviceType
        {
            NONE,
            VDPAU,
            CUDA,
            VAAPI,
            DXVA2,
            QSV,
            VIDEOTOOLBOX,
            D3D11VA,
            DRM,
            OPENCL,
            MEDIACODEC,
            VULKAN
        }

        [BindService]
        private GraphicsContext graphicsContext = null;

        [BindComponent]
        private MaterialComponent materialComponent = null;

        public DeviceType HWDevice
        {
            get
            {
                switch (this.hwDevice)
                {
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU:
                        return DeviceType.VDPAU;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA:
                        return DeviceType.CUDA;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI:
                        return DeviceType.VAAPI;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2:
                        return DeviceType.DXVA2;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_QSV:
                        return DeviceType.QSV;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX:
                        return DeviceType.VIDEOTOOLBOX;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA:
                        return DeviceType.D3D11VA;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_DRM:
                        return DeviceType.DRM;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL:
                        return DeviceType.OPENCL;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC:
                        return DeviceType.MEDIACODEC;
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN:
                        return DeviceType.VULKAN;   
                    case AVHWDeviceType.AV_HWDEVICE_TYPE_NONE:
                    default:
                        return DeviceType.NONE;                        
                }
            }

            set
            {
                switch (value)
                {
                    case DeviceType.NONE:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
                        break;
                    case DeviceType.VDPAU:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_VDPAU;
                        break;
                    case DeviceType.CUDA:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA;
                        break;
                    case DeviceType.VAAPI:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_VAAPI;
                        break;
                    case DeviceType.DXVA2:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2;
                        break;
                    case DeviceType.QSV:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_QSV;
                        break;
                    case DeviceType.VIDEOTOOLBOX:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_VIDEOTOOLBOX;
                        break;
                    case DeviceType.D3D11VA:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA;
                        break;
                    case DeviceType.DRM:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_DRM;
                        break;
                    case DeviceType.OPENCL:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_OPENCL;
                        break;
                    case DeviceType.MEDIACODEC:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_MEDIACODEC;
                        break;
                    case DeviceType.VULKAN:
                        this.hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_VULKAN;
                        break;                    
                }
            }
        }
        public string VideoPath { get; set; }

        public bool Autoplay { get; set; }

        public bool Loop { get; set; }

        private Texture screenTexture;

        private AVHWDeviceType hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2;
        private VideoStreamDecoder videoStreamDecoder;
        private VideoFrameConverter videoFrameConverter;        
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
                Debug.WriteLine("Current directory: " + Environment.CurrentDirectory);
                Debug.WriteLine("Running in {0}-bit mode.", Environment.Is64BitProcess ? "64" : "32");

                FFmpegBinariesHelper.RegisterFFmpegBinaries();
                DynamicallyLoadedBindings.Initialize();

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
                    this.videoStreamDecoder = new VideoStreamDecoder(filePath, this.hwDevice);

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
                    var sourcePixelFormat = this.hwDevice == AVHWDeviceType.AV_HWDEVICE_TYPE_NONE
                        ? this.videoStreamDecoder.PixelFormat
                        : this.GetHWPixelFormat(this.hwDevice);
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

using Evergine.Common.Attributes;
using Evergine.Common.Graphics;
using Evergine.Common.IO;
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
    /// <summary>
    /// Handles video playback and streaming, converting video frames into textures for rendering.
    /// </summary>
    public class VideoPlayer : Behavior
    {
        /// <summary>
        /// Occurs when the video begins playing.
        /// </summary>
        public event EventHandler Playing;

        /// <summary>
        /// Occurs when the video is paused.
        /// </summary>
        public event EventHandler Paused;

        /// <summary>
        /// Occurs when the video stops playing.
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Defines supported hardware acceleration device types.
        /// </summary>
        public enum DeviceType
        {
            /// <summary> No hardware acceleration. </summary>
            NONE,

            /// <summary> VDPAU (Video Decode and Presentation API for Unix). </summary>
            VDPAU,

            /// <summary> NVIDIA CUDA hardware acceleration. </summary>
            CUDA,

            /// <summary> VA-API (Video Acceleration API) for Linux systems. </summary>
            VAAPI,

            /// <summary> DXVA2 (DirectX Video Acceleration 2) for Windows. </summary>
            DXVA2,

            /// <summary> Intel Quick Sync Video. </summary>
            QSV,

            /// <summary> VideoToolbox hardware acceleration for macOS. </summary>
            VIDEOTOOLBOX,

            /// <summary> D3D11VA (Direct3D 11 Video Acceleration). </summary>
            D3D11VA,

            /// <summary> DRM (Direct Rendering Manager) for Linux. </summary>
            DRM,

            /// <summary> OpenCL hardware acceleration. </summary>
            OPENCL,

            /// <summary> Android MediaCodec hardware acceleration. </summary>
            MEDIACODEC,

            /// <summary> Vulkan-based video acceleration. </summary>
            VULKAN,
        }

        /// <summary>
        /// Specifies the different playback states of a video.
        /// </summary>
        public enum VideoStateType
        {
            /// <summary> The video is not currently playing. </summary>
            Stopped,

            /// <summary> The video is actively playing. </summary>
            Playing,

            /// <summary> The video playback is temporarily halted. </summary>
            Paused,
        }

        [BindService]
        private GraphicsContext graphicsContext = null;

        private MaterialComponent materialComponent = null;

        [BindService]
        private AssetsDirectory assetsDirectory = null;

        private Texture videoTexture = null;

        private string videoPath;

        /// <summary>
        /// Gets or sets the hardware device type used for video decoding.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the path to the video file.
        /// </summary>
        public string VideoPath
        {
            get => this.videoPath;
            set
            {
                this.videoPath = value;
                this.playing = false;
                this.frameNumber = 0;
                this.VideoState = VideoStateType.Stopped;

                this.videoStreamDecoder?.Dispose();
                this.videoStreamDecoder = null;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the video should autoplay on initialization.
        /// </summary>
        public bool Autoplay { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the video should loop when reaching the end.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Gets the videoplayer state.
        /// </summary>
        [IgnoreEvergine]
        [DontRenderProperty]
        public VideoStateType VideoState { get; private set; }

        /// <summary>
        /// Gets Video width in pixels.
        /// </summary>
        [IgnoreEvergine]
        [DontRenderProperty]
        public int VideoWidth { get; private set; }

        /// <summary>
        /// Gets Video height in pixels.
        /// </summary>
        [IgnoreEvergine]
        [DontRenderProperty]
        public int VideoHeight { get; private set; }

        /// <summary>
        /// Gets the video texture generated.
        /// </summary>
        public Texture VideoTexture { get => this.videoTexture; }

        private AVHWDeviceType hwDevice = AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2;
        private VideoStreamDecoder videoStreamDecoder;
        private VideoFrameConverter videoFrameConverter;
        private int frameNumber;
        private TimeSpan elapsedTime = TimeSpan.Zero;
        private TimeSpan targetInterval;

        private bool playing = false;

        /// <inheritdoc/>
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

            this.materialComponent = this.Owner.FindComponent<MaterialComponent>();

            this.playing = this.Autoplay;

            this.VideoState = VideoStateType.Stopped;

            return result;
        }

        /// <inheritdoc/>
        protected unsafe override void Update(TimeSpan gameTime)
        {
            if (this.playing)
            {
                if (this.videoStreamDecoder == null)
                {
                    string filePath = Path.Combine(this.assetsDirectory.RootPath, this.VideoPath);
                    this.videoStreamDecoder = new VideoStreamDecoder(filePath, this.hwDevice);

                    Debug.WriteLine($"codec name: {this.videoStreamDecoder.CodecName}");

                    var info = this.videoStreamDecoder.GetContextInfo();
                    info.ToList().ForEach(x => Debug.WriteLine($"{x.Key} = {x.Value}"));

                    this.VideoWidth = this.videoStreamDecoder.FrameSize.Width;
                    this.VideoHeight = this.videoStreamDecoder.FrameSize.Height;

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
                        MipLevels = 1,
                        SampleCount = TextureSampleCount.None,
                    };
                    this.videoTexture = this.graphicsContext.Factory.CreateTexture(ref textureDesc);

                    if (this.materialComponent != null)
                    {
                        StandardMaterial material = new StandardMaterial(this.materialComponent.Material);
                        material.BaseColorTexture = this.videoTexture;
                    }

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

                this.elapsedTime += gameTime;

                if (this.elapsedTime >= this.targetInterval)
                {
                    while (this.elapsedTime >= this.targetInterval)
                    {
                        this.elapsedTime -= this.targetInterval;
                    }

                    if (this.videoStreamDecoder.TryDecodeNextFrame(out var frame, this.Loop))
                    {
                        this.frameNumber++;
                        var convertedFrame = this.videoFrameConverter.Convert(frame);

                        this.graphicsContext.UpdateTextureData(
                                                               this.videoTexture,
                                                               (IntPtr)convertedFrame.data[0],
                                                               (uint)(convertedFrame.width * convertedFrame.height * 4),
                                                               0);
                    }
                }
            }
        }

        /// <summary>
        /// Starts video playback.
        /// </summary>
        public void Play()
        {
            this.playing = true;
            this.VideoState = VideoStateType.Playing;
            this.Playing?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Stops video playback and resets the decoder.
        /// </summary>
        public void Stop()
        {
            if (this.playing)
            {
                this.playing = false;
                this.frameNumber = 0;
                this.videoStreamDecoder.Reset();
                this.VideoState = VideoStateType.Stopped;
                this.Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Pauses video playback.
        /// </summary>
        public void Pause()
        {
            this.playing = false;
            this.VideoState = VideoStateType.Paused;
            this.Paused?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Initializes the video decoder and sets up the texture for rendering.
        /// </summary>
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

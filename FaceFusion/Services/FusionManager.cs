/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using Blake.NUI.WPF.Utility;
using GalaSoft.MvvmLight;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.Fusion;
using FaceFusion.Extensions;

namespace FaceFusion.Services
{
    public class FusionManager : ViewModelBase, IDisposable
    {
        #region Fields

        private bool savingMesh;

        private AudioManager _audioManager;

        private SynchronizationContext _syncContext = SynchronizationContext.Current;

        private Pool<FusionWorkItem, KinectFormat> _fusionWorkItemPool;
        private WorkQueue<FusionWorkItem> _fusionWorkQueue;

        private bool disposed;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorFusionBitmap;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private int[] colorPixels;

        private WriteableBitmap _residualWritableBitmap;
        private byte[] _residualImageData;

        bool _isFusionInitialized;

        float _alignmentEnergy;
        float _averageResidual;

        Vector3 _volumeCenter = new Vector3();
        Vector3 _currentVolumeCenter = new Vector3();

        /// <summary>
        /// The reconstruction volume voxel density in voxels per meter (vpm)
        /// 1000mm / 256vpm = ~3.9mm/voxel
        /// </summary>
        private const int VoxelsPerMeter = 384;

        /// <summary>
        /// The reconstruction volume voxel resolution in the X axis
        /// At a setting of 256vpm the volume is 512 / 256 = 2m wide
        /// </summary>
        private const int VoxelResolutionX = 128;

        /// <summary>
        /// The reconstruction volume voxel resolution in the Y axis
        /// At a setting of 256vpm the volume is 384 / 256 = 1.5m high
        /// </summary>
        private const int VoxelResolutionY = 128;

        /// <summary>
        /// The reconstruction volume voxel resolution in the Z axis
        /// At a setting of 256vpm the volume is 512 / 256 = 2m deep
        /// </summary>
        private const int VoxelResolutionZ = 128;

        /// <summary>
        /// The transformation between the world and camera view coordinate system
        /// </summary>
        private Matrix4 worldToCameraTransform;

        /// <summary>
        /// The reconstruction volume processor type. This parameter sets whether AMP or CPU processing
        /// is used. Note that CPU processing will likely be too slow for real-time processing.
        /// </summary>
        private const ReconstructionProcessor ProcessorType = ReconstructionProcessor.Amp;

        /// <summary>
        /// The zero-based device targetIndex to choose for reconstruction processing if the 
        /// ReconstructionProcessor AMP options are selected.
        /// Here we automatically choose a device to use for processing by passing -1, 
        /// </summary>
        private const int DeviceToUse = -1;

        /// <summary>
        /// The default transformation between the world and volume coordinate system
        /// </summary>
        private Matrix4 defaultWorldToVolumeTransform;

        /// <summary>
        /// The Kinect Fusion volume
        /// </summary>
        private ColorReconstruction volume;

        /// <summary>
        /// Parameter to translate the reconstruction based on the minimum depth setting. When set to
        /// false, the reconstruction volume +Z axis starts at the camera lens and extends into the scene.
        /// Setting this true in the constructor will move the volume forward along +Z away from the
        /// camera by the minimum depth threshold to enable capture of very small reconstruction volumes
        /// by setting a non-identity world-volume transformation in the ResetReconstruction call.
        /// Small volumes should be shifted, as the Kinect hardware has a minimum sensing limit of ~0.35m,
        /// inside which no valid depth is returned, hence it is difficult to initialize and track robustly  
        /// when the majority of a small volume is inside this distance.
        /// </summary>
        private bool translateResetPose = true;

        /// <summary>
        /// Intermediate storage for the depth float data converted from depth image frame
        /// </summary>
        private FusionFloatImageFrame depthFloatBuffer;

        float[] _residualData;
        private FusionFloatImageFrame residualFloatBuffer;

        /// <summary>
        /// Intermediate storage for the point cloud data converted from depth float image frame
        /// </summary>
        private FusionPointCloudImageFrame pointCloudBuffer;

        /// <summary>
        /// Raycast shaded surface image
        /// </summary>
        private FusionColorImageFrame shadedSurfaceColorFrame;

        /// <summary>
        /// Minimum depth distance threshold in meters. Depth pixels below this value will be
        /// returned as invalid (0). Min depth must be positive or 0.
        /// </summary>
        private float minDepthClip = FusionDepthProcessor.DefaultMinimumDepth;

        /// <summary>
        /// Maximum depth distance threshold in meters. Depth pixels above this value will be
        /// returned as invalid (0). Max depth must be greater than 0.
        /// </summary>
        private float maxDepthClip = FusionDepthProcessor.DefaultMaximumDepth;

        /// <summary>
        /// The tracking error count
        /// </summary>
        private int trackingErrorCount;

        /// <summary>
        /// The sensor depth frame data length
        /// </summary>
        private int frameDataLength;

        /// <summary>
        /// Max tracking error count, we will reset the reconstruction if tracking errors
        /// reach this number
        /// </summary>
        private const int MaxTrackingErrors = 100;

        /// <summary>
        /// If set true, will automatically reset the reconstruction when MaxTrackingErrors have occurred
        /// </summary>
        private const bool AutoResetReconstructionWhenLost = false;

        /// <summary>
        /// The integration weight.
        /// </summary>
        public const int IntegrationWeight = 145;

        KinectFormat _currentFormat;

        #endregion

        #region Properties

        public double CurrentRotationDegrees { get; set; }
        public double RotationRateInDegrees { get; set; }

        #region ProcessedFrameCount

        /// <summary>
        /// The count of the frames processed in the FPS interval
        /// </summary>
        public int ProcessedFrameCount { get; set; }

        #endregion

        #region KinectSensor

        /// <summary>
        /// The <see cref="KinectSensor" /> property's name.
        /// </summary>
        public const string KinectSensorPropertyName = "KinectSensor";

        private KinectSensor _kinectSensor = null;

        /// <summary>
        /// Gets the KinectSensor property.
        /// </summary>
        public KinectSensor KinectSensor
        {
            get
            {
                return _kinectSensor;
            }

            set
            {
                if (_kinectSensor == value)
                {
                    return;
                }

                var oldValue = _kinectSensor;
                _kinectSensor = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(KinectSensorPropertyName);
            }
        }

        #endregion

        #region FusionOutputImage

        /// <summary>
        /// The <see cref="FusionOutputImage" /> property's name.
        /// </summary>
        public const string FusionOutputImagePropertyName = "FusionOutputImage";

        private ImageSource _fusionOutputImage = null;

        /// <summary>
        /// Gets the FusionOutputImage property.
        /// </summary>
        public ImageSource FusionOutputImage
        {
            get
            {
                return _fusionOutputImage;
            }

            set
            {
                if (_fusionOutputImage == value)
                {
                    return;
                }

                var oldValue = _fusionOutputImage;
                _fusionOutputImage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(FusionOutputImagePropertyName);
            }
        }

        #endregion

        #region ResidualImage

        /// <summary>
        /// The <see cref="ResidualImage" /> property's name.
        /// </summary>
        public const string ResidualImagePropertyName = "ResidualImage";

        private ImageSource _residualImage = null;

        /// <summary>
        /// Gets the ResidualImage property.
        /// </summary>
        public ImageSource ResidualImage
        {
            get
            {
                return _residualImage;
            }

            set
            {
                if (_residualImage == value)
                {
                    return;
                }

                var oldValue = _residualImage;
                _residualImage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(ResidualImagePropertyName);
            }
        }

        #endregion

        #region FusionStatusMessage

        /// <summary>
        /// The <see cref="FusionStatusMessage" /> property's name.
        /// </summary>
        public const string FusionStatusMessagePropertyName = "FusionStatusMessage";

        private string _fusionStatusMessage = "";

        /// <summary>
        /// Gets the FusionStatusMessage property.
        /// </summary>
        public string FusionStatusMessage
        {
            get
            {
                return _fusionStatusMessage;
            }

            set
            {
                if (_fusionStatusMessage == value)
                {
                    return;
                }

                var oldValue = _fusionStatusMessage;
                _fusionStatusMessage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(FusionStatusMessagePropertyName);
            }
        }

        #endregion

        #region IsTrackingModel

        /// <summary>
        /// The <see cref="IsTrackingModel" /> property's name.
        /// </summary>
        public const string IsTrackingModelPropertyName = "IsTrackingModel";

        private bool _isTrackingModel = true;

        /// <summary>
        /// Gets the IsTrackingModel property.
        /// </summary>
        public bool IsTrackingModel
        {
            get
            {
                return _isTrackingModel;
            }

            set
            {
                if (_isTrackingModel == value)
                {
                    return;
                }

                var oldValue = _isTrackingModel;
                _isTrackingModel = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(IsTrackingModelPropertyName);
            }
        }

        #endregion

        #region IsIntegrationPaused

        /// <summary>
        /// The <see cref="IsIntegrationPaused" /> property's name.
        /// </summary>
        public const string IsIntegrationPausedPropertyName = "IsIntegrationPaused";

        private bool _isIntegrationPaused = true;

        /// <summary>
        /// Gets the IsIntegrationPaused property.
        /// </summary>
        public bool IsIntegrationPaused
        {
            get
            {
                return _isIntegrationPaused;
            }

            set
            {
                if (_isIntegrationPaused == value)
                {
                    return;
                }

                var oldValue = _isIntegrationPaused;
                _isIntegrationPaused = value;

                if (_isIntegrationPaused)
                {
                    _audioManager.Stop();
                }
                else
                {
                    _audioManager.Start();
                }

                // Update bindings, no broadcast
                RaisePropertyChanged(IsIntegrationPausedPropertyName);
            }
        }

        #endregion

        #region IsTracking

        /// <summary>
        /// The <see cref="IsTracking" /> property's name.
        /// </summary>
        public const string IsTrackingPropertyName = "IsTracking";

        private bool _isTracking = false;

        /// <summary>
        /// Gets the IsTracking property.
        /// </summary>
        public bool IsTracking
        {
            get
            {
                return _isTracking;
            }

            set
            {
                if (_isTracking == value)
                {
                    return;
                }

                var oldValue = _isTracking;
                _isTracking = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(IsTrackingPropertyName);
            }
        }

        #endregion

        #region AlignmentEnergyString

        /// <summary>
        /// The <see cref="AlignmentEnergyString" /> property's name.
        /// </summary>
        public const string AlignmentEnergyStringPropertyName = "AlignmentEnergyString";

        private string _alignmentEnergyString = "Alignment Energy: ";

        /// <summary>
        /// Gets the AlignmentEnergyString property.
        /// </summary>
        public string AlignmentEnergyString
        {
            get
            {
                return _alignmentEnergyString;
            }

            set
            {
                if (_alignmentEnergyString == value)
                {
                    return;
                }

                var oldValue = _alignmentEnergyString;
                _alignmentEnergyString = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(AlignmentEnergyStringPropertyName);
            }
        }

        #endregion

        #endregion

        #region Constructors

        public FusionManager(KinectSensor sensor)
        {
            this.KinectSensor = sensor;

            _audioManager = new AudioManager();

            InitFusion();
        }

        ~FusionManager()
        {
            Dispose(false);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (this.disposed)
                return;
            this.disposed = true;

            if (_fusionWorkQueue != null)
            {
                _fusionWorkQueue.Dispose();
                _fusionWorkQueue = null;
            }

            if (_audioManager != null)
            {
                _audioManager.Stop();
                //TODO dispose
            }

            if (null != this.depthFloatBuffer)
            {
                this.depthFloatBuffer.Dispose();
            }

            if (null != this.residualFloatBuffer)
            {
                this.residualFloatBuffer.Dispose();
            }

            if (null != this.pointCloudBuffer)
            {
                this.pointCloudBuffer.Dispose();
            }

            if (null != this.shadedSurfaceColorFrame)
            {
                this.shadedSurfaceColorFrame.Dispose();
            }

            if (null != this.volume)
            {
                this.volume.Dispose();
            }

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reset the reconstruction to initial value
        /// </summary>
        public void ResetReconstruction(Vector3 volumeCenter)
        {
            // Reset tracking error counter
            this.trackingErrorCount = 0;

            _currentVolumeCenter = volumeCenter;
            // Set the world-view transform to identity, so the world origin is the initial camera location.
            this.worldToCameraTransform = Matrix4.Identity;
            CurrentRotationDegrees = 0;

            if (null != this.volume)
            {
                // Translate the reconstruction volume location
                if (this.translateResetPose)
                {
                    Matrix4 worldToVolumeTransform = this.defaultWorldToVolumeTransform;

                    // Translate the volume in the Z axis by the minDepthThreshold distance
                    float minDist = (this.minDepthClip < this.maxDepthClip) ? this.minDepthClip : this.maxDepthClip;
                    double volumeSizeZ = (VoxelResolutionZ / (double)VoxelsPerMeter);
                    
                    worldToVolumeTransform.M41 += (float)(volumeCenter.X * VoxelsPerMeter);
                    worldToVolumeTransform.M42 += (float)(volumeCenter.Y * VoxelsPerMeter);
                    worldToVolumeTransform.M43 -= (float)((volumeCenter.Z - 0.5 * volumeSizeZ) * VoxelsPerMeter);

                    Trace.WriteLine("Reset reconstruction at center: " + volumeCenter.X + ", " + volumeCenter.Y + " " + volumeCenter.Z);

                    this.volume.ResetReconstruction(this.worldToCameraTransform, worldToVolumeTransform);
                }
                else
                {
                    this.volume.ResetReconstruction(this.worldToCameraTransform);
                }
            }

            this.ProcessedFrameCount = 0;
        }

        public void ProcessFusionFrame(KinectFrameWorkItem workItem)
        {
            DepthImagePixel[] depthPixels = workItem.DepthImagePixels;
            byte[] colorPixels = workItem.MappedColorImageData;

            DepthImageFormat format = workItem.Format.DepthImageFormat;
            ColorImageFormat color_format = workItem.Format.ColorImageFormat;
            FaceFusion.Services.KinectFormat kinect_format = new FaceFusion.Services.KinectFormat();

            kinect_format.DepthImageFormat = format;
            kinect_format.ColorImageFormat = color_format;

            if (kinect_format.DepthImageFormat != _currentFormat.DepthImageFormat || kinect_format.ColorImageFormat != _currentFormat.ColorImageFormat)
            {
                _currentFormat = kinect_format;
                _fusionWorkItemPool.Format = kinect_format;
                _residualImageData = new byte[depthPixels.Length * 4];
            }

            var fusionWorkItem = _fusionWorkItemPool.Pop();

            if (fusionWorkItem == null)
            {
                Trace.WriteLine("Fusion Depth Pool empty");
                return;
            }

            Array.Copy(depthPixels, fusionWorkItem.data, depthPixels.Length);
            Array.Copy(colorPixels, fusionWorkItem.colordata, colorPixels.Length);

            if (_fusionWorkQueue != null)
            {
                _fusionWorkQueue.AddWork(fusionWorkItem);
            }
        }

        #endregion

        #region Private Methods

        private void InitFusion()
        {
            if (_isFusionInitialized)
                return;

            _currentFormat = new KinectFormat();

            _currentFormat.DepthImageFormat = DepthImageFormat.Undefined;
            _currentFormat.ColorImageFormat = ColorImageFormat.Undefined;

            _isFusionInitialized = true;

            

            var depthFormat = KinectSensor.DepthStream.Format;
            var colorFormat = KinectSensor.ColorStream.Format;
            var kinectFormat = new KinectFormat();
            kinectFormat.DepthImageFormat = depthFormat;
            kinectFormat.ColorImageFormat = colorFormat;

            var depthSize = FormatHelper.GetDepthSize(depthFormat);

            _fusionWorkItemPool = new Pool<FusionWorkItem, KinectFormat>(5, kinectFormat, FusionWorkItem.Create);

            _fusionWorkQueue = new WorkQueue<FusionWorkItem>(ProcessFusionFrameBackground)
            {
                CanceledCallback = ReturnFusionWorkItem,
                MaxQueueLength = 2
            };

            this.frameDataLength = KinectSensor.DepthStream.FramePixelDataLength;

            // Allocate space to put the color pixels we'll create
            this.colorPixels = new int[(int)(depthSize.Width * 2 * depthSize.Height * 2)];

            // This is the bitmap we'll display on-screen
            this.colorFusionBitmap = new WriteableBitmap(
                (int)depthSize.Width * 2,
                (int)depthSize.Height * 2,
                96.0,
                96.0,
                PixelFormats.Bgr32,
                null);
            FusionOutputImage = colorFusionBitmap;


            var volParam = new ReconstructionParameters(VoxelsPerMeter, VoxelResolutionX, VoxelResolutionY, VoxelResolutionZ);

            // Set the world-view transform to identity, so the world origin is the initial camera location.
            this.worldToCameraTransform = Matrix4.Identity;

            try
            {
                // This creates a volume cube with the Kinect at center of near plane, and volume directly
                // in front of Kinect.
                this.volume = ColorReconstruction.FusionCreateReconstruction(volParam, ProcessorType, DeviceToUse, this.worldToCameraTransform);
                this.defaultWorldToVolumeTransform = this.volume.GetCurrentWorldToVolumeTransform();

                if (this.translateResetPose)
                {
                    this.ResetReconstruction(_currentVolumeCenter);
                }
            }
            catch (ArgumentException)
            {
                FusionStatusMessage = "ArgumentException - DX11 GPU not found?";
                return;
            }
            catch (InvalidOperationException ex)
            {
                FusionStatusMessage = ex.Message;
                return;
            }
            catch (DllNotFoundException)
            {
                FusionStatusMessage = Properties.Resources.MissingPrerequisite;
                return;
            }

            // Depth frames generated from the depth input
            this.depthFloatBuffer = new FusionFloatImageFrame((int)depthSize.Width, (int)depthSize.Height);
            this.residualFloatBuffer = new FusionFloatImageFrame((int)depthSize.Width, (int)depthSize.Height);
            _residualData = new float[(int)(depthSize.Width * depthSize.Height)];

            // Point cloud frames generated from the depth float input
            this.pointCloudBuffer = new FusionPointCloudImageFrame((int)depthSize.Width * 2, (int)depthSize.Height * 2);

            // Create images to raycast the Reconstruction Volume
            this.shadedSurfaceColorFrame = new FusionColorImageFrame((int)depthSize.Width * 2, (int)depthSize.Height * 2);

            // Reset the reconstruction
            this.ResetReconstruction(_currentVolumeCenter);

            _audioManager.Start();
        }

        private void FusionUpdateUI(object state)
        {
            bool lastTrackSucceeded = (bool)state;

            this.IsTracking = lastTrackSucceeded;

            // Write the pixel data into our bitmap
            colorFusionBitmap.WritePixels(
                new Int32Rect(0, 0, this.colorFusionBitmap.PixelWidth, this.colorFusionBitmap.PixelHeight),
                this.colorPixels,
                this.colorFusionBitmap.PixelWidth * sizeof(int),
                0);

            var depthSize = FormatHelper.GetDepthSize(_currentFormat.DepthImageFormat);
            if (_residualWritableBitmap == null ||
                _residualWritableBitmap.PixelWidth != (int)depthSize.Width ||
                _residualWritableBitmap.PixelHeight != (int)depthSize.Height)
            {
                _residualWritableBitmap = new WriteableBitmap(
                    (int)depthSize.Width, (int)depthSize.Height, 96, 96, PixelFormats.Bgr32, null);

                ResidualImage = _residualWritableBitmap;
            }

            _residualWritableBitmap.WritePixels(
                new Int32Rect(0, 0, _residualWritableBitmap.PixelWidth, _residualWritableBitmap.PixelHeight),
                                                _residualImageData, _residualWritableBitmap.PixelWidth * sizeof(int), 0);

            this.AlignmentEnergyString = "Alignment Energy: " + _alignmentEnergy.ToString("F3") + 
                                         " Avg Residual: " + _averageResidual.ToString("F3");
        }

        private void ReturnFusionWorkItem(FusionWorkItem workItem)
        {
            _fusionWorkItemPool.Push(workItem);
        }

        /// <summary>
        /// Process the depth input
        /// </summary>
        /// <param name="depthPixels">The depth data array to be processed</param>
        private void ProcessFusionFrameBackground(FusionWorkItem workItem)
        {
            Debug.Assert(null != this.volume, "volume should be initialized");
            Debug.Assert(null != this.shadedSurfaceColorFrame, "shaded surface should be initialized");
            Debug.Assert(null != this.colorFusionBitmap, "color bitmap should be initialized");

            try
            {
                DepthImagePixel[] depthPixels = workItem.data;
                byte[] colorPixels = workItem.colordata;

                bool trackingSucceeded = false;
                if (!IsIntegrationPaused)
                {
                    trackingSucceeded = TrackIntegrate(depthPixels, workItem.colordata, workItem.Format);
                }
                if (ProcessedFrameCount % 2 == 0)
                {
                    RenderFusion();
                }

                // The input frame was processed successfully, increase the processed frame count
                ++this.ProcessedFrameCount;

                //Console.WriteLine("ohohoh!pushed item !");
                _fusionWorkItemPool.Push(workItem);

                _syncContext.Post((SendOrPostCallback)FusionUpdateUI, trackingSucceeded);
                //return trackingSucceeded;
            }
            catch (InvalidOperationException ex)
            {
                FusionStatusMessage = ex.Message;
                //return false;
            }
            finally
            {
            }
        }

        private bool TrackIntegrate(DepthImagePixel[] depthPixels, byte[] colorPixels, KinectFormat workFormat)
        {
            var depthSize = FormatHelper.GetDepthSize(workFormat.DepthImageFormat);
            var colorSize = FormatHelper.GetColorSize(workFormat.ColorImageFormat);

            // Convert the depth image frame to depth float image frame
            FusionDepthProcessor.DepthToDepthFloatFrame(
                depthPixels,
                (int)depthSize.Width,
                (int)depthSize.Height,
                this.depthFloatBuffer,
                FusionDepthProcessor.DefaultMinimumDepth,
                FusionDepthProcessor.DefaultMaximumDepth,
                false);


            bool trackingSucceeded = this.volume.AlignDepthFloatToReconstruction(
                    depthFloatBuffer,
                    FusionDepthProcessor.DefaultAlignIterationCount,
                    residualFloatBuffer,
                    out _alignmentEnergy,
                    volume.GetCurrentWorldToCameraTransform());

            //if (trackingSucceeded && _alignmentEnergy == 0.0)
            //    trackingSucceeded = false;

            // ProcessFrame will first calculate the camera pose and then integrate
            // if tracking is successful
            //bool trackingSucceeded = this.volume.ProcessFrame(
            //    this.depthFloatBuffer,
            //    FusionDepthProcessor.DefaultAlignIterationCount,
            //    IntegrationWeight,
            //    this.volume.GetCurrentWorldToCameraTransform());

            // If camera tracking failed, no data integration or raycast for reference
            // point cloud will have taken place, and the internal camera pose
            // will be unchanged.
            if (!trackingSucceeded)
            {
                this.trackingErrorCount++;

                // Show tracking error on status bar
                FusionStatusMessage = Properties.Resources.CameraTrackingFailed;
                _audioManager.State = AudioState.Error;
            }
            else
            {
                ProcessResidualImage();

                this.worldToCameraTransform = volume.GetCurrentWorldToCameraTransform();

                if (!IsIntegrationPaused)
                {
                    FusionColorImageFrame frame = new FusionColorImageFrame((int)colorSize.Width, (int)colorSize.Height);

                    int[] samples = new int[colorPixels.Length / 4];
                    Buffer.BlockCopy(colorPixels, 0, samples, 0, colorPixels.Length);

                    frame.CopyPixelDataFrom(samples);
                    this.volume.IntegrateFrame(depthFloatBuffer, frame, FusionDepthProcessor.DefaultIntegrationWeight, FusionDepthProcessor.DefaultColorIntegrationOfAllAngles, this.worldToCameraTransform);
                }

                this.trackingErrorCount = 0;
            }

            if (AutoResetReconstructionWhenLost && !trackingSucceeded && this.trackingErrorCount == MaxTrackingErrors)
            {
                // Auto Reset due to bad tracking
                FusionStatusMessage = Properties.Resources.ResetVolume;

                // Automatically Clear Volume and reset tracking if tracking fails
                this.ResetReconstruction(_currentVolumeCenter);
            }
            return trackingSucceeded;
        }

        private void RenderFusion()
        {
            Matrix3D m = Matrix3D.Identity;
            m = worldToCameraTransform.ToMatrix3D();

            CurrentRotationDegrees += RotationRateInDegrees;

            double zSize = VoxelResolutionZ / (double)VoxelsPerMeter;
            m.Translate(new Vector3D(_currentVolumeCenter.X,
                                     _currentVolumeCenter.Y,
                                     -_currentVolumeCenter.Z));
            m.Rotate(new Quaternion(new Vector3D(0, 1, 0), CurrentRotationDegrees));

            double zDelta = _volumeCenter.Z - _currentVolumeCenter.Z;

            m.Translate(new Vector3D(0,
                                    0,
                                    1.75 * zSize));


			//m.Translate(new Vector3D(0 * VoxelsPerMeter,
            //                        0,
            //                        -1.0 * (HeadNeckOffset + 0.5 * zSize)));
            //m.Translate(new Vector3D(_currentVolumeCenter.X, _currentVolumeCenter.Y, _currentVolumeCenter.Z + zSize));

            var cameraTransform = m.ToMatrix4();

            var viewCam = cameraTransform;

            if (!IsTrackingModel)
            {
                viewCam = worldToCameraTransform;
            }

            // Calculate the point cloud
            this.volume.CalculatePointCloud(this.pointCloudBuffer, viewCam);

            float volSizeX = VoxelResolutionX / (float)VoxelsPerMeter;
            float volSizeY = VoxelResolutionY / (float)VoxelsPerMeter;
            float volSizeZ = VoxelResolutionZ / (float)VoxelsPerMeter;

            Matrix4 worldToBGRTransform = Matrix4.Identity;
            worldToBGRTransform.M11 = VoxelsPerMeter / (float)VoxelResolutionX;
            worldToBGRTransform.M22 = VoxelsPerMeter / (float)VoxelResolutionY;
            worldToBGRTransform.M33 = VoxelsPerMeter / (float)VoxelResolutionZ;
            worldToBGRTransform.M41 = -_currentVolumeCenter.X - 0.5f * volSizeX;
            worldToBGRTransform.M42 = _currentVolumeCenter.Y - 0.5f * volSizeY;
            worldToBGRTransform.M43 = _currentVolumeCenter.Z - 0.5f * volSizeZ;
            worldToBGRTransform.M44 = 1.0f;

            // Shade point cloud and render
            FusionDepthProcessor.ShadePointCloud(
                this.pointCloudBuffer,
                viewCam,
                worldToBGRTransform,
                null,
                this.shadedSurfaceColorFrame);

            this.shadedSurfaceColorFrame.CopyPixelDataTo(this.colorPixels);
        }

        private void ProcessResidualImage()
        {
            residualFloatBuffer.CopyPixelDataTo(_residualData);

            int len = _residualData.Length;

            float newAvgResidual = 0.0f;
            int avgCount = 0;

            for (int i = 0; i < len; i++)
            {
                float data = _residualData[i];

                if (data <= 1.0)
                {
                    newAvgResidual += Math.Abs(data);
                    avgCount++;
                    _residualImageData[i * 4 + 0] = (byte)(255 * MathUtility.Clamp(1 - data, 0, 1));
                    _residualImageData[i * 4 + 1] = (byte)(255 * MathUtility.Clamp(1 - Math.Abs(data), 0, 1));
                    _residualImageData[i * 4 + 2] = (byte)(255 * MathUtility.Clamp(1 + data, 0, 1));
                }
                else
                {
                    _residualImageData[i * 4 + 0] = 0;
                    _residualImageData[i * 4 + 1] = 0;
                    _residualImageData[i * 4 + 2] = 0;
                }
            }

            if (avgCount > 0)
            {
                newAvgResidual /= avgCount;
                _alignmentEnergy = newAvgResidual;
                double rootTone = 60;

                _averageResidual += (newAvgResidual - _averageResidual) * 0.1f;

                if (_averageResidual < 0.1)
                {
                    _audioManager.Semitone = rootTone;
                    _audioManager.State = AudioState.Chord;
                }
                else
                {
                    _audioManager.State = AudioState.SlidingNote;
                    double offsetTone = MathUtility.Clamp(MathUtility.MapValue(_averageResidual, 0.16, 0.1, -16, 0), -16, 0);
                    rootTone += offsetTone;
                    _audioManager.Semitone = rootTone;
                }
            }
            else
            {
                _audioManager.State = AudioState.None;
            }

        }

        #region ExportMesh

        public void ExportMesh()
        {
            if (null == this.volume)
            {
                this.FusionStatusMessage = "Volume not ready to be exported";
                return;
            }
            if (this.savingMesh)
            {
                //Already saving mesh
                return;
            }
            this.savingMesh = true;

            // Mark the start time of saving mesh
            DateTime begining = DateTime.Now;

            try
            {
                this.FusionStatusMessage = "Saving mesh...";

                ColorMesh mesh = this.volume.CalculateMesh(1);

                Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();

                //if (true == this.stlFormat.IsChecked)
                //{
                //    dialog.FileName = "MeshedReconstruction.stl";
                //    dialog.Filter = "STL Mesh Files|*.stl|All Files|*.*";
                //}
                //else
                {
                    dialog.FileName = "MeshedReconstruction.ply";
                    dialog.Filter = "PLY Mesh Files|*.ply|All Files|*.*";
                }

                if (true == dialog.ShowDialog())
                {
                    //if (true == this.stlFormat.IsChecked)
                    //{
                    //    using (BinaryWriter writer = new BinaryWriter(dialog.OpenFile()))
                    //    {
                    //        SaveBinarySTLMesh(mesh, writer);
                    //    }
                    //}
                    //else
                    {
                        using (StreamWriter writer = new StreamWriter(dialog.FileName))
                        {
                            ExportColorMesh(mesh, writer);
                        }
                    }

                    this.FusionStatusMessage = "Mesh saved";
                }
                else
                {
                    this.FusionStatusMessage = "Mesh saving cancelled";
                }
            }
            catch (ArgumentException)
            {
                this.FusionStatusMessage = "Error saving mesh";
            }
            catch (InvalidOperationException)
            {
                this.FusionStatusMessage = "Error saving mesh";
            }
            catch (IOException)
            {
                this.FusionStatusMessage = "Error saving mesh";
            }
            finally
            {
                this.savingMesh = false;
            }
        }

        private void ExportColorMesh(ColorMesh mesh, StreamWriter writer, bool flipAxes = true)
        {
            if (null == writer || null == mesh)
            {
                return;
            }

            bool outputColor = true;

            var vertices = mesh.GetVertices();
            var indices = mesh.GetTriangleIndexes();
            var colors = mesh.GetColors();

            // Check mesh arguments
            if (0 == vertices.Count || 0 != vertices.Count % 3 || vertices.Count != indices.Count || (outputColor && vertices.Count != colors.Count))
            {
                throw new ArgumentException("Invalid mesh arguments. Saving mesh process aborted");
            }

            int faces = indices.Count / 3;

            // Write the PLY header lines
            writer.WriteLine("ply");
            writer.WriteLine("format ascii 1.0");
            writer.WriteLine("comment file created by Microsoft Kinect Fusion");

            writer.WriteLine("element vertex " + vertices.Count.ToString(CultureInfo.CurrentCulture));
            writer.WriteLine("property float x");
            writer.WriteLine("property float y");
            writer.WriteLine("property float z");

            if (outputColor)
            {
                writer.WriteLine("property uchar red");
                writer.WriteLine("property uchar green");
                writer.WriteLine("property uchar blue");
            }

            writer.WriteLine("element face " + faces.ToString(CultureInfo.CurrentCulture));
            writer.WriteLine("property list uchar int vertex_index");
            writer.WriteLine("end_header");

            // Sequentially write the 3 vertices of the triangle, for each triangle
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];

                string vertexString = vertex.X.ToString(CultureInfo.CurrentCulture) + " ";

                if (flipAxes)
                {
                    vertexString += (-vertex.Y).ToString(CultureInfo.CurrentCulture) + " " + (-vertex.Z).ToString(CultureInfo.CurrentCulture);
                }
                else
                {
                    vertexString += vertex.Y.ToString(CultureInfo.CurrentCulture) + " " + vertex.Z.ToString(CultureInfo.CurrentCulture);
                }

                if (outputColor)
                {
                    int red = (colors[i] >> 16) & 255;
                    int green = (colors[i] >> 8) & 255;
                    int blue = colors[i] & 255;

                    vertexString += " " + red.ToString(CultureInfo.CurrentCulture) + " " + green.ToString(CultureInfo.CurrentCulture) + " "
                                    + blue.ToString(CultureInfo.CurrentCulture);
                }

                writer.WriteLine(vertexString);
            }

            // Sequentially write the 3 vertex indices of the triangle face, for each triangle, 0-referenced in PLY files
            for (int i = 0; i < faces; i++)
            {
                string baseIndex0 = (i * 3).ToString(CultureInfo.CurrentCulture);
                string baseIndex1 = ((i * 3) + 1).ToString(CultureInfo.CurrentCulture);
                string baseIndex2 = ((i * 3) + 2).ToString(CultureInfo.CurrentCulture);

                string faceString = "3 " + baseIndex0 + " " + baseIndex1 + " " + baseIndex2;
                writer.WriteLine(faceString);
            }
        }

        #endregion

        #endregion

    }
}

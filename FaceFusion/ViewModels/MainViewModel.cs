/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Blake.NUI.WPF.Utility;
using FaceFusion.Services;
using GalaSoft.MvvmLight;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Fusion;
using RelayCommand = GalaSoft.MvvmLight.Command.RelayCommand;

namespace FaceFusion.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        #region Fields

        DispatcherTimer _voiceHeardResetTimer;

        Vector3 _volumeCenter = new Vector3();

        VoiceCommand _voiceCommand;

        private int rawFrameCount;

        DateTime _lastFPSUpdate = DateTime.Now;

        DispatcherTimer _elevationTimer;

        private WriteableBitmap _colorImageWritableBitmap;

        private SynchronizationContext _syncContext = SynchronizationContext.Current;

        private Pool<KinectFrameWorkItem, KinectFormat> _kinectFrameWorkItemPool;
        private WorkQueue<KinectFrameWorkItem> _kinectWorkQueue;

        private byte[] _depthImageData;
        private WriteableBitmap _depthImageWritableBitmap;

        private byte[] _modDepthImageData;
        private WriteableBitmap _modDepthImageWritableBitmap;

        private const ColorImageFormat DefaultColorImageFormat = ColorImageFormat.RgbResolution640x480Fps30;
        private const DepthImageFormat DefaultDepthImageFormat = DepthImageFormat.Resolution640x480Fps30;
        private KinectFormat _currentKinectFormat;

        private const int DefaultNumSkeletons = 6;

        Skeleton _activeSkeleton;
        int _activeSkeletonId;

        public const int InactiveSkeletonId = -1;

        /// <summary>
        /// Track whether Dispose has been called
        /// </summary>
        private bool disposed;

        int _activeSkeletonLostCount = 0;
        int _activeSkeletonLostLimit = 60;

        #endregion

        #region Properties

        #region Commands

        public RelayCommand ResetCommand { get; private set; }
        public RelayCommand ExportCommand { get; private set; }

        #endregion

        #region IsListening

        /// <summary>
        /// The <see cref="IsListening" /> property's name.
        /// </summary>
        public const string IsListeningPropertyName = "IsListening";

        private bool _isListening = false;

        /// <summary>
        /// Gets the IsListening property.
        /// </summary>
        public bool IsListening
        {
            get
            {
                return _isListening;
            }

            set
            {
                if (_isListening == value)
                {
                    return;
                }

                var oldValue = _isListening;
                _isListening = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(IsListeningPropertyName);
            }
        }

        #endregion

        #region FusionManager

        /// <summary>
        /// The <see cref="FusionManager" /> property's name.
        /// </summary>
        public const string FusionManagerPropertyName = "FusionManager";

        private FusionManager _fusionManager = null;

        /// <summary>
        /// Gets the FusionManager property.
        /// </summary>
        public FusionManager FusionManager
        {
            get
            {
                return _fusionManager;
            }

            set
            {
                if (_fusionManager == value)
                {
                    return;
                }

                var oldValue = _fusionManager;
                _fusionManager = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(FusionManagerPropertyName);
            }
        }

        #endregion

        #region SkeletonJointVM

        /// <summary>
        /// The <see cref="SkeletonJointVM" /> property's name.
        /// </summary>
        public const string SkeletonJointVMPropertyName = "SkeletonJointVM";

        private SkeletonJointViewModel _skeletonJointVM = new SkeletonJointViewModel();

        /// <summary>
        /// Gets the SkeletonJointVM property.
        /// </summary>
        public SkeletonJointViewModel SkeletonJointVM
        {
            get
            {
                return _skeletonJointVM;
            }

            set
            {
                if (_skeletonJointVM == value)
                {
                    return;
                }

                var oldValue = _skeletonJointVM;
                _skeletonJointVM = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(SkeletonJointVMPropertyName);
            }
        }

        #endregion

        #region UserFusionOnly

        /// <summary>
        /// The <see cref="UserFusionOnly" /> property's name.
        /// </summary>
        public const string UserFusionOnlyPropertyName = "UserFusionOnly";

        private bool _userFusionOnly = true;

        /// <summary>
        /// Gets the UserFusionOnly property.
        /// </summary>
        public bool UserFusionOnly
        {
            get
            {
                return _userFusionOnly;
            }

            set
            {
                if (_userFusionOnly == value)
                {
                    return;
                }

                var oldValue = _userFusionOnly;
                _userFusionOnly = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(UserFusionOnlyPropertyName);
                ResetFusion();
            }
        }

        #endregion

        #region ShowRGBOverlay

        /// <summary>
        /// The <see cref="ShowRGBOverlay" /> property's name.
        /// </summary>
        public const string ShowRGBOverlayPropertyName = "ShowRGBOverlay";

        private bool _showRGBOverlay = false;

        /// <summary>
        /// Gets the ShowRGBOverlay property.
        /// </summary>
        public bool ShowRGBOverlay
        {
            get
            {
                return _showRGBOverlay;
            }

            set
            {
                if (_showRGBOverlay == value)
                {
                    return;
                }

                var oldValue = _showRGBOverlay;
                _showRGBOverlay = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(ShowRGBOverlayPropertyName);
            }
        }

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

                FaceTrackingVM.Kinect = _kinectSensor;

                // Update bindings, no broadcast
                RaisePropertyChanged(KinectSensorPropertyName);
            }
        }

        #endregion

        #region KinectSensorChooser

        /// <summary>
        /// The <see cref="KinectSensorChooser" /> property's name.
        /// </summary>
        public const string KinectSensorChooserPropertyName = "KinectSensorChooser";

        private KinectSensorChooser _kinectSensorChooser = new KinectSensorChooser();

        /// <summary>
        /// Gets the KinectSensorChooser property.
        /// </summary>
        public KinectSensorChooser KinectSensorChooser
        {
            get
            {
                return _kinectSensorChooser;
            }
        }

        #endregion

        #region ElevationAngle

        /// <summary>
        /// The <see cref="ElevationAngle" /> property's name.
        /// </summary>
        public const string ElevationAnglePropertyName = "ElevationAngle";

        private double _elevationAngle = 0.0;

        /// <summary>
        /// Gets the ElevationAngle property.
        /// </summary>
        public double ElevationAngle
        {
            get
            {
                return _elevationAngle;
            }

            set
            {
                if (_elevationAngle == value)
                {
                    return;
                }

                var oldValue = _elevationAngle;
                _elevationAngle = value;

                _elevationTimer.Stop();
                _elevationTimer.Start();

                // Update bindings, no broadcast
                RaisePropertyChanged(ElevationAnglePropertyName);
            }
        }

        #endregion

        #region ColorImage

        /// <summary>
        /// The <see cref="ColorImage" /> property's name.
        /// </summary>
        public const string ColorImagePropertyName = "ColorImage";

        private ImageSource _colorImage = null;

        /// <summary>
        /// Gets the ColorImage property.
        /// </summary>
        public ImageSource ColorImage
        {
            get
            {
                return _colorImage;
            }

            set
            {
                if (_colorImage == value)
                {
                    return;
                }

                var oldValue = _colorImage;
                _colorImage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(ColorImagePropertyName);
            }
        }

        #endregion

        #region DepthImage

        /// <summary>
        /// The <see cref="DepthImage" /> property's name.
        /// </summary>
        public const string DepthImagePropertyName = "DepthImage";

        private ImageSource _depthImage = null;

        /// <summary>
        /// Gets the DepthImage property.
        /// </summary>
        public ImageSource DepthImage
        {
            get
            {
                return _depthImage;
            }

            set
            {
                if (_depthImage == value)
                {
                    return;
                }

                var oldValue = _depthImage;
                _depthImage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(DepthImagePropertyName);
            }
        }

        #endregion

        #region FusionInputImage

        /// <summary>
        /// The <see cref="FusionInputImage" /> property's name.
        /// </summary>
        public const string FusionInputImagePropertyName = "FusionInputImage";

        private ImageSource _fusionInputImage = null;

        /// <summary>
        /// Gets the FusionInputImage property.
        /// </summary>
        public ImageSource FusionInputImage
        {
            get
            {
                return _fusionInputImage;
            }

            set
            {
                if (_fusionInputImage == value)
                {
                    return;
                }

                var oldValue = _fusionInputImage;
                _fusionInputImage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(FusionInputImagePropertyName);
            }
        }

        #endregion

        #region StatusMessage

        /// <summary>
        /// The <see cref="StatusMessage" /> property's name.
        /// </summary>
        public const string StatusMessagePropertyName = "StatusMessage";

        private string _statusMessage = "";

        /// <summary>
        /// Gets the StatusMessage property.
        /// </summary>
        public string StatusMessage
        {
            get
            {
                return _statusMessage;
            }

            set
            {
                if (_statusMessage == value)
                {
                    return;
                }

                var oldValue = _statusMessage;
                _statusMessage = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(StatusMessagePropertyName);
            }
        }

        #endregion

        #region FaceTrackingVM

        /// <summary>
        /// The <see cref="FaceTrackingVM" /> property's name.
        /// </summary>
        public const string FaceTrackingVMPropertyName = "FaceTrackingVM";

        private FaceTrackingViewModel _faceTrackingVM = new FaceTrackingViewModel();

        /// <summary>
        /// Gets the FaceTrackingVM property.
        /// </summary>
        public FaceTrackingViewModel FaceTrackingVM
        {
            get
            {
                return _faceTrackingVM;
            }

            set
            {
                if (_faceTrackingVM == value)
                {
                    return;
                }

                var oldValue = _faceTrackingVM;
                _faceTrackingVM = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(FaceTrackingVMPropertyName);
            }
        }

        #endregion

        #region HeadNeckOffset

        /// <summary>
        /// The <see cref="HeadNeckOffset" /> property's name.
        /// </summary>
        public const string HeadNeckOffsetPropertyName = "HeadNeckOffset";

        private double _headNeckOffset = 0.95;

        /// <summary>
        /// Gets the HeadNeckOffset property.
        /// </summary>
        public double HeadNeckOffset
        {
            get
            {
                return _headNeckOffset;
            }

            set
            {
                if (_headNeckOffset == value)
                {
                    return;
                }

                var oldValue = _headNeckOffset;
                _headNeckOffset = value;

                ResetFusion();
                StatusMessage = "Reset; Head-Neck Offset now " + _headNeckOffset;
                // Update bindings, no broadcast
                RaisePropertyChanged(HeadNeckOffsetPropertyName);
            }
        }

        #endregion

        #region VoiceHeard

        /// <summary>
        /// The <see cref="VoiceHeard" /> property's name.
        /// </summary>
        public const string VoiceHeardPropertyName = "VoiceHeard";

        private string _voiceHeard = "";

        /// <summary>
        /// Gets the VoiceHeard property.
        /// </summary>
        public string VoiceHeard
        {
            get
            {
                return _voiceHeard;
            }

            set
            {
                if (_voiceHeard == value)
                {
                    return;
                }

                var oldValue = _voiceHeard;
                _voiceHeard = value;

                _voiceHeardResetTimer.Stop();
                _voiceHeardResetTimer.Start();

                // Update bindings, no broadcast
                RaisePropertyChanged(VoiceHeardPropertyName);
            }
        }

        #endregion

        #region IsColorIntegrated

        /// <summary>
        /// The <see cref="IsColorIntegrated" /> property's name.
        /// </summary>
        public const string IsColorIntegratedPropertyName = "IsColorIntegrated";

        private bool _isColorIntegrated = false;

        /// <summary>
        /// Gets the IsColorIntegrated property.
        /// </summary>
        public bool IsColorIntegrated
        {
            get
            {
                return _isColorIntegrated;
            }

            set
            {
                if (_isColorIntegrated == value)
                {
                    return;
                }

                ResetFusion();
                var oldValue = _isColorIntegrated;
                _isColorIntegrated = value;
                FusionManager.IntegratingColor = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(IsColorIntegratedPropertyName);
            }
        }

        #endregion

        #endregion

        #region Constructors

        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
            }
            else
            {
                // Code runs "for real": Connect to service, etc...
                Init();
            }
        }

        ~MainViewModel()
        {
            this.Dispose(false);
        }

        #endregion

        #region Overridden Methods

        public override void Cleanup()
        {
            // Clean own resources if needed
            base.Cleanup();
            Dispose();
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose the allocated frame buffers and reconstruction.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees all memory associated with the FusionImageFrame.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;
            this.disposed = true;

            KinectSensorChooser.Stop();

            StopKinect();

            if (_kinectWorkQueue != null)
            {
                _kinectWorkQueue.Dispose();
                _kinectWorkQueue = null;
            }

            if (FusionManager != null)
            {
                FusionManager.Dispose();
                FusionManager = null;
            }
        }

        #endregion

        #region Private Methods

        #region Initialization

        private void Init()
        {
            _currentKinectFormat = new KinectFormat()
            {
                ColorImageFormat = ColorImageFormat.Undefined,
                DepthImageFormat = DepthImageFormat.Undefined,
                NumSkeletons = 0
            };

            _kinectFrameWorkItemPool = new Pool<KinectFrameWorkItem, KinectFormat>(5, _currentKinectFormat, KinectFrameWorkItem.Create);

            _kinectWorkQueue = new WorkQueue<KinectFrameWorkItem>(ProcessKinectFrame)
            {
                CanceledCallback = ReturnKinectFrameWorkItem,
                MaxQueueLength = 1
            };

            _elevationTimer = new DispatcherTimer();
            _elevationTimer.Interval = TimeSpan.FromMilliseconds(500);
            _elevationTimer.Tick += new EventHandler(elevationTimer_Tick);

            InitRelayCommands();

            KinectSensorChooser.KinectChanged += SensorChooserOnKinectChanged;

            KinectSensorChooser.Start();

            _voiceHeardResetTimer = new DispatcherTimer();
            _voiceHeardResetTimer.Tick += new EventHandler(_voiceHeadResetTimer_Tick);
            _voiceHeardResetTimer.Interval = TimeSpan.FromSeconds(2);
        }

        void _voiceHeadResetTimer_Tick(object sender, EventArgs e)
        {
            _voiceHeardResetTimer.Stop();
            _voiceHeard = "";
            RaisePropertyChanged(VoiceHeardPropertyName);
        }

        private void InitRelayCommands()
        {
            ResetCommand = new RelayCommand(() =>
            {
                if (this.KinectSensor == null)
                {
                    StatusMessage = Properties.Resources.ConnectDeviceFirst;
                    return;
                }

                // reset the reconstruction and update the status text
                ResetFusion();
                StatusMessage = Properties.Resources.ResetReconstruction;
            });

            ExportCommand = new RelayCommand(() =>
            {
                if (FusionManager != null)
                {
                    FusionManager.ExportMesh();
                }
            });
        }


        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {
            KinectSensor oldSensor = kinectChangedEventArgs.OldSensor;
            KinectSensor newSensor = kinectChangedEventArgs.NewSensor;

            if (oldSensor != null && oldSensor == KinectSensor)
            {
                StopKinect();
            }

            if (newSensor != null)
            {
                StartKinect(newSensor);
            }
        }

        private void StartKinect(KinectSensor newSensor)
        {
            try
            {
                newSensor.ColorStream.Enable(DefaultColorImageFormat);
                newSensor.DepthStream.Enable(DefaultDepthImageFormat);
                try
                {
                    // This will throw on non Kinect For Windows devices.
                    newSensor.DepthStream.Range = DepthRange.Near;
                    newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                }
                catch (InvalidOperationException)
                {
                    newSensor.DepthStream.Range = DepthRange.Default;
                    newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                }

                newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;

                var smoothParams = new TransformSmoothParameters()
                {
                    Smoothing = 0.8f,
                    Correction = 0.2f,
                    Prediction = 0.5f,
                    JitterRadius = 0.10f,
                    MaxDeviationRadius = 0.04f
                };

                newSensor.SkeletonStream.Enable(smoothParams);
                newSensor.AllFramesReady += KinectSensorOnAllFramesReady;

                _elevationAngle = newSensor.ElevationAngle;
                RaisePropertyChanged(ElevationAnglePropertyName);

                this.KinectSensor = newSensor;

                this.rawFrameCount = 0;

                //TODO cleanup old voice command
                _voiceCommand = new VoiceCommand(newSensor);
                _voiceCommand.IsListeningChanged += (s, e) =>
                    {
                        this.IsListening = _voiceCommand.IsListening;
                        if (this.IsListening)
                        {
                            VoiceHeard = "Listening...";
                        }
                    };
                _voiceCommand.FusionPause += (s, e) =>
                    {
                        if (FusionManager.IsIntegrationPaused)
                        {
                            FusionManager.IsIntegrationPaused = false;
                            FusionManager.RotationRateInDegrees = 0;
                            FusionManager.CurrentRotationDegrees = 0;
                        }
                        else
                        {
                            FusionManager.IsIntegrationPaused = true;
                            FusionManager.RotationRateInDegrees = 3;
                        }
                        VoiceHeard = "Heard: Fusion Pause";
                    };
                _voiceCommand.FusionReset += (s, e) =>
                    {
                        ResetCommand.Execute(null);
                        if (FusionManager != null)
                        {
                            FusionManager.IsIntegrationPaused = false;
                            FusionManager.RotationRateInDegrees = 0;

                            FusionManager.CurrentRotationDegrees = 0;
                        }
                        VoiceHeard = "Heard: Fusion Reset";
                    };
                _voiceCommand.FusionStart += (s, e) =>
                    {
                        ResetCommand.Execute(null);
                        if (FusionManager != null)
                        {
                            FusionManager.IsIntegrationPaused = false;
                            FusionManager.RotationRateInDegrees = 0;

                            FusionManager.CurrentRotationDegrees = 0;
                        }
                        VoiceHeard = "Heard: Fusion Start";
                    };
                _voiceCommand.FusionColor += (s, e) =>
                    {
                        this.IsColorIntegrated = !this.IsColorIntegrated;
                    };
                if (FusionManager != null)
                {
                    FusionManager.Dispose();
                    FusionManager = null;
                }

                FusionManager = new FusionManager(KinectSensor);
            }
            catch (InvalidOperationException)
            {
                // This exception can be thrown when we are trying to
                // enable streams on a device that has gone away.  This
                // can occur, say, in app shutdown scenarios when the sensor
                // goes away between the time it changed status and the
                // time we get the sensor changed notification.
                //
                // Behavior here is to just eat the exception and assume
                // another notification will come along if a sensor
                // comes back.
            }
        }

        private void StopKinect()
        {
            if (KinectSensor == null)
                return;

            KinectSensor.AllFramesReady -= KinectSensorOnAllFramesReady;
            KinectSensor.ColorStream.Disable();
            KinectSensor.DepthStream.Disable();
            KinectSensor.DepthStream.Range = DepthRange.Default;
            KinectSensor.SkeletonStream.Disable();
            KinectSensor.SkeletonStream.EnableTrackingInNearRange = false;
            KinectSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;

            KinectSensor.Stop();

            KinectSensor = null;
        }

        #endregion

        #region Kinect

        private void KinectSensorOnAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            bool formatChanged = false;

            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;
            try
            {
                colorImageFrame = e.OpenColorImageFrame();
                depthImageFrame = e.OpenDepthImageFrame();

                if (colorImageFrame != null)
                {
                    if (_currentKinectFormat.ColorImageFormat != colorImageFrame.Format)
                    {
                        formatChanged = true;
                        _currentKinectFormat.ColorImageFormat = colorImageFrame.Format;
                    }
                }

                if (depthImageFrame != null)
                {
                    if (_currentKinectFormat.DepthImageFormat != depthImageFrame.Format)
                    {
                        formatChanged = true;

                        _currentKinectFormat.DepthImageFormat = depthImageFrame.Format;

                        var depthWidth = depthImageFrame.Width;
                        var depthHeight = depthImageFrame.Height;

                        FaceTrackingVM.DepthWidth = depthWidth;
                        FaceTrackingVM.DepthHeight = depthHeight;

                        this._depthImageData = new byte[depthImageFrame.PixelDataLength * 4];
                        this._modDepthImageData = new byte[depthImageFrame.PixelDataLength * 4];
                        this._depthImageWritableBitmap = new WriteableBitmap(
                            depthWidth, depthHeight, 96, 96, PixelFormats.Bgr32, null);

                        this._modDepthImageWritableBitmap = new WriteableBitmap(
                            depthWidth, depthHeight, 96, 96, PixelFormats.Bgr32, null);

                        _colorImageWritableBitmap = new WriteableBitmap(
                            depthWidth, depthHeight, 96, 96, PixelFormats.Bgr32, null);

                        ColorImage = _colorImageWritableBitmap;
                        DepthImage = _depthImageWritableBitmap;
                        FusionInputImage = _modDepthImageWritableBitmap;
                    }

                }

                skeletonFrame = e.OpenSkeletonFrame();
                if (skeletonFrame != null)
                {
                    if (_currentKinectFormat.NumSkeletons != skeletonFrame.SkeletonArrayLength)
                    {
                        _currentKinectFormat.NumSkeletons = skeletonFrame.SkeletonArrayLength;
                        formatChanged = true;
                    }
                }

                if (formatChanged)
                {
                    _kinectFrameWorkItemPool.Format = _currentKinectFormat;
                }

                if (colorImageFrame != null &&
                    depthImageFrame != null &&
                    skeletonFrame != null)
                {
                    var workItem = _kinectFrameWorkItemPool.Pop();

                    workItem.FrameNumber = depthImageFrame.FrameNumber;

                    colorImageFrame.CopyPixelDataTo(workItem.ColorPixels);
                    depthImageFrame.CopyDepthImagePixelDataTo(workItem.DepthImagePixels);
                    skeletonFrame.CopySkeletonDataTo(workItem.Skeletons);

                    var mapper = KinectSensor.CoordinateMapper;

                    mapper.MapColorFrameToDepthFrame(workItem.Format.ColorImageFormat,
                                                     workItem.Format.DepthImageFormat,
                                                     workItem.DepthImagePixels,
                                                     workItem.ColorMappedToDepthPoints);

                    if (_kinectWorkQueue != null)
                    {
                        _kinectWorkQueue.AddWork(workItem);
                    }
                }

            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }
                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }
                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void ReturnKinectFrameWorkItem(KinectFrameWorkItem workItem)
        {
            _kinectFrameWorkItemPool.Push(workItem);
        }

        private void ProcessKinectFrame(KinectFrameWorkItem workItem)
        {
            ProcessSkeletonFrame(workItem);
            ProcessColorFrame(workItem);
            ProcessDepthFrame(workItem);

            FusionManager.ProcessFusionFrame(workItem);
			//var skeletonList = new List<Skeleton>();
            //if (_activeSkeleton != null)
            //{
            //    skeletonList.Add(_activeSkeleton);
            //}

            //FaceTrackingVM.TrackFrame(DefaultColorImageFormat,
            //                         _colorImageData,
            //                         DefaultDepthImageFormat,
            //                         _depthImagePixels,
            //                         skeletonList,
            //                         fusionWorkItem.FrameNumber);

            rawFrameCount++;

            _kinectFrameWorkItemPool.Push(workItem);

            _syncContext.Post((SendOrPostCallback)UpdateKinectFrameUI, workItem);
        }

        private void UpdateKinectFrameUI(object state)
        {
            KinectFrameWorkItem item = state as KinectFrameWorkItem;

            _depthImageWritableBitmap.WritePixels(
                new Int32Rect(0, 0, _depthImageWritableBitmap.PixelWidth, _depthImageWritableBitmap.PixelHeight),
                _depthImageData,
                _depthImageWritableBitmap.PixelWidth * 4,
                0);

            _colorImageWritableBitmap.WritePixels(
                new Int32Rect(0, 0, _colorImageWritableBitmap.PixelWidth, _colorImageWritableBitmap.PixelHeight),
                item.MappedColorImageData,
                _colorImageWritableBitmap.PixelWidth * 4,
                0);

            SkeletonJointVM.ProcessFrame(KinectSensor.CoordinateMapper, _activeSkeleton, _currentKinectFormat.DepthImageFormat);

            CheckFPS();
        }

        private void ProcessSkeletonFrame(KinectFrameWorkItem workItem)
        {
            var skeletonList = workItem.Skeletons.ToList();
            var closestSkeleton = skeletonList.Where(s => s.TrackingState == SkeletonTrackingState.Tracked)
                                              .OrderBy(s => s.Position.Z * Math.Abs(s.Position.X))
                                              .FirstOrDefault();

            bool newSkeleton = false;
            if (closestSkeleton != null &&
                (_activeSkeleton == null ||
                 _activeSkeleton.TrackingId != closestSkeleton.TrackingId))
            {
                newSkeleton = true;
            }

            if (closestSkeleton == null)
            {
                _activeSkeletonLostCount++;

                if (_activeSkeletonLostCount > _activeSkeletonLostLimit)
                {
                    _activeSkeleton = null;
                }
                _activeSkeletonId = InactiveSkeletonId;
            }
            else
            {
                _activeSkeletonLostCount = 0;
                _activeSkeleton = closestSkeleton;
                _activeSkeletonId = skeletonList.IndexOf(closestSkeleton) + 1;
                
                var headJoint = closestSkeleton.Joints[JointType.Head];
                var neckJoint = closestSkeleton.Joints[JointType.ShoulderCenter];
                float headFraction = (float)HeadNeckOffset;

                _volumeCenter.X = (headJoint.Position.X * headFraction) + (neckJoint.Position.X) * (1.0f - headFraction);
                _volumeCenter.Y = (headJoint.Position.Y * headFraction) + (neckJoint.Position.Y) * (1.0f - headFraction);
                _volumeCenter.Z = (headJoint.Position.Z * headFraction) + (neckJoint.Position.Z) * (1.0f - headFraction);
            }

            if (newSkeleton)
            {
                ResetFusion();
            }
        }

        private unsafe void ProcessDepthFrame(KinectFrameWorkItem workItem)
        {
            var depthSize = FormatHelper.GetDepthSize(workItem.Format.DepthImageFormat);
            int width = (int)depthSize.Width;
            int height = (int)depthSize.Height;

            double maxDepth = 4000;
            double minDepth = 400;

            fixed (byte* depthPtrFixed = _depthImageData)
            {
                fixed (DepthImagePixel* pixelPtrFixed = workItem.DepthImagePixels)
                {
                    int* depthIntPtr = (int*)depthPtrFixed;
                    DepthImagePixel* pixelPtr = pixelPtrFixed;

                    int len = width * height;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int srcIndex = (width - 1 - x) + y * width;
                            int targetIndex = x + y * width;

                            var dip = *(pixelPtr + srcIndex);

                            short depth = dip.Depth;
                            int playerIndex = dip.PlayerIndex;

                            byte value = (byte)(255 - 255 * (depth - minDepth) / maxDepth);

                            if (depth <= 0)
                            {
                                value = 0;
                            }

                            int shiftValue = ((255 << 24) | (value << 16) | (value << 8) | (value));

                            *depthIntPtr = shiftValue;
                            depthIntPtr++;

                        }
                    }
                }
            }
        }

        #region Old
        /*
        private unsafe void ProcessDepthFrameMod()
        {
            int width = _depthWidth;
            int height = _depthHeight;

            //Array.Clear(_modDepthImagePixels, 0, _modDepthImagePixels.Length);

            //short defaultDepth = (short)KinectSensor.DepthStream.UnknownDepth;
            //var defaultDIP = new DepthImagePixel() { Depth = defaultDepth };

            double maxDepth = 4000;
            double minDepth = 400;

            //bool processModImage = _activeSkeleton != null;

            //double hx = 0, hy = 0, hzMax = 0, hzMin = 0;
            //double headNeckDist2 = 0;
            //double headDepthThreshold = 300;

            //if (processModImage)
            //{
            //    var mapper = _kinectSensor.CoordinateMapper;

            //    var headJoint = _activeSkeleton.Joints[JointType.Head];
            //    var neckJoint = _activeSkeleton.Joints[JointType.ShoulderCenter];
            //    if (headJoint.TrackingState == JointTrackingState.Tracked &&
            //        neckJoint.TrackingState == JointTrackingState.Tracked)
            //    {
            //        var headPoint = mapper.MapSkeletonPointToDepthPoint(headJoint.Position, DefaultDepthImageFormat);
            //        var pos = new SkeletonPoint()
            //            {
            //                X = headJoint.Position.X,
            //                Y = headJoint.Position.Y - 0.200f,
            //                Z = headJoint.Position.Z
            //            };
            //        var neckPoint = mapper.MapSkeletonPointToDepthPoint(pos, DefaultDepthImageFormat);

            //        hx = depthWidth - headPoint.X;
            //        hy = headPoint.Y;
            //        hzMax = headPoint.Depth + headDepthThreshold;
            //        hzMin = headPoint.Depth - headDepthThreshold;
            //        double factor = 2;
            //        headNeckDist2 = (Math.Pow(neckPoint.X - headPoint.X, 2) +
            //                         Math.Pow(neckPoint.Y - headPoint.Y, 2)) * factor * factor;
            //    }
            //    else
            //    {
            //        processModImage = false;
            //    }
            //}

            fixed (byte* depthPtrFixed = _depthImageData)//, modDepthPtrFixed = _modDepthImageData)
            {
                int* depthIntPtr = (int*)depthPtrFixed;
                //int* modDepthIntPtr = (int*)modDepthPtrFixed;

                int len = width * height;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIndex = (width - 1 - x) + y * width;
                        int targetIndex = x + y * width;

                        var dip = _depthImagePixels[srcIndex];
                        short depth = dip.Depth;
                        int playerIndex = dip.PlayerIndex;

                        byte value = (byte)(255 - 255 * (depth - minDepth) / maxDepth);

                        //byte modValue = 0;
                        //byte modRMult = 0;

                        ////if (processModImage &&
                        ////    playerIndex == _activeSkeletonId)
                        //{
                        //    var dx = x - hx;
                        //    var dy = y - hy;
                        //    var dist2 = dx * dx + dy * dy;

                        //    modValue = value;

                        //    //if (dist2 < headNeckDist2 &&
                        //    //    depth >= hzMin &&
                        //    //    depth <= hzMax)
                        //    {
                        //        modRMult = 1;
                        //        _modDepthImagePixels[srcIndex] = new DepthImagePixel() { Depth = depth };
                        //    }
                        //}
                        //else
                        //{
                        //    _modDepthImagePixels[targetIndex] = defaultDIP;
                        //}

                        if (depth <= 0)
                        {
                            value = 0;
                        }

                        //_depthImageData[targetIndex * 4 + 0] = value;
                        //_depthImageData[targetIndex * 4 + 1] = value;
                        //_depthImageData[targetIndex * 4 + 2] = value;

                        int shiftValue = ((255 << 24) | (value << 16) | (value << 8) | (value));

                        *depthIntPtr = shiftValue;
                        depthIntPtr++;

                        //_modDepthImageData[targetIndex * 4 + 0] = modValue;
                        //_modDepthImageData[targetIndex * 4 + 1] = modValue;
                        //_modDepthImageData[targetIndex * 4 + 2] = modValue;

                        //int shiftModValue = ((255 << 24) | (modValue << 16) | ((modValue * modRMult) << 8) | ((modValue * modRMult)));

                        //*modDepthIntPtr = shiftModValue;
                        //modDepthIntPtr++;

                    }
                }
            }

            _depthImageWritableBitmap.WritePixels(
                new Int32Rect(0, 0, width, height),
                _depthImageData,
                width * 4,
                0);

            //_modDepthImageWritableBitmap.WritePixels(
            //    new Int32Rect(0, 0, width, height),
            //    _modDepthImageData,
            //    width * 4,
            //    0);

            //if (UserFusionOnly)
            //{
            //    ProcessFusionFrame((DepthImagePixel[])_modDepthImagePixels.Clone());
            //}
            //else
            //{
            //}
            ProcessFusionFrame((DepthImagePixel[])_depthImagePixels.Clone());
        }
        */
        #endregion

        private unsafe void ProcessColorFrame(KinectFrameWorkItem workItem)
        {
            var depthSize = FormatHelper.GetDepthSize(workItem.Format.DepthImageFormat);
            var colorSize = FormatHelper.GetColorSize(workItem.Format.ColorImageFormat);

            int colorWidth = (int)colorSize.Width;
            int colorHeight = (int)colorSize.Height;
            int depthWidth = (int)depthSize.Width;
            int depthHeight = (int)depthSize.Height;

            Array.Clear(workItem.MappedColorImageData, 0, workItem.MappedColorImageData.Length);

            var map = workItem.ColorMappedToDepthPoints;

            int depthWidthMinusOne = depthWidth - 1;

            fixed (byte* colorPtrFixed = workItem.ColorPixels, mappedColorPtrFixed = workItem.MappedColorImageData)
            {
                fixed (DepthImagePoint* mapPtrFixed = workItem.ColorMappedToDepthPoints)
                {
                    int* colorIntPtr = (int*)colorPtrFixed;
                    int* mappedColorIntPtr = (int*)mappedColorPtrFixed;
                    DepthImagePoint* mapPtr = mapPtrFixed;

                    //for (int y = 0; y < colorHeight; y += 1)
                    //{
                    //    for (int x = 0; x < colorWidth; x += 1)
                    //    {
                    //        int srcIndex = x + y * colorWidth;
                    int len = colorWidth * colorHeight;
                    for (int i = 0; i < len; i++)
                    {
                        var coord = *mapPtr;

                        int cx = coord.X;
                        int cy = coord.Y;
                        if (cx >= 0 && cx < depthWidth &&
                            cy >= 0 && cy < depthHeight)
                        {
                            int targetIndex = (depthWidthMinusOne - cx) + cy * depthWidth;

                            *(mappedColorIntPtr + targetIndex) = *(colorIntPtr);
                        }

                        mapPtr++;
                        colorIntPtr++;
                    }
					//    }
                    //}
                }
            }
        }

        void elevationTimer_Tick(object sender, EventArgs e)
        {
            _elevationTimer.Stop();

            if (KinectSensor != null)
            {
                KinectSensor.ElevationAngle = (int)Math.Round(ElevationAngle);
            }
        }

        /// <summary>
        /// Update the FPS reading in the status text bar
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckFPS()
        {
            DateTime now = DateTime.Now;
            var span = now - _lastFPSUpdate;
            if (span.TotalSeconds < 1.0)
                return;

            _lastFPSUpdate = now;

            double fusionFPS = FusionManager.ProcessedFrameCount / span.TotalSeconds;
            double rawFPS = this.rawFrameCount / span.TotalSeconds;

            // Update the FPS reading

            StatusMessage = String.Format("Kinect FPS: {0} Fusion FPS: {1}", rawFPS.ToString("F1"), fusionFPS.ToString("F1"));

            // Reset the frame count
            FusionManager.ProcessedFrameCount = 0;
            this.rawFrameCount = 0;
        }

        private void ResetFusion()
        {
            if (!UserFusionOnly)
            {
                _volumeCenter.X = 0;
                _volumeCenter.Y = 0;
                _volumeCenter.Z = (float)(HeadNeckOffset);
            }
            FusionManager.ResetReconstruction(_volumeCenter);
        }

        #endregion

        #endregion
    }
}
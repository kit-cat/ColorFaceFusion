/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight;
using Microsoft.Kinect;
using FaceFusion.Services;

namespace FaceFusion.ViewModels
{
    public class FaceTrackingViewModel : ViewModelBase
    {
        #region Fields

        private readonly Dictionary<int, SkeletonFaceTracker> skeletonFaceTrackers = new Dictionary<int, SkeletonFaceTracker>();

        private const uint MaxMissedFrames = 100;

        private ColorImageFormat _currentColorImageFormat = ColorImageFormat.Undefined;

        //private DepthImagePoint[] _colorMappedDepthPoints;

        #endregion

        #region Properties

        #region FaceTrackers

        /// <summary>
        /// The <see cref="FaceTrackers" /> property's name.
        /// </summary>
        public const string FaceTrackersPropertyName = "FaceTrackers";

        private ObservableCollection<FaceTrackerBase> _FaceTrackers = new ObservableCollection<FaceTrackerBase>();

        /// <summary>
        /// Gets the FaceTrackers property.
        /// </summary>
        public ObservableCollection<FaceTrackerBase> FaceTrackers
        {
            get
            {
                return _FaceTrackers;
            }
        }

        #endregion

        #region Kinect

        public KinectSensor Kinect { get; set; }

        #endregion

        #region DepthWidth

        /// <summary>
        /// The <see cref="DepthWidth" /> property's name.
        /// </summary>
        public const string DepthWidthPropertyName = "DepthWidth";

        private double _depthWidth = 0;

        /// <summary>
        /// Gets the DepthWidth property.
        /// </summary>
        public double DepthWidth
        {
            get
            {
                return _depthWidth;
            }

            set
            {
                if (_depthWidth == value)
                {
                    return;
                }

                var oldValue = _depthWidth;
                _depthWidth = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(DepthWidthPropertyName);
            }
        }

        #endregion

        #region DepthHeight

        /// <summary>
        /// The <see cref="DepthHeight" /> property's name.
        /// </summary>
        public const string DepthHeightPropertyName = "DepthHeight";

        private double _depthHeight = 0.0;

        /// <summary>
        /// Gets the DepthHeight property.
        /// </summary>
        public double DepthHeight
        {
            get
            {
                return _depthHeight;
            }

            set
            {
                if (_depthHeight == value)
                {
                    return;
                }

                var oldValue = _depthHeight;
                _depthHeight = value;

                // Update bindings, no broadcast
                RaisePropertyChanged(DepthHeightPropertyName);
            }
        }

        #endregion

        #endregion

        #region Events

        public event EventHandler FrameUpdated;

        protected void RaiseFrameUpdated()
        {
            if (FrameUpdated == null)
                return;
            FrameUpdated(this, EventArgs.Empty);
        }

        #endregion

        #region Constructors

        public FaceTrackingViewModel()
        {

        }

        #endregion

        #region Public Methods

        public void TrackFrame(ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, DepthImagePixel[] depthImage, IEnumerable<Skeleton> skeletons, int frameNumber)
        {
            if (_currentColorImageFormat != colorImageFormat)
            {
                _currentColorImageFormat = colorImageFormat;
                //_colorMappedDepthPoints = CreateDepthImagePointsFromColorFormat(colorImageFormat);
            }

            //var mapper = this.Kinect.CoordinateMapper;

            //mapper.MapColorFrameToDepthFrame(colorImageFormat, depthImageFormat, depthImage, _colorMappedDepthPoints);

            foreach (var skeleton in skeletons)
            {
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked ||
                    skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                {
                    // We want keep a record of any skeleton, tracked or untracked.
                    if (!this.skeletonFaceTrackers.ContainsKey(skeleton.TrackingId))
                    {
                        var tracker = new SkeletonFaceTracker();
                        this.skeletonFaceTrackers.Add(skeleton.TrackingId, tracker);
                        FaceTrackers.Add(tracker);
                    }

                    // Give each tracker the upated frame.
                    SkeletonFaceTracker skeletonFaceTracker;
                    if (this.skeletonFaceTrackers.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                    {
                        skeletonFaceTracker.OnFrameReady(this.Kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, null, skeleton);
                        skeletonFaceTracker.LastTrackedFrame = frameNumber;
                    }
                }
            }

            this.RemoveOldTrackers(frameNumber);

            RaiseFrameUpdated();
        }

        public void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.skeletonFaceTrackers.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        #endregion

        #region Private Methods

        DepthImagePoint[] CreateDepthImagePointsFromColorFormat(ColorImageFormat colorImageFormat)
        {
            switch (colorImageFormat)
            {
                case Microsoft.Kinect.ColorImageFormat.RgbResolution640x480Fps30:
                    return new DepthImagePoint[640 * 480];
                case Microsoft.Kinect.ColorImageFormat.RgbResolution1280x960Fps12:
                    return new DepthImagePoint[1280 * 960];
                default:
                    throw new NotImplementedException();
            }
        }

        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.skeletonFaceTrackers)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }

        }

        private void RemoveTracker(int trackingId)
        {
            var tracker = skeletonFaceTrackers[trackingId];
            tracker.Dispose();

            skeletonFaceTrackers.Remove(trackingId);

            FaceTrackers.Remove(tracker);
        }


        #endregion
    }
}

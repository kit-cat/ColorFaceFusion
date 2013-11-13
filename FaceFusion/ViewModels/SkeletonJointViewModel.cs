/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Collections.ObjectModel;
using FaceFusion.Services;
using GalaSoft.MvvmLight;
using Microsoft.Kinect;

namespace FaceFusion.ViewModels
{
    public class SkeletonJointViewModel : ViewModelBase
    {
        #region Properties

        #region Joints

        /// <summary>
        /// The <see cref="Joints" /> property's name.
        /// </summary>
        public const string JointsPropertyName = "Joints";

        private ObservableCollection<DepthImagePoint> _joints = new ObservableCollection<DepthImagePoint>();

        /// <summary>
        /// Gets the Joints property.
        /// </summary>
        public ObservableCollection<DepthImagePoint> Joints
        {
            get
            {
                return _joints;
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

        public SkeletonJointViewModel()
        {
            ////if (IsInDesignMode)
            ////{
            ////    // Code runs in Blend --> create design time data.
            ////}
            ////else
            ////{
            ////    // Code runs "for real": Connect to service, etc...
            ////}
        }

        internal void ProcessFrame(CoordinateMapper mapper, Skeleton skeletonOfInterest, DepthImageFormat depthImageFormat)
        {
            _joints.Clear();
            if (skeletonOfInterest != null)
            {
                var size = FormatHelper.GetDepthSize(depthImageFormat);

                var depthWidth = (int)size.Width;

                var headJoint = skeletonOfInterest.Joints[JointType.Head];
                var neckJoint = skeletonOfInterest.Joints[JointType.ShoulderCenter];

                var _headPoint = mapper.MapSkeletonPointToDepthPoint(headJoint.Position, depthImageFormat);
                var _neckPoint = mapper.MapSkeletonPointToDepthPoint(neckJoint.Position, depthImageFormat);

                _headPoint.X = depthWidth - _headPoint.X;
                _neckPoint.X = depthWidth - _neckPoint.X;

                _joints.Add(_headPoint);
                _joints.Add(_neckPoint);

            }
            RaiseFrameUpdated();
        }
    }
}
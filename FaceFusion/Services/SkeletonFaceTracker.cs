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
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit.FaceTracking;
using Point = System.Windows.Point;

namespace FaceFusion.Services
{
    public abstract class FaceTrackerBase : IDisposable
    {
        protected static FaceTriangle[] faceTriangles;

        protected EnumIndexableCollection<FeaturePoint, Vector3DF> facePoints;

        protected FaceTracker faceTracker;

        protected bool lastFaceTrackSucceeded;

        public Microsoft.Kinect.Toolkit.FaceTracking.Rect FaceRect { get; protected set; }

        public int LastTrackedFrame { get; set; }

        public Vector3DF FaceTranslation { get; private set; }

        public Vector3DF FaceRotation { get; private set; }

        protected abstract Brush FaceBrush { get; }

        protected KinectSensor Sensor { get; private set; }

        protected DepthImageFormat DepthImageFormat { get; private set; }

        protected ColorImageFormat ColorImageFormat { get; private set; }

        private DepthImagePoint[] _colorMappedDepthPoints;

        public void Dispose()
        {
            if (this.faceTracker != null)
            {
                this.faceTracker.Dispose();
                this.faceTracker = null;
            }
        }

        protected virtual void DrawOverride(DrawingContext drawingContext)
        { }

        public void DrawFaceModel(DrawingContext drawingContext)
        {
            if (this.lastFaceTrackSucceeded)
            {
                //DrawFaceMask(drawingContext);
                //DrawFaceBox(drawingContext);
            }

            DrawOverride(drawingContext);
        }

        private void DrawFaceBox(DrawingContext drawingContext)
        {
            var depthPoint1 = GetDepthPointForColorPoint(FaceRect.Left, FaceRect.Top);
            var depthPoint2 = GetDepthPointForColorPoint(FaceRect.Right, FaceRect.Bottom);

            var p1 = new Point(depthPoint1.X, depthPoint1.Y);
            var p2 = new Point(depthPoint2.X, depthPoint2.Y);

            drawingContext.DrawRectangle(null, new Pen(FaceBrush, 1.0), new System.Windows.Rect(p1, p2));
        }

        private void DrawFaceMask(DrawingContext drawingContext)
        {
            var faceModelPts = new List<Point>();
            var faceModel = new List<FaceModelTriangle>();

            var mapper = Sensor.CoordinateMapper;

            for (int i = 0; i < this.facePoints.Count; i++)
            {
                var facePoint = facePoints[i];
                var skeletonPoint = new SkeletonPoint() { X = facePoint.X, Y = facePoint.Y, Z = facePoint.Z };

                var depthImagePoint = mapper.MapSkeletonPointToDepthPoint(skeletonPoint, DepthImageFormat);

                faceModelPts.Add(new Point(depthImagePoint.X + 0.5f, depthImagePoint.Y + 0.5f));
            }

            foreach (var t in faceTriangles)
            {
                var triangle = new FaceModelTriangle();
                triangle.P1 = faceModelPts[t.First];
                triangle.P2 = faceModelPts[t.Second];
                triangle.P3 = faceModelPts[t.Third];
                faceModel.Add(triangle);
            }

            var faceModelGroup = new GeometryGroup();
            for (int i = 0; i < faceModel.Count; i++)
            {
                var faceTriangle = new GeometryGroup();
                faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
                faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
                faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));
                faceModelGroup.Children.Add(faceTriangle);
            }

            drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);
        }

        private DepthImagePoint GetDepthPointForColorPoint(int x, int y)
        {
            int index = x + y * GetWidthFromColorImageFormat(ColorImageFormat);
            var depthPoint = _colorMappedDepthPoints[index];
            return depthPoint;
        }

        private int GetWidthFromColorImageFormat(ColorImageFormat format)
        {
            switch (format)
            {
                case Microsoft.Kinect.ColorImageFormat.RgbResolution640x480Fps30:
                case Microsoft.Kinect.ColorImageFormat.RawYuvResolution640x480Fps15:
                case Microsoft.Kinect.ColorImageFormat.RawBayerResolution640x480Fps30:
                case Microsoft.Kinect.ColorImageFormat.InfraredResolution640x480Fps30:
                case Microsoft.Kinect.ColorImageFormat.YuvResolution640x480Fps15:
                    return 640;
                case Microsoft.Kinect.ColorImageFormat.RgbResolution1280x960Fps12:
                case Microsoft.Kinect.ColorImageFormat.RawBayerResolution1280x960Fps12:
                    return 1280;
                default:
                    throw new NotImplementedException();
            }
        }

        protected void VerifyFaceTracker(KinectSensor kinectSensor)
        {
            if (this.faceTracker == null)
            {
                try
                {
                    this.faceTracker = new FaceTracker(kinectSensor);
                }
                catch (InvalidOperationException)
                {
                    // During some shutdown scenarios the FaceTracker
                    // is unable to be instantiated.  Catch that exception
                    // and don't track a face.
                    Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                    this.faceTracker = null;
                }
            }
        }

        protected void UpdateFrame(FaceTrackFrame frame)
        {
            this.lastFaceTrackSucceeded = frame.TrackSuccessful;
            if (this.lastFaceTrackSucceeded)
            {
                if (faceTriangles == null)
                {
                    // only need to get this once.  It doesn't change.
                    faceTriangles = frame.GetTriangles();
                }

                this.facePoints = frame.Get3DShape();
                this.FaceRect = frame.FaceRect;
                this.FaceTranslation = frame.Translation;
                this.FaceRotation = frame.Rotation;
            }
        }

        internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, DepthImagePixel[] depthImage, DepthImagePoint[] colorMappedDepthPoints, Skeleton skeletonOfInterest = null)
        {
            this.Sensor = kinectSensor;

            if (this.ColorImageFormat != colorImageFormat)
            {
                this.ColorImageFormat = colorImageFormat;
            }

            if (this.DepthImageFormat != depthImageFormat)
            {
                this.DepthImageFormat = depthImageFormat;
            }
            this._colorMappedDepthPoints = colorMappedDepthPoints;
            OnFrameReadyOverride(kinectSensor, colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);
        }

        protected abstract void OnFrameReadyOverride(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, DepthImagePixel[] depthImage, Skeleton skeletonOfInterest = null);


        protected struct FaceModelTriangle
        {
            public Point P1;
            public Point P2;
            public Point P3;
        }

    }

    public class RegionFaceTracker : FaceTrackerBase
    {
        protected override Brush FaceBrush
        {
            get { return Brushes.Blue; }
        }

        public RegionFaceTracker(Microsoft.Kinect.Toolkit.FaceTracking.Rect regionOfInterest)
        {
            this.FaceRect = regionOfInterest;
        }

        /// <summary>
        /// Updates the face tracking information for this skeleton
        /// </summary>
        protected override void OnFrameReadyOverride(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, DepthImagePixel[] depthImage, Skeleton skeletonOfInterest = null)
        {
            VerifyFaceTracker(kinectSensor);

            if (this.faceTracker != null)
            {
                var shortImage = Helpers.ConvertDepthImagePixelToShort(depthImage);
                FaceTrackFrame frame = this.faceTracker.Track(
                    colorImageFormat, colorImage, depthImageFormat, shortImage, this.FaceRect);

                UpdateFrame(frame);
                var rect = this.FaceRect;
                rect.Left += 1;
                rect.Top += 1;
                rect.Bottom += 1;
                rect.Right += 1;
                this.FaceRect = rect;
            }
        }
    }

    public class SkeletonFaceTracker : FaceTrackerBase
    {
        private DepthImagePoint _headPoint;
        private DepthImagePoint _neckPoint;

        protected override Brush FaceBrush
        {
            get { return Brushes.Green; }
        }

        protected override void DrawOverride(DrawingContext drawingContext)
        {
            var pen = new Pen(Brushes.Blue, 1.0);

            drawingContext.DrawEllipse(null, pen, new Point(_headPoint.X, _headPoint.Y), 4, 4);
            drawingContext.DrawEllipse(null, pen, new Point(_neckPoint.X, _neckPoint.Y), 4, 4);
        }

        /// <summary>
        /// Updates the face tracking information for this skeleton
        /// </summary>
        protected override void OnFrameReadyOverride(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, DepthImagePixel[] depthImage, Skeleton skeletonOfInterest = null)
        {
            if (skeletonOfInterest == null || skeletonOfInterest.TrackingState != SkeletonTrackingState.Tracked)
            {
                // nothing to do with an untracked skeleton.
                return;
            }

            var mapper = kinectSensor.CoordinateMapper;

            var depthWidth = kinectSensor.DepthStream.FrameWidth;

            var headJoint = skeletonOfInterest.Joints[JointType.Head];
            var neckJoint = skeletonOfInterest.Joints[JointType.ShoulderCenter];

            _headPoint = mapper.MapSkeletonPointToDepthPoint(headJoint.Position, depthImageFormat);
            _neckPoint = mapper.MapSkeletonPointToDepthPoint(neckJoint.Position, depthImageFormat);

            _headPoint.X = depthWidth - _headPoint.X;
            _neckPoint.X = depthWidth - _neckPoint.X;

            //VerifyFaceTracker(kinectSensor);

            //BackgroundWorker worker = new BackgroundWorker();

            //worker.DoWork += (s, e) =>
            //    {
                    //if (this.faceTracker != null)
                    //{

                    //    var shortImage = Helpers.ConvertDepthImagePixelToShort(depthImage);
                    //    FaceTrackFrame frame = this.faceTracker.Track(
                    //        colorImageFormat, colorImage, depthImageFormat, shortImage, skeletonOfInterest);

                    //    UpdateFrame(frame);
                    //}

            //    };

            //worker.RunWorkerCompleted += (s, e) =>
            //    {
            //    };

            //worker.RunWorkerAsync();
        }
    }
}


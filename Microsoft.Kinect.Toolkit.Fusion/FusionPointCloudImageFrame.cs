﻿//-----------------------------------------------------------------------
// <copyright file="FusionPointCloudImageFrame.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Kinect.Toolkit.Fusion
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// A frame used specifically for float-based point cloud images.
    /// It provides access to the dimensions, format and pixel data for a depth frame.
    /// </summary>
    public sealed class FusionPointCloudImageFrame : FusionImageFrame
    {
        /// <summary>
        /// Initializes a new instance of the FusionPointCloudImageFrame class.
        /// </summary>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="cameraParameters">The camera parameters.</param>
        public FusionPointCloudImageFrame(int width, int height, CameraParameters cameraParameters)
            : base(FusionImageType.PointCloud, width, height, cameraParameters)
        {
        }

        /// <summary>
        /// Initializes a new instance of the FusionPointCloudImageFrame class with default camera parameters.
        /// </summary>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        public FusionPointCloudImageFrame(int width, int height)
            : this(width, height, null)
        {
        }

        /// <summary>
        /// Gets the bytes per pixel of this image frame.
        /// </summary>
        public override int BytesPerPixel
        {
            // 6 floats per pixel (3D Point x,y,z, Normal x,y,z).
            get { return sizeof(float) * 6; }
        }

        /// <summary>
        ///  This method copies pixel data from a pre-allocated array to this image.
        /// </summary>
        /// <param name="sourcePixelData">
        /// The source float array of pixel data. It must be exactly PixelDataLength pixels in length,
        /// with the number of bytes per Pixel equal to BytesPerPixel.
        /// </param>
        public void CopyPixelDataFrom(float[] sourcePixelData)
        {
            if (null == sourcePixelData)
            {
                throw new ArgumentNullException("sourcePixelData");
            }

            if (sourcePixelData.Length != this.PixelDataLength * this.BytesPerPixel / sizeof(float))
            {
                throw new ArgumentException(Resources.ImageDataLengthMismatch, "sourcePixelData");
            }

            this.LockFrameAndExecute((Action<IntPtr>)(
                (dest) =>
                {
                    //JB 6/1/2013 Fixed length calculation per v1.7 known issue notes http://msdn.microsoft.com/en-us/library/dn188692.aspx
                    Marshal.Copy(sourcePixelData, 0, dest, this.PixelDataLength * this.BytesPerPixel / sizeof(float));
                }));
        }

        /// <summary>
        /// This method copies pixel data from this frame to a pre-allocated array.
        /// </summary>
        /// <param name="destinationPixelData">
        /// The destination float array to receive the data. It must be exactly PixelDataLength pixels
        /// in length, with the number of bytes per Pixel equal to BytesPerPixel.
        /// </param>
        public void CopyPixelDataTo(float[] destinationPixelData)
        {
            if (null == destinationPixelData)
            {
                throw new ArgumentNullException("destinationPixelData");
            }

            if (destinationPixelData.Length != this.PixelDataLength * this.BytesPerPixel / sizeof(float))
            {
                throw new ArgumentException(Resources.ImageDataLengthMismatch, "destinationPixelData");
            }

            this.LockFrameAndExecute((Action<IntPtr>)(
                (src) =>
                {
                    //JB 6/1/2013 Fixed length calculation per v1.7 known issue notes http://msdn.microsoft.com/en-us/library/dn188692.aspx
                    Marshal.Copy(src, destinationPixelData, 0, this.PixelDataLength * this.BytesPerPixel / sizeof(float));
                }));
        }
    }
}

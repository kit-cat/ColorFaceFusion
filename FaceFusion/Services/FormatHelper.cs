/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System;
using System.Windows;
using Microsoft.Kinect;

namespace FaceFusion.Services
{
    static class FormatHelper
    {
        /// <summary>
        /// Get the depth image size from the color image format.
        /// </summary>
        /// <param name="imageFormat">The depth image format.</param>
        /// <returns>The width and height of the color image format.</returns>
        public static Size GetColorSize(ColorImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case ColorImageFormat.InfraredResolution640x480Fps30:
                case ColorImageFormat.RawBayerResolution640x480Fps30:
                case ColorImageFormat.RawYuvResolution640x480Fps15:
                case ColorImageFormat.RgbResolution640x480Fps30:
                case ColorImageFormat.YuvResolution640x480Fps15:
                    return new Size(640, 480);

                case ColorImageFormat.RawBayerResolution1280x960Fps12:
                case ColorImageFormat.RgbResolution1280x960Fps12:
                    return new Size(1280, 960);

                case ColorImageFormat.Undefined:
                    return new Size(0, 0);
            }

            throw new ArgumentOutOfRangeException("imageFormat");
        }

        /// <summary>
        /// Get the depth image size from the depth image format.
        /// </summary>
        /// <param name="imageFormat">The depth image format.</param>
        /// <returns>The width and height of the depth image format.</returns>
        public static Size GetDepthSize(DepthImageFormat imageFormat)
        {
            switch (imageFormat)
            {
                case DepthImageFormat.Resolution320x240Fps30:
                    return new Size(320, 240);

                case DepthImageFormat.Resolution640x480Fps30:
                    return new Size(640, 480);

                case DepthImageFormat.Resolution80x60Fps30:
                    return new Size(80, 60);
                case DepthImageFormat.Undefined:
                    return new Size(0, 0);
            }

            throw new ArgumentOutOfRangeException("imageFormat");
        }
    }
}

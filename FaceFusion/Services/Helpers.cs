/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using Microsoft.Kinect;

namespace FaceFusion.Services
{
    static class Helpers
    {
        public static unsafe short[] ConvertDepthImagePixelToShort(DepthImagePixel[] depthImage)
        {
            int len = depthImage.Length;
            short[] ret = new short[len];

            fixed (short* retPtrFixed = ret)
            {
                fixed (DepthImagePixel* srcPtrFixed = depthImage)
                {
                    short* retPtr = retPtrFixed;
                    DepthImagePixel* srcPtr = srcPtrFixed;

                    for (int i = 0; i < len; i++)
                    {
                        *(retPtr) = (*(srcPtr)).Depth;
                        retPtr++;
                        srcPtr++;

                        //ret[i] = depthImage[i].Depth;
                    }
                }
            }

            return ret;
        }
    }
}

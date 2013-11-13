/*
 * This file is part of the Face Fusion project. 
 *
 * Copyright (c) 2013 Joshua Blake
 *
 * This code is licensed to you under the terms of the MIT license.
 * See https://facefusion.codeplex.com/license for a copy of the license.
 */

using System.Windows.Media.Media3D;
using Microsoft.Kinect;

namespace FaceFusion.Extensions
{
    public static class MatrixExtensions
    {
        public static System.Windows.Media.Media3D.Matrix3D ToMatrix3D(this Matrix4 mat)
        {
            System.Windows.Media.Media3D.Matrix3D m = new System.Windows.Media.Media3D.Matrix3D(mat.M11, mat.M12, mat.M13, mat.M14,
                                                                                                mat.M21, mat.M22, mat.M23, mat.M24,
                                                                                                mat.M31, mat.M32, mat.M33, mat.M34,
                                                                                                mat.M41, mat.M42, mat.M43, mat.M44);

            return m;
        }

        public static Matrix4 ToMatrix4(this Matrix3D m)
        {
            Matrix4 ret = Matrix4.Identity;
            UpdateMatrix4(m, ref ret);
            return ret;
        }

        public static void UpdateMatrix4(this Matrix3D m, ref Matrix4 updateMatrix)
        {
            updateMatrix.M11 = (float)m.M11;
            updateMatrix.M12 = (float)m.M12;
            updateMatrix.M13 = (float)m.M13;
            updateMatrix.M14 = (float)m.M14;
            updateMatrix.M21 = (float)m.M21;
            updateMatrix.M22 = (float)m.M22;
            updateMatrix.M23 = (float)m.M23;
            updateMatrix.M24 = (float)m.M24;
            updateMatrix.M31 = (float)m.M31;
            updateMatrix.M32 = (float)m.M32;
            updateMatrix.M33 = (float)m.M33;
            updateMatrix.M34 = (float)m.M34;
            updateMatrix.M41 = (float)m.OffsetX;
            updateMatrix.M42 = (float)m.OffsetY;
            updateMatrix.M43 = (float)m.OffsetZ;
            updateMatrix.M44 = (float)m.M44;
        }
    }
}

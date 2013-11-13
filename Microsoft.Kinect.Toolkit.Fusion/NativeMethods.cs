//-----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Kinect.Toolkit.Fusion
{
    using System;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using System.Text;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43102C25-82EE-4958-AA24-AC5456BF83C0")]
    internal interface INuiFusionMesh
    {
        /// <summary>
        /// Gets the number of vertices in the mesh.
        /// </summary>
        /// <returns>
        /// Returns the number of vertices in the mesh.
        /// </returns>
        [PreserveSig]
        uint VertexCount();

        /// <summary>
        /// Gets the vertices. Each vertex has a corresponding normal with the same index.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT GetVertices([Out] out IntPtr vertices);

        /// <summary>
        /// Gets the number of normals in the mesh.
        /// </summary>
        /// <returns>
        /// Returns the number of normals in the mesh.
        /// </returns>
        [PreserveSig]
        uint NormalCount();

        /// <summary>
        /// Gets the normals. Each normal has a corresponding vertex with the same index.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT GetNormals([Out] out IntPtr normals);

        /// <summary>
        /// Gets the number of triangle indices in the mesh.
        /// Each triangle is formed by three consecutive indices, 
        /// used to index the vertex and normal buffers.
        /// </summary>
        /// <returns>
        /// Returns the length of the buffer.
        /// </returns>
        [PreserveSig]
        uint TriangleVertexIndexCount();

        /// <summary>
        /// Gets the triangle indices.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT GetTriangleIndices([Out] out IntPtr triangleVertexIndices);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("AC19AD5F-2218-4C08-A00F-C981C50A09DF")]
    internal interface INuiFusionReconstruction
    {
        /// <summary>
        /// Clear the volume, optionally setting a new initial camera pose and worldToVolumeTransform.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT ResetReconstruction(
            [In] ref Matrix4 initialWorldToCameraTransform,
            [In] ref Matrix4 worldToVolumeTransform);

        /// <summary>
        /// Aligns a depth float image to the Reconstruction volume to calculate the new camera pose.
        /// This camera tracking method requires a Reconstruction volume, and updates the internal 
        /// camera pose if successful.
        /// The maximum image resolution supported in this function is 640x480.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT AlignDepthFloatToReconstruction(
            [In] HandleRef depthFloatFrame,
            [In] ushort maxAlignIterationCount,
            [In] HandleRef deltaFromReferenceFrame,
            [Out] out float alignmentEnergy,
            [In] ref Matrix4 worldToCameraTransform);

        /// <summary>
        /// Get current internal camera pose.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT GetCurrentWorldToCameraTransform([Out] out Matrix4 worldToCameraTransform);

        /// <summary>
        /// Get current internal world to volume transform.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT GetCurrentWorldToVolumeTransform([Out] out Matrix4 worldToVolumeTransform);

        /// <summary>
        /// Integrates depth float data into the reconstruction volume using the current internal 
        /// camera pose, or the optional pWorldToCameraTransform camera pose.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT IntegrateFrame(
            [In] HandleRef depthFloatFrame, 
            [In] ushort maxIntegrationWeight, 
            [In] ref Matrix4 worldToCameraTransform);

        /// <summary>
        /// A high-level function to process a depth frame through the Kinect Fusion pipeline.
        /// Specifically, this performs on-GPU processing equivalent to the following functions 
        /// for each frame:
        ///
        /// 1) AlignDepthFloatToReconstruction
        /// 2) IntegrateFrame
        /// 3) CalculatePointCloud
        ///
        /// Users may also optionally call the low-level functions individually, instead of calling this
        /// function, for more control. However, this function call will be faster due to the integrated 
        /// nature of the calls. After this call completes, if a visible output image of the reconstruction
        /// is required, the user can call RenderReconstruction and then ShadePointCloud.
        /// The maximum image resolution supported in this function is 640x480.
        ///
        /// If there is a tracking error in the AlignDepthFloatToReconstruction stage, no depth data 
        /// integration will be performed, and the camera pose will remain unchanged.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT ProcessFrame(
            [In] HandleRef depthFloatFrame,
            [In] ushort maxAlignIterationCount,
            [In] ushort maxIntegrationWeight,
            [In] ref Matrix4 worldToCameraTransform);

        /// <summary>
        /// Calculate a point cloud by raycasting into the reconstruction volume, returning the point
        /// cloud containing 3D points and normals of the zero-crossing dense surface at every visible 
        /// pixel in the image from the given camera pose.
        /// This point cloud can be used as a reference frame in the next call to NuiFusionAlignPointClouds,
        /// or passed to NuiFusionShadePointCloud to produce a visible image output. 
        /// pPointCloudFrame can be an arbitrary image size, for example, enabling you to calculate 
        /// point clouds at the size of your window and then create a visible image by calling 
        /// NuiFusionShadePointCloud and render this image. 
        /// Large images will be expensive to calculate, however, raycasting from the current camera pose
        /// with the same resolution and camera parameters as the input depth image enables internal re-use
        /// of data, so it may be faster to render this then scale images pixel-wise in some circumstances.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        HRESULT CalculatePointCloud(
            [In] HandleRef pointCloudFrame,
            [In] ref Matrix4 worldToCameraTransform);

        /// <summary>
        /// Export a mesh of the zero-crossing dense surfaces in the reconstruction volume.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT CalculateMesh(
            [In] uint voxelStep,
            [MarshalAs(UnmanagedType.Interface)] out INuiFusionMesh mesh);

        /// <summary>
        /// Export a part or all of the reconstruction volume as a short array. 
        /// The surface boundary occurs where the tri-linearly interpolated voxel values have a zero crossing.
        /// Note, this means that a 0 in the volume does not necessarily imply a surface.  A surface only 
        /// occurs when an interpolation crosses from positive to negative or vice versa.   
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        [PreserveSig]
        HRESULT ExportVolumeBlock(
            [In] uint sourceOriginX,
            [In] uint sourceOriginY,
            [In] uint sourceOriginZ,
            [In] uint destinationResolutionX,
            [In] uint destinationResolutionY,
            [In] uint destinationResolutionZ,
            [In] uint voxelStep,
            [In] uint countVolumeBlockBytes,
            [Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I1, SizeParamIndex = 7)] short[] volumeBlock);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("13EA17F5-FF2E-4670-9EE5-1297A6E880D1")]
    internal interface INuiFrameTexture
    {
        [PreserveSig]
        int BufferLen();

        [PreserveSig]
        int Pitch();

        [PreserveSig]
        HRESULT LockRect(uint level, ref NUI_LOCKED_RECT lockedRect, IntPtr rect, uint flags);

        [PreserveSig]
        HRESULT GetLevelDesc(uint level, IntPtr desc);

        [PreserveSig]
        HRESULT UnlockRect([In] uint level);
    }

    /// <summary>
    /// The native NUI_FUSION_IMAGE_FRAME structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NUI_FUSION_IMAGE_FRAME
    {
        /// <summary>
        /// The width of the image frame.
        /// </summary>
        public uint Width;

        /// <summary>
        /// The height of the image frame.
        /// </summary>
        public uint Height;

        /// <summary>
        /// The image frame type.
        /// </summary>
        public FusionImageType ImageType;

        /// <summary>
        /// The pointer to the camera parameters.
        /// </summary>
        public IntPtr CameraParameters;

        /// <summary>
        /// The texture interface of the image frame.
        /// </summary>
        public IntPtr FrameTexture;
    }

    /// <summary>
    /// The native NUI_LOCKED_RECT structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NUI_LOCKED_RECT
    {
        /// <summary>
        /// The pitch value.
        /// </summary>
        public int Pitch;

        /// <summary>
        /// The size of the locked rect.
        /// </summary>
        public int Size;

        /// <summary>
        /// The bits pointer of the locked rect.
        /// </summary>
        public IntPtr Bits;
    }

    /// <summary>
    /// Native API declarations from KinectFusion dll.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// The delegate for NuiFusionCreateImageFrame function.
        /// </summary>
        private static NuiFusionCreateImageFrameDelegate CreateImageFrame;

        /// <summary>
        /// The delegate for NuiFusionReleaseImageFrame function.
        /// </summary>
        private static NuiFusionReleaseImageFrameDelegate ReleaseImageFrame;

        /// <summary>
        /// The delegate for NuiFusionDepthToDepthFloatFrame function.
        /// </summary>
        private static NuiFusionDepthToDepthFloatFrameDelegate DepthToDepthFloatFrame;

        /// <summary>
        /// The delegate for NuiFusionDepthFloatFrameToPointCloud function.
        /// </summary>
        private static NuiFusionDepthFloatFrameToPointCloudDelegate DepthFloatFrameToPointCloud;

        /// <summary>
        /// A delegate for NuiFusionShadePointCloud function.
        /// </summary>
        private static NuiFusionShadePointCloudDelegate ShadePointCloud;

        /// <summary>
        /// A delegate for NuiFusionShadePointCloud function.
        /// </summary>
        private static NuiFusionShadePointCloud2Delegate ShadePointCloud2;

        /// <summary>
        /// The delegate for NuiFusionAlignPointClouds function.
        /// </summary>
        private static NuiFusionAlignPointCloudsDelegate AlignPointClouds;

        /// <summary>
        /// The delegate for NuiFusionCreateReconstruction function.
        /// </summary>
        private static NuiFusionCreateReconstructionDelegate CreateReconstruction;

        /// <summary>
        /// The delegate for NuiFusionGetDeviceInfo function.
        /// </summary>
        private static NuiFusionGetDeviceInfoDelegate GetDeviceInfo;

        /// <summary>
        /// The native KinectFusion dll handle.
        /// </summary>
        private static IntPtr fusionModule;

        /// <summary>
        /// Create an image frame for use with Kinect Fusion with a specified data type and resolution.
        /// Note that image width must be a minimum of 32 pixels in both width and height and for camera 
        /// tracking Align functions and volume Integration, use of the default camera parameters is only 
        /// supported with 4:3 pixel aspect ratio images such as uniformly scaled versions of the source Kinect 
        /// image (e.g. 160x120,320x240,640x480 etc.). To crop to smaller non 4:3 ratios and still use the 
        /// default camera parameters set unwanted pixels to 0 depth, which will be ignored in processing, or 
        /// alternately, the user can supply their own calibration with an arbitrary sized image. For example,
        /// a user supplied set of parameters can be used when calling CalculatePointCloud to calculate a 
        /// large image of the reconstruction at the UI window resolution (perhaps with a virtual viewpoint 
        /// different to the Kinect camera or a non 4:3 aspect image ratio) by then subsequently calling 
        /// ShadePointCloud and rendering the resulting images on screen.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionCreateImageFrameDelegate(
                                                                 [In] FusionImageType frameType,
                                                                 [In] uint width,
                                                                 [In] uint height,
                                                                 [In, Optional] CameraParameters cameraParameters,
                                                                 [Out] out NativeFrameHandle imageFrame);

        /// <summary>
        /// Releases the specified frame of data.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionReleaseImageFrameDelegate([In] NativeFrameHandle imageFrame);

        /// <summary>
        /// Converts Kinect depth frames in unsigned short format to depth frames in float format 
        /// representing distance from the camera in meters (parallel to the optical center axis).
        /// Note: <paramref name="depthImageData"/> and <paramref name="depthFloatFrame"/> must
        /// be the same pixel resolution and equal to <paramref name="depthImageDataWidth"/> by
        /// <paramref name="depthImageDataHeight"/>.
        /// The min and max depth clip values enable clipping of the input data, for example, to help
        /// isolate particular objects or surfaces to be reconstructed. Note that the thresholds return 
        /// different values when a depth pixel is outside the threshold - pixels inside minDepthClip will
        /// will be returned as 0 and ignored in processing, whereas pixels beyond maxDepthClip will be set
        /// to 1000 to signify a valid depth ray with depth beyond the set threshold. Setting this far-
        /// distance flag is important for reconstruction integration in situations where the camera is
        /// static or does not move significantly, as it enables any voxels closer to the camera
        /// along this ray to be culled instead of persisting (as would happen if the pixels were simply 
        /// set to 0 and ignored in processing). Note that when reconstructing large real-world size volumes,
        /// be sure to set large maxDepthClip distances, as when the camera moves around, any voxels in view
        /// which go beyond this threshold distance from the camera will be removed.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionDepthToDepthFloatFrameDelegate(
                                                                       [In] DepthImagePixel[] depthImageData,
                                                                       [In] uint depthImageDataWidth,
                                                                       [In] uint depthImageDataHeight,
                                                                       [In] HandleRef depthFloatFrame,
                                                                       [In] float minDepthClip,
                                                                       [In] float maxDepthClip,
                                                                       [In, MarshalAs(UnmanagedType.Bool)] bool mirrorDepth);

        /// <summary>
        /// Construct an oriented point cloud in the local camera frame of reference from a depth float
        /// image frame. Here we calculate the 3D position of each depth float pixel with the optical
        /// center of the camera as the origin. Both images must be the same size and have the same camera
        /// parameters. We use a right-hand coordinate system, and (in common with bitmap images with top left
        /// origin) +X is to the right, +Y down, and +Z is now forward from the Kinect camera into the scene,
        /// as though looking into the scene from behind the kinect.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionDepthFloatFrameToPointCloudDelegate(
                                                                            [In] HandleRef depthFloatFrame,
                                                                            [In] HandleRef pointCloudFrame);

        /// <summary>
        /// Create visible color shaded images of a point cloud and its normals. All image frames must be
        /// the same size and have the same camera parameters.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionShadePointCloudDelegate(
                                                                [In] HandleRef pointCloudFrame,
                                                                [In] ref Matrix4 worldToCameraTransform,
                                                                [In] ref Matrix4 worldToBGRTransform,
                                                                [In] HandleRef shadedSurfaceFrame,
                                                                [In] HandleRef shadedSurfaceNormalsFrame);

        /// <summary>
        /// Create visible color shaded images of a point cloud and its normals. All image frames must be
        /// the same size and have the same camera parameters.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionShadePointCloud2Delegate(
                                                                [In] HandleRef pointCloudFrame,
                                                                [In] ref Matrix4 worldToCameraTransform,
                                                                [In] IntPtr worldToBGRTransform,
                                                                [In] HandleRef shadedSurfaceFrame,
                                                                [In] HandleRef shadedSurfaceNormalsFrame);

        /// <summary>
        /// The AlignPointClouds function uses an iterative algorithm to align two sets of oriented point 
        /// clouds and calculate the camera's relative pose. This is a generic function which can be used
        /// independently of a Reconstruction Volume with sets of overlapping point clouds.
        /// All images must be the same size and have the same camera parameters.
        /// To find the frame to frame relative transformation between two sets of point clouds in the 
        /// camera local frame of reference (created by NuiFusionDepthFloatFrameToPointCloud), set the
        /// pReferenceToObservedTransform to NULL or the identity.
        /// To calculate the pose transformation between new depth frames and an existing Reconstruction
        /// volume, pass in previous frame's point cloud from CalculatePointCloud as the reference frame,
        /// and the current frame point cloud (from NuiFusionDepthFloatFrameToPointCloud) as the observed
        /// frame. Set the pReferenceToObservedTransform to the previous frames calculated camera pose.
        /// Note that here the current frame point cloud will be in the camera local frame of reference,
        /// whereas the synthetic points and normals will be in the global/world volume coordinate system.
        /// By passing the pReferenceToObservedTransform you make the algorithm aware of the transformation
        /// between them.
        /// The pReferenceToObservedTransform pose supplied can also take into account information you may
        /// have from other sensors or sensing mechanisms to aid the tracking. 
        /// To do this multiply the relative frame to frame delta transformation from the other sensing
        /// system with the previous frame's pose before passing to this function.
        /// Note that any delta transform used should be in the same coordinate system as that returned 
        /// by the NuiFusionDepthFloatFrameToPointCloud calculation.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionAlignPointCloudsDelegate(
                                                                 [In] HandleRef referencePointCloudFrame,
                                                                 [In] HandleRef observedPointCloudFrame,
                                                                 [In] ushort maxAlignIterationCount,
                                                                 [In, Optional] HandleRef deltaFromReferenceFrame,
                                                                 [In, Out] ref Matrix4 observedToReferenceTransform);

        /// <summary>
        /// Create Kinect Fusion Reconstruction Volume instance.
        /// Voxel volume axis sizes must be greater than 0 and a multiple of 32.
        /// Users can select which device the processing is performed on with the reconstructionProcessorType parameter.
        /// For those with multiple devices the deviceIndex parameter also enables users to explicitly configure on 
        /// which device the reconstruction volume is created.
        /// Note that this function creates a default world-volume transform. To set a non-default
        /// transform call ResetReconstruction with an appropriate Matrix4. This default transform is a
        /// translation and scale to locate the world origin at the center of the front face of the volume
        /// and set the relationship between the the world coordinates and volume indices.
        /// </summary>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionCreateReconstructionDelegate(
                                                                     [In] ReconstructionParameters reconstructionParameters,
                                                                     [In] ReconstructionProcessor reconstructionProcessorType,
                                                                     [In] int deviceIndex,
                                                                     [In] ref Matrix4 initialWorldToCameraTransform,
                                                                     [Out] out INuiFusionReconstruction ppVolume);

        /// <summary>
        /// Enumerate the devices capable of running KinectFusion.
        /// This enables a specific device to be chosen when calling NuiFusionCreateReconstruction if desired.
        /// </summary>
        /// <param name="type">The type of processor to enumerate.</param>
        /// <param name="index">The zero-based index of the device for which the description is returned.</param>
        /// <param name="description">A buffer that receives a description string for the device.</param>
        /// <param name="descriptionSizeInChar">The size of the buffer referenced by <paramref name="description"/>, in characters.</param>
        /// <param name="instancePath">A buffer that receives the device instance string.</param>
        /// <param name="instancePathSizeInChar">The size of the buffer referenced by <paramref name="instancePath"/>, in characters.</param>
        /// <param name="memoryKB">On success, the variable is assigned the total amount of memory on the device, in kilobytes.</param>
        /// <returns>
        /// Returns S_OK if successful; otherwise, returns the error code.
        /// </returns>
        internal delegate HRESULT NuiFusionGetDeviceInfoDelegate(
                                                                 [In] ReconstructionProcessor type,
                                                                 [In] int index,
                                                                 [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description,
                                                                 [In] uint descriptionSizeInChar,
                                                                 [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder instancePath,
                                                                 [In] uint instancePathSizeInChar,
                                                                 [Out] out uint memoryKB);

        /// <summary>
        /// Get the delegate for the native NuiFusionCreateImageFrame function.
        /// </summary>
        internal static NuiFusionCreateImageFrameDelegate NuiFusionCreateImageFrame
        {
            get
            {
                if (CreateImageFrame == null)
                {
                    CreateImageFrame = GetFunctionDelegate<NuiFusionCreateImageFrameDelegate>("NuiFusionCreateImageFrame");
                }

                return CreateImageFrame;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionReleaseImageFrame function.
        /// </summary>
        internal static NuiFusionReleaseImageFrameDelegate NuiFusionReleaseImageFrame
        {
            get
            {
                if (ReleaseImageFrame == null)
                {
                    ReleaseImageFrame = GetFunctionDelegate<NuiFusionReleaseImageFrameDelegate>("NuiFusionReleaseImageFrame");
                }

                return ReleaseImageFrame;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionDepthToDepthFloatFrame function.
        /// </summary>
        internal static NuiFusionDepthToDepthFloatFrameDelegate NuiFusionDepthToDepthFloatFrame
        {
            get
            {
                if (DepthToDepthFloatFrame == null)
                {
                    DepthToDepthFloatFrame = GetFunctionDelegate<NuiFusionDepthToDepthFloatFrameDelegate>("NuiFusionDepthToDepthFloatFrame");
                }

                return DepthToDepthFloatFrame;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionDepthFloatFrameToPointCloud function.
        /// </summary>
        internal static NuiFusionDepthFloatFrameToPointCloudDelegate NuiFusionDepthFloatFrameToPointCloud
        {
            get
            {
                if (DepthFloatFrameToPointCloud == null)
                {
                    DepthFloatFrameToPointCloud = GetFunctionDelegate<NuiFusionDepthFloatFrameToPointCloudDelegate>("NuiFusionDepthFloatFrameToPointCloud");
                }

                return DepthFloatFrameToPointCloud;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionShadePointCloud function.
        /// </summary>
        internal static NuiFusionShadePointCloudDelegate NuiFusionShadePointCloud
        {
            get
            {
                if (ShadePointCloud == null)
                {
                    ShadePointCloud = GetFunctionDelegate<NuiFusionShadePointCloudDelegate>("NuiFusionShadePointCloud");
                }

                return ShadePointCloud;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionShadePointCloud2 function.
        /// </summary>
        internal static NuiFusionShadePointCloud2Delegate NuiFusionShadePointCloud2
        {
            get
            {
                if (ShadePointCloud2 == null)
                {
                    ShadePointCloud2 = GetFunctionDelegate<NuiFusionShadePointCloud2Delegate>("NuiFusionShadePointCloud");
                }

                return ShadePointCloud2;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionAlignPointClouds function.
        /// </summary>
        internal static NuiFusionAlignPointCloudsDelegate NuiFusionAlignPointClouds
        {
            get
            {
                if (AlignPointClouds == null)
                {
                    AlignPointClouds = GetFunctionDelegate<NuiFusionAlignPointCloudsDelegate>("NuiFusionAlignPointClouds");
                }

                return AlignPointClouds;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionCreateReconstruction function.
        /// </summary>
        internal static NuiFusionCreateReconstructionDelegate NuiFusionCreateReconstruction
        {
            get
            {
                if (CreateReconstruction == null)
                {
                    CreateReconstruction = GetFunctionDelegate<NuiFusionCreateReconstructionDelegate>("NuiFusionCreateReconstruction");
                }

                return CreateReconstruction;
            }
        }

        /// <summary>
        /// Get the delegate for the native NuiFusionGetDeviceInfo function.
        /// </summary>
        internal static NuiFusionGetDeviceInfoDelegate NuiFusionGetDeviceInfo
        {
            get
            {
                if (GetDeviceInfo == null)
                {
                    GetDeviceInfo = GetFunctionDelegate<NuiFusionGetDeviceInfoDelegate>("NuiFusionGetDeviceInfo");
                }

                return GetDeviceInfo;
            }
        }

        /// <summary>
        /// Get the library name for current execution architecture.
        /// </summary>
        private static string LibraryName
        {
            get { return sizeof(int) == IntPtr.Size ? "KinectFusion170_32.dll" : "KinectFusion170_64.dll"; }
        }

        // Get the native handle to the KinectFusion dll.
        private static IntPtr FusionModule
        {
            get
            {
                if (fusionModule == IntPtr.Zero)
                {
                    fusionModule = LoadLibrary(LibraryName);

                    if (fusionModule == IntPtr.Zero)
                    {
                        throw new InvalidOperationException(
                            string.Format(CultureInfo.InvariantCulture, Resources.LibraryInvocationFailed, LibraryName));
                    }
                }

                return fusionModule;
            }
        }

        /// <summary>
        /// Loads the specified dll into the address space of the calling process.
        /// </summary>
        /// <returns>
        /// If the function succeeds, the return value is a handle to the module.
        /// If the function fails, the return value is NULL. To get extended error information, call GetLastError
        /// </returns>
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibrary(string fileName);

        /// <summary>
        /// Retrieves the address of an exported function or variable from the specified
        /// dynamic-link library (DLL).
        /// </summary>
        /// <returns>
        /// If the function succeeds, the return value is a handle to the module.
        /// If the function fails, the return value is NULL. To get extended error information, call GetLastError
        /// </returns>
        [DllImport("kernel32", SetLastError = true, BestFitMapping = false)]
        internal static extern IntPtr GetProcAddress(IntPtr module, [MarshalAs(UnmanagedType.LPStr)] string procName);

        /// <summary>
        /// Get the delegate of a native function.
        /// </summary>
        /// <typeparam name="FunctionType">The function delegate type.</typeparam>
        /// <param name="functionName">The native function name to be loaded.</param>
        /// <returns>The delegate of the function.</returns>
        private static FunctionType GetFunctionDelegate<FunctionType>(string functionName)
            where FunctionType : class
        {
            // Find the entry point
            IntPtr nativeFunction = GetProcAddress(FusionModule, functionName);

            if (nativeFunction == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, Resources.LoadFunctionFailed, functionName));
            }

            // Get a delegate from it
            return Marshal.GetDelegateForFunctionPointer(
                    nativeFunction,
                    typeof(FunctionType)) as FunctionType;
        }
    }
}

//-----------------------------------------------------------------------
// <copyright file="Reconstruction.cs" company="Microsoft Corporation">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Kinect.Toolkit.Fusion
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The Reconstruction Processor type.
    /// </summary>
    public enum ReconstructionProcessor
    {
        /// <summary>
        /// Process all Reconstruction calls on CPU (potentially enables larger volume allocation,
        /// but not real-time processing)
        /// </summary>
        Cpu = 1,

        /// <summary>
        /// Process all Reconstruction calls on GPU using C++ AMP and any DirectX11 compatible GPU 
        /// (real-time reconstruction on suitable hardware)
        /// </summary>
        Amp = 2,
    }

    /// <summary>
    /// Reconstruction encapsulates reconstruction volume creation updating and meshing functions.
    /// </summary>
    public class Reconstruction : IDisposable
    {
        /// <summary>
        /// The native reconstruction interface wrapper.
        /// </summary>
        private INuiFusionReconstruction volume;
        private Matrix4 defaultWorldToVolumeTransform;

        /// <summary>
        /// Track whether Dispose has been called.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the Reconstruction class.
        /// Default constructor used to initialize with the native Reconstruction volume object.
        /// </summary>
        /// <param name="volume">
        /// The native Reconstruction volume object to be encapsulated.
        /// </param>
        internal Reconstruction(INuiFusionReconstruction volume)
        {
            this.volume = volume;
            defaultWorldToVolumeTransform = this.GetCurrentWorldToVolumeTransform();
        }

        /// <summary>
        /// Finalizes an instance of the Reconstruction class.
        /// This destructor will run only if the Dispose method does not get called.
        /// </summary>
        ~Reconstruction()
        {
            Dispose(false);
        }

        /// <summary>
        /// Initialize a Kinect Fusion 3D Reconstruction Volume.
        /// Voxel volume axis sizes must be greater than 0 and a multiple of 32.
        /// Users can select which device the processing is performed on with
        /// the <paramref name="reconstructionProcessorType"/> parameter. For those with multiple GPUs
        /// the <paramref name="deviceIndex"/> parameter also enables users to explicitly configure
        /// on which device the reconstruction volume is created.
        /// </summary>
        /// <param name="reconstructionParameters">
        /// The Reconstruction parameters to define the size and shape of the reconstruction volume.
        /// </param>
        /// <param name="reconstructionProcessorType">
        /// the processor type to be used for all calls to the reconstruction volume object returned
        /// from this function.
        /// </param>
        /// <param name="deviceIndex">Set this variable to an explicit zero-based device index to use
        /// a specific GPU as enumerated by NuiFusionGetDeviceInfo, or set to -1 to automatically
        /// select the default device for a given processor type.
        /// </param>
        /// <param name="initialWorldToCameraTransform">
        /// The initial camera pose of the reconstruction volume with respect to the world origin. 
        /// Pass identity as the default camera pose. 
        /// </param>
        /// <returns>The Reconstruction instance.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="reconstructionParameters"/> parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="reconstructionParameters"/> parameter's <c>VoxelX</c>,
        /// <c>VoxelY</c>, or <c>VoxelZ</c> member is not a greater than 0 and multiple of 32 or the
        /// <paramref name="deviceIndex"/> parameter is less than -1 or greater than the number of
        /// available devices for the respective processor type.
        /// </exception>
        /// <exception cref="OutOfMemoryException">
        /// Thrown when the memory required for the Reconstruction volume processing could not be
        /// allocated.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the Kinect device is not
        /// connected or the Reconstruction volume is too big so a GPU memory
        /// allocation failed, or the call failed for an unknown reason.
        /// </exception>
        public static Reconstruction FusionCreateReconstruction(
            ReconstructionParameters reconstructionParameters,
            ReconstructionProcessor reconstructionProcessorType,
            int deviceIndex,
            Matrix4 initialWorldToCameraTransform)
        {
            if (null == reconstructionParameters)
            {
                throw new ArgumentNullException("reconstructionParameters");
            }

            INuiFusionReconstruction reconstruction = null;

            ExceptionHelper.ThrowIfFailed(NativeMethods.NuiFusionCreateReconstruction(
                reconstructionParameters,
                reconstructionProcessorType,
                deviceIndex,
                ref initialWorldToCameraTransform,
                out reconstruction));

            return new Reconstruction(reconstruction);
        }

        /// <summary>
        /// Clear the volume, and set a new camera pose or identity. 
        /// This internally sets the default world to volume transform. where the Kinect camera is
        /// translated to the center of the front face of the volume cube, looking into the cube, and
        /// the world coordinates are scaled to volume indices according to the voxels per meter setting.
        /// </summary>
        /// <param name="initialWorldToCameraTransform">
        /// The initial camera pose with respect to the world origin. 
        /// Pass identity as the default camera pose. 
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected,
        /// or the call failed for an unknown reason.
        /// </exception>
        public void ResetReconstruction(
            Matrix4 initialWorldToCameraTransform)
        {
            ExceptionHelper.ThrowIfFailed(volume.ResetReconstruction(
                ref initialWorldToCameraTransform,
                ref defaultWorldToVolumeTransform));
        }

        /// <summary>
        /// Clear the volume, and set a new camera pose or identity, and set a new world-volume 
        /// transform.
        /// </summary>
        /// <param name="initialWorldToCameraTransform">
        /// The initial camera pose with respect to the world origin. 
        /// Pass identity as the default camera pose. 
        /// </param>
        /// <param name="worldToVolumeTransform">A  Matrix4 instance, containing the world to volume
        /// transform. This controls where the reconstruction volume appears in the real world with 
        /// respect to the world origin position.
        /// To create your own transformation first get the current transform by calling
        /// GetCurrentWorldToVolumeTransform then either modify the matrix directly or multiply
        /// with your own similarity matrix to alter the volume translation or rotation with respect
        /// to the world coordinate system. Note that other transforms such as skew are not supported.
        /// To reset the volume while keeping the same world-volume transform, first get the current
        /// transform by calling GetCurrentWorldToVolumeTransform and pass this Matrix4 as the
        /// <paramref name="worldToVolumeTransform"/> parameter when calling this reset
        /// function. </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected,
        /// or the call failed for an unknown reason.
        /// </exception>
        public void ResetReconstruction(
            Matrix4 initialWorldToCameraTransform, 
            Matrix4 worldToVolumeTransform)
        {
            ExceptionHelper.ThrowIfFailed(volume.ResetReconstruction(
                ref initialWorldToCameraTransform,
                ref worldToVolumeTransform));
        }

        /// <summary>
        /// Aligns a depth float image to the Reconstruction volume to calculate the new camera pose.
        /// This camera tracking method requires a Reconstruction volume, and updates the internal 
        /// camera pose if successful. The maximum image resolution supported in this function is 640x480.
        /// Note that this function is designed primarily for tracking either with static scenes when
        /// performing environment reconstruction, or objects which move rigidly when performing object
        /// reconstruction from a static camera. Consider using the standalone function
        /// NuiFusionAlignPointClouds instead if tracking failures occur due to parts of a scene which
        /// move non-rigidly or should be considered as outliers, although in practice, such issues are 
        /// best avoided by carefully designing or constraining usage scenarios wherever possible.
        /// </summary>
        /// <param name="depthFloatFrame">The depth float frame to be processed.</param>
        /// <param name="maxAlignIterationCount">
        /// The maximum number of iterations of the algorithm to run. 
        /// The minimum value is 1. Using only a small number of iterations will have a faster runtime,
        /// however, the algorithm may not converge to the correct transformation.
        /// </param>
        /// <param name="deltaFromReferenceFrame">
        /// Optionally, a pre-allocated float image frame, to be filled with information about how
        /// well each observed pixel aligns with the passed in reference frame. This maybe processed
        /// to create a color rendering, or may be used as input to additional vision algorithms such
        /// as object segmentation. Pass null if not required.
        /// </param>
        /// <param name="alignmentEnergy">
        /// A float to receive a value describing how well the observed frame aligns to the model with
        /// the calculated pose. A larger magnitude value represent more discrepancy, and a lower value
        /// represent less discrepancy. Note that it is unlikely an exact 0 (perfect alignment) value will
        /// ever be returned as every frame from the sensor will contain some sensor noise.
        /// </param>
        /// <param name="worldToCameraTransform">
        /// The best guess of the camera pose (usually the camera pose result from the last
        /// AlignPointClouds or AlignDepthFloatToReconstruction).
        /// </param>
        /// <returns>
        /// Returns true if successful; return false if the algorithm encountered a problem aligning
        /// the input depth image and could not calculate a valid transformation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="depthFloatFrame"/> parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="maxAlignIterationCount"/> parameter is less than 1.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected
        /// or the call failed for an unknown reason.
        /// </exception>
        public bool AlignDepthFloatToReconstruction(
            FusionFloatImageFrame depthFloatFrame,
            int maxAlignIterationCount,
            FusionFloatImageFrame deltaFromReferenceFrame,
            out float alignmentEnergy,
            Matrix4 worldToCameraTransform)
        {
            if (null == depthFloatFrame)
            {
                throw new ArgumentNullException("depthFloatFrame");
            }

            ushort maxIterations = ExceptionHelper.CastAndThrowIfOutOfUshortRange(maxAlignIterationCount);

            HRESULT hr = volume.AlignDepthFloatToReconstruction(
                FusionImageFrame.ToHandleRef(depthFloatFrame),
                maxIterations,
                FusionImageFrame.ToHandleRef(deltaFromReferenceFrame),
                out alignmentEnergy,
                ref worldToCameraTransform);

            if (hr == HRESULT.E_NUI_FUSION_TRACKING_ERROR)
            {
                return false;
            }
            else
            {
                ExceptionHelper.ThrowIfFailed(hr);
            }

            return true;
        }

        /// <summary>
        /// Get current internal camera pose.
        /// </summary>
        /// <returns>The current world to camera pose.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the call failed for an unknown reason.
        /// </exception>
        public Matrix4 GetCurrentWorldToCameraTransform()
        {
            Matrix4 cameraPose;
            ExceptionHelper.ThrowIfFailed(volume.GetCurrentWorldToCameraTransform(out cameraPose));

            return cameraPose;
        }

        /// <summary>
        /// Get current internal world to volume transform.
        /// Note: A right handed coordinate system is used, with the origin of the volume (i.e. voxel 0,0,0) 
        /// at the top left of the front plane of the cube. Similar to bitmap images with top left origin, 
        /// +X is to the right, +Y down, and +Z is forward from origin into the reconstruction volume.
        /// The default transform is a combination of translation in X,Y to locate the world origin at the
        /// center of the front face of the reconstruction volume cube, and scaling by the voxelsPerMeter
        /// reconstruction parameter to convert from world coordinate system to volume voxel indices.
        /// </summary>
        /// <returns>The current world to volume transform. This is a similarity transformation
        ///  that converts world coordinates to volume coordinates.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the call failed for an unknown reason.
        /// </exception>
        public Matrix4 GetCurrentWorldToVolumeTransform()
        {
            Matrix4 transform;
            ExceptionHelper.ThrowIfFailed(volume.GetCurrentWorldToVolumeTransform(out transform));

            return transform;
        }

        /// <summary>
        /// Integrates depth float data into the reconstruction volume from the 
        /// worldToCameraTransform camera pose.
        /// </summary>
        /// <param name="depthFloatFrame">The depth float frame to be integrated.</param>
        /// <param name="maxIntegrationWeight">
        /// A parameter to control the temporal smoothing of depth integration. Minimum value is 1.
        /// Lower values have more noisy representations, but objects that move integrate and 
        /// disintegrate faster, so are suitable for more dynamic environments. Higher values
        /// integrate objects more slowly, but provides finer detail with less noise.</param>
        /// <param name="worldToCameraTransform">
        /// The camera pose (usually the camera pose result from the last AlignPointClouds or 
        /// AlignDepthFloatToReconstruction).
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="depthFloatFrame"/> parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="maxIntegrationWeight"/> parameter is less than 1
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected
        /// or the call failed for an unknown reason.
        /// </exception>
        public void IntegrateFrame(
            FusionFloatImageFrame depthFloatFrame,
            int maxIntegrationWeight,
            Matrix4 worldToCameraTransform)
        {
            if (null == depthFloatFrame)
            {
                throw new ArgumentNullException("depthFloatFrame");
            }

            ushort integrationWeight = ExceptionHelper.CastAndThrowIfOutOfUshortRange(maxIntegrationWeight);

            ExceptionHelper.ThrowIfFailed(volume.IntegrateFrame(
                FusionImageFrame.ToHandleRef(depthFloatFrame),
                integrationWeight,
                ref worldToCameraTransform));
        }

        /// <summary>
        /// A high-level function to process a depth frame through the Kinect Fusion pipeline.
        /// Specifically, this performs on-GPU processing equivalent to the following functions
        /// for each frame:
        /// <para>
        /// 1) AlignDepthFloatToReconstruction
        /// 2) IntegrateFrame
        /// </para>
        /// Users may also optionally call the low-level functions individually, instead of calling this
        /// function, for more control. However, this function call will be faster due to the integrated 
        /// nature of the calls. After this call completes, if a visible output image of the reconstruction
        /// is required, the user can call CalculatePointCloud and then ShadePointCloud.
        /// The maximum image resolution supported in this function is 640x480.
        /// <para/>
        /// If there is a tracking error in the AlignDepthFloatToReconstruction stage, no depth data 
        /// integration will be performed, and the camera pose will remain unchanged.
        /// </summary>
        /// <param name="depthFloatFrame">The depth float frame to be processed.</param>
        /// <param name="maxAlignIterationCount">
        /// The maximum number of iterations of the align camera tracking algorithm to run.
        /// The minimum value is 1. Using only a small number of iterations will have a faster
        /// runtime, however, the algorithm may not converge to the correct transformation.
        /// </param>
        /// <param name="maxIntegrationWeight">
        /// A parameter to control the temporal smoothing of depth integration. Lower values have
        /// more noisy representations, but objects that move appear and disappear faster, so are
        /// suitable for more dynamic environments. Higher values integrate objects more slowly,
        /// but provides finer detail with less noise.
        /// </param>
        /// <param name="worldToCameraTransform">
        /// The best guess of the latest camera pose (usually the camera pose result from the last
        /// process call).
        /// </param>
        /// <returns>
        /// Returns true if successful; return false if the algorithm encountered a problem aligning
        /// the input depth image and could not calculate a valid transformation.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="depthFloatFrame"/> parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="maxAlignIterationCount"/> parameter is less than 1.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected,
        /// or the call failed for an unknown reason.
        /// </exception>
        public bool ProcessFrame(
            FusionFloatImageFrame depthFloatFrame,
            int maxAlignIterationCount,
            int maxIntegrationWeight,
            Matrix4 worldToCameraTransform)
        {
            if (null == depthFloatFrame)
            {
                throw new ArgumentNullException("depthFloatFrame");
            }

            ushort maxIterations = ExceptionHelper.CastAndThrowIfOutOfUshortRange(maxAlignIterationCount);
            ushort maxWeight = ExceptionHelper.CastAndThrowIfOutOfUshortRange(maxIntegrationWeight);

            HRESULT hr = volume.ProcessFrame(
                FusionImageFrame.ToHandleRef(depthFloatFrame),
                maxIterations,
                maxWeight,
                ref worldToCameraTransform);

            if (hr == HRESULT.E_NUI_FUSION_TRACKING_ERROR)
            {
                return false;
            }
            else
            {
                ExceptionHelper.ThrowIfFailed(hr);
            }

            return true;
        }

        /// <summary>
        /// Calculate a point cloud by raycasting into the reconstruction volume, returning the point
        /// cloud containing 3D points and normals of the zero-crossing dense surface at every visible
        /// pixel in the image from the given camera pose.
        /// This point cloud can be used as a reference frame in the next call to
        /// FusionDepthProcessor.AlignPointClouds, or passed to FusionDepthProcessor.ShadePointCloud
        /// to produce a visible image output.
        /// The <paramref name="pointCloudFrame"/> can be an arbitrary image size, for example, enabling
        /// you to calculate point clouds at the size of your window and then create a visible image by
        /// calling FusionDepthProcessor.ShadePointCloud and render this image, however, be aware that 
        /// large images will be expensive to calculate.
        /// </summary>
        /// <param name="pointCloudFrame">
        /// The pre-allocated point cloud frame, to be filled by raycasting into the reconstruction volume.
        /// Typically used as the reference frame with the FusionDepthProcessor.AlignPointClouds function
        /// or for visualization by calling FusionDepthProcessor.ShadePointCloud.
        /// </param>
        /// <param name="worldToCameraTransform">
        /// The world to camera transform (camera pose) to raycast from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="pointCloudFrame"/> parameter is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the call failed for an unknown reason.
        /// </exception>
        public void CalculatePointCloud(
            FusionPointCloudImageFrame pointCloudFrame,
            Matrix4 worldToCameraTransform)
        {
            if (null == pointCloudFrame)
            {
                throw new ArgumentNullException("pointCloudFrame");
            }

            ExceptionHelper.ThrowIfFailed(volume.CalculatePointCloud(
                FusionImageFrame.ToHandleRef(pointCloudFrame),
                ref worldToCameraTransform));
        }

        /// <summary>
        /// Export a mesh of the zero-crossing dense surfaces from the reconstruction volume.
        /// </summary>
        /// <param name="voxelStep">
        /// The step value in voxels for sampling points to use in the volume when exporting
        /// a mesh, which determines the final resolution of the mesh. Use higher values for 
        /// lower resolution meshes. voxelStep must be greater than 0 and smaller than the smallest
        ///  volume axis voxel resolution. Default is 1.
        /// </param>
        /// <returns>Returns the mesh object created by Kinect Fusion.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="voxelStep"/> parameter is out of range.
        /// </exception>
        /// <exception cref="OutOfMemoryException">
        /// Thrown if the CPU memory required for mesh calculation could not be allocated.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected,
        /// a GPU memory allocation failed or the call failed for an unknown reason.
        /// </exception>
        public Mesh CalculateMesh(int voxelStep)
        {
            INuiFusionMesh mesh = null;

            uint step = ExceptionHelper.CastAndThrowIfOutOfUintRange(voxelStep);

            ExceptionHelper.ThrowIfFailed(volume.CalculateMesh(step, out mesh));

            return new Mesh(mesh);
        }

        /// <summary>
        /// Export a part or all of the reconstruction volume as a SHORT array. 
        /// The surface boundary occurs where the tri-linearly interpolated voxel values have a zero crossing.
        /// Note, this means that a 0 in the volume does not necessarily imply a surface.  A surface only 
        /// occurs when an interpolation crosses from positive to negative or vice versa.   
        /// </summary>
        /// <param name="sourceOriginX">The reconstruction volume voxel index in the X axis from which the 
        /// extraction should begin. This value must be greater than or equal to 0 and less than the
        /// reconstruction volume X axis voxel resolution.</param>
        /// <param name="sourceOriginY">The reconstruction volume voxel index in the Y axis from which the 
        /// extraction should begin. This value must be greater than or equal to 0 and less than the
        /// reconstruction volume Y axis voxel resolution.</param>
        /// <param name="sourceOriginZ">The reconstruction volume voxel index in the Z axis from which the 
        /// extraction should begin. This value must be greater than or equal to 0 and less than the
        /// reconstruction volume Z axis voxel resolution.</param>
        /// <param name="destinationResolutionX">The X axis resolution/width of the new voxel volume to return
        /// in the array. This value must be greater than 0 and less than or equal to the current volume X 
        /// axis voxel resolution. The final count of (sourceOriginX+(destinationResolutionX*voxelStep) must 
        /// not be greater than the current reconstruction volume X axis voxel resolution.</param>
        /// <param name="destinationResolutionY">The Y axis resolution/height of the new voxel volume to return
        /// in the array. This value must be greater than 0 and less than or equal to the current volume Y 
        /// axis voxel resolution. The final count of (sourceOriginY+(destinationResolutionY*voxelStep) must 
        /// not be greater than the current reconstruction volume Y axis voxel resolution.</param>
        /// <param name="destinationResolutionZ">The Z axis resolution/depth of the new voxel volume to return
        /// in the array. This value must be greater than 0 and less than or equal to the current volume Z 
        /// axis voxel resolution. The final count of (sourceOriginZ+(destinationResolutionZ*voxelStep) must 
        /// not be greater than the current reconstruction volume Z axis voxel resolution.</param>
        /// <param name="voxelStep">The step value in integer voxels for sampling points to use in the
        /// volume when exporting. The value must be greater than 0 and less than the smallest 
        /// volume axis voxel resolution. To export the volume at its full resolution, use a step value of 1. 
        /// Use higher step values to skip voxels and return the new volume as if there were a lower effective 
        /// resolution volume. For example, when exporting with a destination resolution of 320^3, setting 
        /// voxelStep to 2 would actually cover a 640^3 voxel are a(destinationResolution*voxelStep) in the 
        /// source reconstruction, but the data returned would skip every other voxel in the original volume.
        /// NOTE:  Any value higher than 1 for this value runs the risk of missing zero crossings, and hence
        /// missing surfaces or surface details.</param>
        /// <param name="volumeBlock">A pre-allocated short array to be filled with 
        /// volume data. The number of elements in this user array should be allocated as:
        /// (destinationResolutionX * destinationResolutionY * destinationResolutionZ) 
        /// To access the voxel located at x,y,z use pVolume[z][y][x], or index as 1D array for a particular
        /// voxel(x,y,z) as follows: with pitch = x resolution, slice = (y resolution * pitch)
        /// unsigned int index = (z * slice)  + (y * pitch) + x;
        /// Note: A right handed coordinate system is used, with the origin of the volume (i.e. voxel 0,0,0) 
        /// at the top left of the front plane of the cube. Similar to bitmap images with top left origin, 
        /// +X is to the right, +Y down, and +Z is forward from origin into the reconstruction volume.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the <paramref name="volumeBlock"/> parameter is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the <paramref name="volumeBlock"/> parameter length is not equal to
        /// (<paramref name="destinationResolutionX"/> * <paramref name="destinationResolutionY"/> *
        /// <paramref name="destinationResolutionZ"/>), or a parameter was out of range.
        /// </exception>
        /// <exception cref="OutOfMemoryException">
        /// Thrown if the CPU or GPU memory required for volume export could not be allocated.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Kinect Runtime could not be accessed, the device is not connected,
        /// or the call failed for an unknown reason.
        /// </exception>
        public void ExportVolumeBlock(
            int sourceOriginX,
            int sourceOriginY,
            int sourceOriginZ,
            int destinationResolutionX,
            int destinationResolutionY,
            int destinationResolutionZ,
            int voxelStep,
            short[] volumeBlock)
        {
            if (null == volumeBlock)
            {
                throw new ArgumentNullException("volumeBlock");
            }
            
            uint srcX = ExceptionHelper.CastAndThrowIfOutOfUintRange(sourceOriginX);
            uint srcY = ExceptionHelper.CastAndThrowIfOutOfUintRange(sourceOriginY);
            uint srcZ = ExceptionHelper.CastAndThrowIfOutOfUintRange(sourceOriginZ);

            uint destX = ExceptionHelper.CastAndThrowIfOutOfUintRange(destinationResolutionX);
            uint destY = ExceptionHelper.CastAndThrowIfOutOfUintRange(destinationResolutionY);
            uint destZ = ExceptionHelper.CastAndThrowIfOutOfUintRange(destinationResolutionZ);

            uint step = ExceptionHelper.CastAndThrowIfOutOfUintRange(voxelStep);

            if (volumeBlock.Length != (destX * destY * destZ))
            {
                throw new ArgumentException("volumeBlock");
            }

            ExceptionHelper.ThrowIfFailed(volume.ExportVolumeBlock(srcX, srcY, srcZ, destX, destY, destZ, step, (uint)volumeBlock.Length * sizeof(short), volumeBlock));
        }

        /// <summary>
        /// Disposes the Reconstruction.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees all memory associated with the Reconstruction.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                Marshal.FinalReleaseComObject(volume);
                disposed = true;
            }
        }
    }
}

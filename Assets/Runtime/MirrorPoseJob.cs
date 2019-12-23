using System;
using UnityEngine;
using Unity.Collections;

#if UNITY_2019_3_OR_NEWER
using UnityEngine.Animations;
#else
using UnityEngine.Experimental.Animations;
#endif


namespace Lilium
{


    public struct MirrorPoseJob : IAnimationJob, IDisposable
    {

        public struct MirroringConstrant
        {
            public TransformStreamHandle driven;
            public TransformStreamHandle source;
        }

        public struct MirroringTransform
        {
            public TransformStreamHandle source;
            public TransformStreamHandle driven;
        }

        public bool debug;
        public bool mirror;

        public TransformStreamHandle root;

        public NativeArray<MirroringTransform> mirroringTransforms;
        public NativeArray<MirroringConstrant> mirroringConstrants;

        public void ProcessRootMotion (AnimationStream stream) { }

        public void ProcessAnimation (AnimationStream stream)
        {
            Vector3 rootPosition;
            Quaternion rootRotation;
            root.GetGlobalTR (stream, out rootPosition, out rootRotation);
            var rootTx = new AffineTransform (rootPosition, rootRotation);

            var mirroredTransforms = new NativeArray<AffineTransform> (mirroringTransforms.Length, Allocator.Temp);

            // 追加トランスフォームのミラーリング計算
            if (mirror) {
                for (int i = 0; i < mirroringTransforms.Length; i++) {

                    if (!mirroringTransforms[i].source.IsValid (stream)) continue;
                    if (!mirroringTransforms[i].driven.IsValid (stream)) continue;

                    Vector3 position;
                    Quaternion rotation;
                    mirroringTransforms[i].source.GetGlobalTR (stream, out position, out rotation);

                    var drivenTx = new AffineTransform (position, rotation);
                    drivenTx = rootTx.Inverse() * drivenTx;
                    drivenTx = AnimationStreamMirrorExtensions.Mirrored (drivenTx);
                    drivenTx = rootTx * drivenTx;
                    mirroredTransforms[i] = drivenTx;
                }
            }

            // Humanoid ミラーリング
            if (stream.isHumanStream) {
                AnimationHumanStream humanStream = stream.AsHuman ();

                if (mirror) {
                    humanStream.MirrorPose ();
                }

                humanStream.SolveIK ();
            }

            // 追加トランスフォームのミラーリング適用
            if (mirror) {
                for (int i = 0; i < mirroringTransforms.Length; i++) {

                    if (!mirroringTransforms[i].source.IsValid (stream)) continue;
                    if (!mirroringTransforms[i].driven.IsValid (stream)) continue;

                    mirroringTransforms[i].driven.SetGlobalTR (stream, mirroredTransforms[i].position, mirroredTransforms[i].rotation, false);
                }
            }

            // 追加トランスフォームのミラーリング拘束
            if (mirror) {
                for (int i = 0; i < mirroringConstrants.Length; i++) {

                    if (!mirroringConstrants[i].source.IsValid (stream)) continue;
                    if (!mirroringConstrants[i].driven.IsValid (stream)) continue;

                    Vector3 position;
                    Quaternion rotation;
                    mirroringConstrants[i].source.GetGlobalTR (stream, out position, out rotation);
                    mirroringConstrants[i].driven.SetGlobalTR (stream, position, rotation, false);
                }
            }
        }

        public static MirrorPoseJob Create (TransformStreamHandle root, int mirroringTransformsLength, int mirroringParentConstraintsLength)
        {
            return new MirrorPoseJob {
                mirror = false,
                root = root,
                mirroringTransforms = new NativeArray<MirroringTransform> (mirroringTransformsLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                mirroringConstrants = new NativeArray<MirroringConstrant> (mirroringParentConstraintsLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
            };
        }

        public void Dispose ()
        {
            mirroringTransforms.Dispose ();
            mirroringConstrants.Dispose ();
        }

    }

}
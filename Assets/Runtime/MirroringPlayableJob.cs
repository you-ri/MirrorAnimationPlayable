using UnityEngine;
using UnityEngine.Playables;
using System;

#if UNITY_2019_3_OR_NEWER
using UnityEngine.Animations;
#else
using UnityEngine.Experimental.Animations;
#endif
using Unity.Collections;


namespace Lilium
{

    /// <summary>
    /// based on: com.unity.animation.rigging@0.2.5-preview\Runtime\Utils\AffineTransform
    /// </summary>
    [System.Serializable]
    public struct AffineTransform
    {
        public Vector3 position;
        public Quaternion rotation;

        public AffineTransform (Vector3 p, Quaternion r)
        {
            position = p;
            rotation = r;
        }

        public Vector3 Transform (Vector3 p) =>
            rotation * p + position;

        public AffineTransform Inverse ()
        {
            var invR = Quaternion.Inverse (rotation);
            return new AffineTransform (invR * -position, invR);
        }


        public static AffineTransform operator * (AffineTransform lhs, AffineTransform rhs) =>
            new AffineTransform (lhs.Transform (rhs.position), lhs.rotation * rhs.rotation);
    }


    /// <summary>
    /// based on: https://forum.unity.com/threads/playables-api-mirroring-clips-and-directorupdatemode-manual.504533/
    /// </summary>
    public static class AnimationStreamMirrorExtensions
    {
        private static readonly float[] BodyDoFMirror = new float[] {
            +1.0f,  // BodyDof.SpineFrontBack,
            -1.0f,  // BodyDof.SpineLeftRight,
            -1.0f,  // BodyDof.SpineRollLeftRight,
            +1.0f,  // BodyDof.ChestFrontBack,
            -1.0f,  // BodyDof.ChestLeftRight,
            -1.0f,  // BodyDof.ChestRollLeftRight,
            +1.0f,  // BodyDof.UpperChestFrontBack,
            -1.0f,  // BodyDof.UpperChestLeftRight,
            -1.0f   // BodyDof.UpperChestRollLeftRight,
        };

        private static readonly float[] HeadDoFMirror = new float[] {
            +1.0f,  // HeadDof.NeckFrontBack,
            -1.0f,  // HeadDof.NeckLeftRight,
            -1.0f,  // HeadDof.NeckRollLeftRight,
            +1.0f,  // HeadDof.HeadFrontBack,
            -1.0f,  // HeadDof.HeadLeftRight,
            -1.0f,  // HeadDof.HeadRollLeftRight,
            +1.0f,  // HeadDof.LeftEyeDownUp,
            -1.0f,  // HeadDof.LeftEyeLeftRight,
            +1.0f,  // HeadDof.RightEyeDownUp,
            -1.0f,  // HeadDof.RightEyeLeftRight,
            +1.0f,  // HeadDof.JawDownUp,
            -1.0f   // HeadDof.JawLeftRight,
        };

        public static void MultMuscle (this AnimationHumanStream stream, MuscleHandle h, float f)
        {
            stream.SetMuscle (h, f * stream.GetMuscle (h));
        }

        public static void SwapMuscles (this AnimationHumanStream stream, MuscleHandle a, MuscleHandle b)
        {
            var t = stream.GetMuscle (a);
            stream.SetMuscle (a, stream.GetMuscle (b));
            stream.SetMuscle (b, t);
        }

        public static void MirrorPose (this AnimationHumanStream humanStream)
        {
            // mirror body
            for (int i = 0; i < (int)BodyDof.LastBodyDof; i++) {
                humanStream.MultMuscle (new MuscleHandle ((BodyDof)i), BodyDoFMirror[i]);
            }

            // mirror head
            for (int i = 0; i < (int)HeadDof.LastHeadDof; i++) {
                humanStream.MultMuscle (new MuscleHandle ((HeadDof)i), HeadDoFMirror[i]);
            }

            // swap arms
            for (int i = 0; i < (int)ArmDof.LastArmDof; i++) {
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftArm, (ArmDof)i),
                    new MuscleHandle (HumanPartDof.RightArm, (ArmDof)i));
            }

            // swap legs
            for (int i = 0; i < (int)LegDof.LastLegDof; i++) {
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftLeg, (LegDof)i),
                    new MuscleHandle (HumanPartDof.RightLeg, (LegDof)i));
            }

            // swap fingers
            for (int i = 0; i < (int)FingerDof.LastFingerDof; i++) {
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftThumb, (FingerDof)i),
                    new MuscleHandle (HumanPartDof.RightThumb, (FingerDof)i));
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftIndex, (FingerDof)i),
                    new MuscleHandle (HumanPartDof.RightIndex, (FingerDof)i));
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftMiddle, (FingerDof)i),
                    new MuscleHandle (HumanPartDof.RightMiddle, (FingerDof)i));
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftRing, (FingerDof)i),
                    new MuscleHandle (HumanPartDof.RightRing, (FingerDof)i));
                humanStream.SwapMuscles (
                    new MuscleHandle (HumanPartDof.LeftLittle, (FingerDof)i),
                    new MuscleHandle (HumanPartDof.RightLittle, (FingerDof)i));
            }


            humanStream.bodyLocalPosition = Mirrored(humanStream.bodyLocalPosition);

            // mirror rotation (invert Y axis angle)
            humanStream.bodyLocalRotation = Mirrored(humanStream.bodyLocalRotation);

            // swap ik
            Vector3[] goalPositions = new Vector3[4];
            Quaternion[] goalRotations = new Quaternion[4];
            float[] goalWeightPositons = new float[4];
            float[] goalWeightRotations = new float[4];
            Vector3[] hintPositions = new Vector3[4];
            float[] hintWeightPositions = new float[4];
            for (int i = 0; i < 4; i++) {
                goalPositions[i] = humanStream.GetGoalLocalPosition (AvatarIKGoal.LeftFoot + i);
                goalRotations[i] = humanStream.GetGoalLocalRotation (AvatarIKGoal.LeftFoot + i);
                goalWeightPositons[i] = humanStream.GetGoalWeightPosition (AvatarIKGoal.LeftFoot + i);
                goalWeightRotations[i] = humanStream.GetGoalWeightRotation (AvatarIKGoal.LeftFoot + i);
                hintPositions[i] = humanStream.GetHintPosition (AvatarIKHint.LeftKnee + i);
                hintWeightPositions[i] = humanStream.GetHintWeightPosition (AvatarIKHint.LeftKnee + i);
            }
            for (int i = 0; i < 4; i++) {
                int j = (i + 1) % 2 + (i / 2) * 2;                  // make [1, 0, 3, 2]
                humanStream.SetGoalLocalPosition (AvatarIKGoal.LeftFoot + i, Mirrored(goalPositions[j]));
                humanStream.SetGoalLocalRotation (AvatarIKGoal.LeftFoot + i, Mirrored(goalRotations[j]));
                humanStream.SetGoalWeightPosition (AvatarIKGoal.LeftFoot + i, goalWeightPositons[j]);
                humanStream.SetGoalWeightRotation (AvatarIKGoal.LeftFoot + i, goalWeightRotations[j]);
                humanStream.SetHintPosition (AvatarIKHint.LeftKnee + i, hintPositions[j]);
                humanStream.SetHintWeightPosition (AvatarIKHint.LeftKnee + i, hintWeightPositions[j]);
            }

        }

        // mirror position (invert X coordinate)
        public static Vector3 Mirrored (Vector3 value)
        {
            return new Vector3 (-value.x, value.y, value.z);
        }

        // mirror rotation (invert Y,Z axis angle)
        public static Quaternion Mirrored (Quaternion value)
        {
            return Quaternion.Euler (value.eulerAngles.x, -value.eulerAngles.y, -value.eulerAngles.z);
        }

        // mirror affine transform
        public static AffineTransform Mirrored (AffineTransform value)
        {
            return new AffineTransform (Mirrored (value.position), Mirrored (value.rotation));
        }

    }

    public struct MirroringPlayableJob : IAnimationJob, IDisposable
    {

        public struct MirroringConstrant
        {
            public TransformStreamHandle driven;
            public TransformStreamHandle source;
        }

        public struct MirroringPosture
        {
            public TransformStreamHandle source;
            public TransformStreamHandle driven;
        }

        public bool debug;
        public bool isMirror;

        public TransformStreamHandle root;

        public NativeArray<MirroringPosture> mirroringTransforms;
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
            if (isMirror) {
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

                if (isMirror) {
                    humanStream.MirrorPose ();
                }

                humanStream.SolveIK ();
            }

            // 追加トランスフォームのミラーリング適用
            if (isMirror) {
                for (int i = 0; i < mirroringTransforms.Length; i++) {

                    if (!mirroringTransforms[i].source.IsValid (stream)) continue;
                    if (!mirroringTransforms[i].driven.IsValid (stream)) continue;

                    mirroringTransforms[i].driven.SetGlobalTR (stream, mirroredTransforms[i].position, mirroredTransforms[i].rotation, false);
                }
            }

            // 追加トランスフォームのミラーリング拘束
            if (isMirror) {
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

        public static MirroringPlayableJob Create (TransformStreamHandle root, int mirroringTransformsLength, int mirroringParentConstraintsLength)
        {
            return new MirroringPlayableJob {
                isMirror = false,
                root = root,
                mirroringTransforms = new NativeArray<MirroringPosture> (mirroringTransformsLength, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
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
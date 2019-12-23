using System.Collections;
using System.Collections.Generic;
using UnityEngine;


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

}
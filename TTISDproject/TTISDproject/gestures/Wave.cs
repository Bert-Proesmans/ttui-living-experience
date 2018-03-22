using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;

using Emgu.CV;

using static TTISDproject.gestures.GestureEventArgs;
using Emgu.CV.Structure;
using System.Diagnostics;
using System.Drawing;

namespace TTISDproject.gestures
{
    class RHWaveSegment1 : IGestureSegment
    {
        public GesturePartResult Update(KinectSensor sensor, Skeleton skeleton)
        {
            var hand_point = skeleton.Joints[JointType.HandRight].Position;
            var elbow_point = skeleton.Joints[JointType.ElbowRight].Position;
            //
            double hand_x = hand_point.X;
            double hand_y = hand_point.Y;
            // 
            double elbow_x = elbow_point.X;
            double elbow_y = elbow_point.Y;

            // Debug.WriteLine("RHS SEG 1: HAND X: {0}, Y: {1}", hand_x, hand_y);
            // Debug.WriteLine("RHS SEG 1: ELB X: {0}, Y: {1}", elbow_x, elbow_y);

            // Hand above elbow
            if (hand_y > elbow_y)
            {
                // Debug.WriteLine("Above");
                // Hand right of elbow
                if (hand_x > elbow_x)
                {
                    // Debug.WriteLine("Right");
                    return GesturePartResult.Succeeded;
                }
            }

            // Hand dropped
            return GesturePartResult.Failed;
        }
    }

    class RHWaveSegment2 : IGestureSegment
    {
        public GesturePartResult Update(KinectSensor sensor, Skeleton skeleton)
        {
            var hand_point = skeleton.Joints[JointType.HandRight].Position;
            var elbow_point = skeleton.Joints[JointType.ElbowRight].Position;
            //
            double hand_x = hand_point.X;
            double hand_y = hand_point.Y;
            // 
            double elbow_x = elbow_point.X;
            double elbow_y = elbow_point.Y;

            // Debug.WriteLine("RHS SEG 1: HAND X: {0}, Y: {1}", hand_x, hand_y);
            // Debug.WriteLine("RHS SEG 1: ELB X: {0}, Y: {1}", elbow_x, elbow_y);

            // Hand above elbow
            if (hand_y > elbow_y)
            {
                // Debug.WriteLine("Above");
                // Hand left of elbow
                if (hand_x < elbow_x)
                {
                    // Debug.WriteLine("Left");
                    return GesturePartResult.Succeeded;
                }
            }

            // Hand dropped
            return GesturePartResult.Failed;
        }
    }

    class RHSWaveGesture : IGesture
    {
        // Amount of frames used for the entire gesture detection
        // Note that the kinect runs at 30fps
        readonly int WINDOW_SIZE = 50;

        IGestureSegment[] segments;

        int currentSegmentIdx;
        int frameCount;

        public event GestureRecognizedHandler OnRecognized;

        public RHSWaveGesture()
        {
            RHWaveSegment1 segOne = new RHWaveSegment1();
            RHWaveSegment2 segTwo = new RHWaveSegment2();

            segments = new IGestureSegment[]
            {
                // We want exactly ONE waves
                segOne, segTwo,
                // segOne, segTwo,
                // segOne, segTwo,
            };
        }

        public void Update(KinectSensor sensor, Skeleton skeleton, System.Windows.Point skel2DCenter)
        {
            GesturePartResult result = segments[currentSegmentIdx].Update(sensor, skeleton);
            if (result == GesturePartResult.Succeeded)
            {
                if (currentSegmentIdx + 1 < segments.Length)
                {
                    currentSegmentIdx++;
                    frameCount = 0;
                }
                else
                {
                    if (OnRecognized != null)
                    {
                        OnRecognized(this, new GestureEventArgs(skeleton.TrackingId, skel2DCenter));
                        Reset();
                    }
                }
            }
            else if (result == GesturePartResult.Failed || frameCount == WINDOW_SIZE)
            {
                Reset();
            }
            else
            {
                frameCount++;
            }
        }

        public void Reset()
        {
            currentSegmentIdx = 0;
            frameCount = 0;
        }
    }
}

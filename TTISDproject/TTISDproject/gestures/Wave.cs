using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using static TTISDproject.gestures.GestureEventArgs;

namespace TTISDproject.gestures
{
    class RHWaveSegment1 : IGestureSegment
    {
        public GesturePartResult Update(Skeleton skeleton)
        {
            // Hand above elbow
            if (skeleton.Joints[JointType.HandRight].Position.Y >
                skeleton.Joints[JointType.ElbowRight].Position.Y)
            {
                // Hand right of elbow
                if (skeleton.Joints[JointType.HandRight].Position.X >
                    skeleton.Joints[JointType.ElbowRight].Position.X)
                {
                    return GesturePartResult.Failed;
                }
            }

            // Hand dropped
            return GesturePartResult.Failed;
        }
    }

    class RHWaveSegment2 : IGestureSegment
    {
        public GesturePartResult Update(Skeleton skeleton)
        {
            // Hand above elbow
            if (skeleton.Joints[JointType.HandRight].Position.Y >
                skeleton.Joints[JointType.ElbowRight].Position.Y)
            {
                // Hand left of elbow
                if (skeleton.Joints[JointType.HandRight].Position.X <
                    skeleton.Joints[JointType.ElbowRight].Position.X)
                {
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
                // We want exactly three waves in WINDOW_SIZE samples!s
                segOne, segTwo,
                segOne, segTwo,
                segOne, segTwo,
            };
        }

        public void Update(Skeleton skeleton)
        {
            GesturePartResult result = segments[currentSegmentIdx].Update(skeleton);
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
                        OnRecognized(this, new GestureEventArgs(skeleton.TrackingId));
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

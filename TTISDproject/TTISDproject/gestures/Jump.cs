using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Emgu.CV;
using System.Drawing;
using System.Diagnostics;

namespace TTISDproject.gestures
{

    class JumpSegment1 : IJumpSegment
    {
        public GesturePartResult Update(KinectSensor sensor, PointF[] kalman_result)
        {
            float predicted_y = kalman_result[0].Y;
            float actual_y = kalman_result[1].Y;

            return GesturePartResult.Failed;
        }
    }

    class JumpSegment2 : IJumpSegment
    {
        public GesturePartResult Update(KinectSensor sensor, PointF[] kalman_result)
        {
            float predicted_y = kalman_result[0].Y;
            float actual_y = kalman_result[1].Y;

            return GesturePartResult.Failed;
        }
    }

    class Jump : IGesture
    {
        readonly int WINDOW_SIZE = 50;

        IJumpSegment[] segments;
        GesturePartResult[] results;

        int currentSegmentIdx;
        int frameCount;

        private KalmanFilter kal;
        private JumpData syntheticData;

        public event GestureEventArgs.GestureRecognizedHandler OnRecognized;

        public Jump()
        {
            var segOne = new JumpSegment1();
            var segTwo = new JumpSegment2();

            segments = new IJumpSegment[]
            {
                // Up movement
                segOne,
                // Down movement
                segTwo
            };

            results = new GesturePartResult[]
            {
                GesturePartResult.Failed,
                GesturePartResult.Failed,
            };

            SetupKalmanFilter();
        }

        private void SetupKalmanFilter()
        {
            kal = new KalmanFilter(4, 2, 0);
            syntheticData = new JumpData();
            // Initial state of the skeleton.
            // TODO; Find out if the initial position of (0,0) will invoke errors.
            Matrix<float> state = new Matrix<float>(new float[]
            {
                0.0f, 0.0f, 0.0f, 0.0f
            });
            // Transfer references into the kalman-filter
            state.Mat.CopyTo(kal.StatePost); // == correctedstate
            syntheticData.transition.Mat.CopyTo(kal.TransitionMatrix);
            syntheticData.measurementNoise.Mat.CopyTo(kal.MeasurementNoiseCov);
            syntheticData.processNoise.Mat.CopyTo(kal.ProcessNoiseCov);
            syntheticData.errorCovariancePost.Mat.CopyTo(kal.ErrorCovPost);
            syntheticData.measurement.Mat.CopyTo(kal.MeasurementMatrix);
        }

        public void Reset()
        {
            currentSegmentIdx = 0;
            frameCount = 0;
        }

        public void Update(KinectSensor sensor, Skeleton skeleton)
        {
            var bone_hip = skeleton.Joints[JointType.HipCenter];
            if(bone_hip.TrackingState == JointTrackingState.NotTracked)
            {
                Reset();
                return;
            }

            // Update filter
            float pos_hip_x = bone_hip.Position.X;
            float pos_hip_y = bone_hip.Position.Y;

            syntheticData.state[0, 0] = pos_hip_x;
            syntheticData.state[1, 0] = pos_hip_y;
            // Prediction from Kalman for the next timestep.
            float[] pred = new float[2];
            kal.Predict();
            kal.StatePre.CopyTo(pred);
            PointF predictedPoint = new PointF(pred[0], pred[1]);
            // Update kalman state with a noice induced measurement.
            Matrix<float> measurement = syntheticData.GetMeasurement();
            PointF measurePoint = new PointF(measurement[0, 0], measurement[1, 0]);
            kal.Correct(measurement.Mat);
            // Get an adjusted internal state measurement.
            float[] estimated = new float[2];
            kal.StatePost.CopyTo(estimated);
            PointF estimatedPoint = new PointF(estimated[0], estimated[1]);
            // 
            syntheticData.GoToNextState();

            Debug.WriteLine("KALMAN X: {0}, Y: {1}", estimated[0], estimated[1]);

            // Note: This is the data passed into the segment recognizer
            PointF[] kal_results = new PointF[2]
            {
                predictedPoint,
                estimatedPoint
            };

            for (int i = 0; i < segments.Length; ++i)
            {
                results[i] = segments[i].Update(sensor, kal_results);
            }

            GesturePartResult result = results[currentSegmentIdx];
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
    }
}

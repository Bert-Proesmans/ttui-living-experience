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
        public GesturePartResult Update(KinectSensor sensor, Skeleton skel, float[][] kalman_result)
        {
            float[] predicted = kalman_result[0];
            float[] actual = kalman_result[1];

            float predicted_speed_y = predicted[3];
            float actual_speed_y = actual[3];

            Debug.WriteLine("UP\tY speed: {0} --- {1}", predicted_speed_y, actual_speed_y);

            var left_foot_y = skel.Joints[JointType.AnkleLeft].Position.Y;
            var right_foot_y = skel.Joints[JointType.AnkleRight].Position.Y;

            // Debug.WriteLine("Feet Y: {0} --- {1}", left_foot_y, right_foot_y);

            // Both feet at same height
            if (Util.NearlyEqual(left_foot_y, right_foot_y, 0.05f))
            {
                // Movement upwards
                if (actual_speed_y > 0)
                {
                    if (predicted_speed_y < actual_speed_y)
                    {
                        Debug.WriteLine("UP");
                        return GesturePartResult.Succeeded;
                    }
                }
            }

            return GesturePartResult.Failed;
        }
    }

    class JumpSegment2 : IJumpSegment
    {
        public GesturePartResult Update(KinectSensor sensor, Skeleton skel, float[][] kalman_result)
        {
            float[] predicted = kalman_result[0];
            float[] actual = kalman_result[1];

            float predicted_speed_y = predicted[3];
            float actual_speed_y = actual[3];

            // Debug.WriteLine("DOWN\tY speed: {0} --- {1}", predicted_speed_y, actual_speed_y);

            var left_foot_y = skel.Joints[JointType.AnkleLeft].Position.Y;
            var right_foot_y = skel.Joints[JointType.AnkleRight].Position.Y;

            // Debug.WriteLine("Feet Y: {0} --- {1}", left_foot_y, right_foot_y);

            // Both feet at same height
            if (Util.NearlyEqual(left_foot_y, right_foot_y, 0.05f))
            {
                // Movement downwards
                if (actual_speed_y < 0)
                {
                    if (predicted_speed_y > actual_speed_y)
                    {
                        Debug.WriteLine("DOWN");
                        return GesturePartResult.Succeeded;
                    }
                }
            }

            return GesturePartResult.Failed;
        }
    }

    class JumpGesture : IGesture
    {
        readonly int WINDOW_SIZE = 50;

        IJumpSegment[] segments;

        int currentSegmentIdx;
        int frameCount;

        private KalmanFilter kal;
        private JumpData syntheticData;

        public event GestureEventArgs.GestureRecognizedHandler OnRecognized;

        public JumpGesture(Skeleton skel)
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

            SetupKalmanFilter(skel);
        }

        private void SetupKalmanFilter(Skeleton skel)
        {
            kal = new KalmanFilter(4, 2, 0);
            syntheticData = new JumpData();

            // Initial state of the skeleton, positioned at 0.
            Matrix<float> state = new Matrix<float>(new float[]
            {
                0.0f, 0.0f,
                0.0f, 0.0f
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
            var left_foot = skeleton.Joints[JointType.AnkleLeft];
            var right_foot = skeleton.Joints[JointType.AnkleRight];
            if (left_foot.TrackingState != JointTrackingState.Tracked ||
                right_foot.TrackingState != JointTrackingState.Tracked)
            {
                Reset();
                return;
            }

            // Update filter
            var left_foot_pos = left_foot.Position;
            var right_foot_pos = right_foot.Position;
            float pos_feet_x = (left_foot_pos.X + right_foot_pos.X) / 2.0f;
            float pos_feet_y = (left_foot_pos.Y + right_foot_pos.Y) / 2.0f;

            syntheticData.state[0, 0] = pos_feet_x;
            syntheticData.state[1, 0] = pos_feet_y;
            // Prediction from Kalman for the next timestep.
            float[] pred = new float[4];
            kal.Predict();
            kal.StatePre.CopyTo(pred);
            PointF predictedPositionPoint = new PointF(pred[0], pred[1]);
            // Update kalman state with a noice induced measurement.
            Matrix<float> measurement = syntheticData.GetMeasurement();
            PointF measurePoint = new PointF(measurement[0, 0], measurement[1, 0]);
            kal.Correct(measurement.Mat);
            // Get an adjusted internal state measurement.
            float[] estimated = new float[4];
            kal.StatePost.CopyTo(estimated);
            PointF estimatedPositionPoint = new PointF(estimated[0], estimated[1]);
            // 
            syntheticData.GoToNextState();

            // Note: This is the data passed into the segment recognizer
            float[][] kal_results = new float[2][]
            {
                pred,
                estimated
            };

            GesturePartResult result = segments[currentSegmentIdx].Update(sensor, skeleton, kal_results);
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

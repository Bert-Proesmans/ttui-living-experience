using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTISDproject.gestures
{
    class JumpData
    {
        public Matrix<float> state;
        public Matrix<float> transition;
        public Matrix<float> measurement;
        public Matrix<float> processNoise;
        public Matrix<float> measurementNoise;
        public Matrix<float> errorCovariancePost;

        public JumpData()
        {
            state = new Matrix<float>(new float[,]
            {
                {0f }, // x-position
                {0f }, // y-position
                {0f }, // x-velocity
                {0f }, // y-velocity
            });

            // Low noise tolerance -> quick changes in velocity 
            //transition = new Matrix<float>(new float[,]
            //{
            //    {1, 0, 1, 0 }, // Expected change on state for x-pos
            //    {0, 1, 0, 1 }, // ..
            //    {0, 0, 1, 0 }, // Expected change on state for x-velocity
            //    {0, 0, 0, 1 }, // ..
            //});

            // Higher noise tolerance -> slow changes in velocity
            transition = new Matrix<float>(new float[,]
            {
                {1, 0, 1, 0 }, // Expected change on state for x-pos
                {0, 1, 0, 1 }, // ..
                {0, 0, 1, 0 }, // Expected change on state for x-velocity
                {0, 0, 0, 1 }, // ..
            });

            measurement = new Matrix<float>(new float[,]
            {
                {1, 0, 0, 0 },
                {0, 1, 0, 0 }
            });
            measurement.SetIdentity();

            processNoise = new Matrix<float>(4, 4); // Fixed to transition size
            processNoise.SetIdentity(new MCvScalar(1.0e-4));

            measurementNoise = new Matrix<float>(2, 2); // Fixed to input data size
            measurementNoise.SetIdentity(new MCvScalar(1.0e-1));

            errorCovariancePost = new Matrix<float>(4, 4); // Fixed to transition size
            errorCovariancePost.SetIdentity();
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <returns></returns>
        public Matrix<float> GetMeasurement()
        {
            Matrix<float> measurementNoise = new Matrix<float>(2, 1);
            // Suppose noise is a Gaussian distribution around 0-mean.
            var noiseDevation = new MCvScalar(Math.Sqrt(measurementNoise[0, 0]));
            measurementNoise.SetRandNormal(new MCvScalar(), noiseDevation);
            return measurement * state + measurementNoise;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public void GoToNextState()
        {
            Matrix<float> processNoise = new Matrix<float>(4, 1);
            // Suppose noise is a Guassian distribution around 0-mean.
            var noiseDeviation = new MCvScalar(processNoise[0, 0]);
            processNoise.SetRandNormal(new MCvScalar(), noiseDeviation);
            state = transition * state + processNoise;
        }
    }
}

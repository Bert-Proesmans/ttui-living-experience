using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static TTISDproject.gestures.GestureEventArgs;

namespace TTISDproject.gestures
{
    interface IGesture
    {
        event GestureRecognizedHandler OnRecognized;
        void Update(KinectSensor sensor, Skeleton skeleton);
        void Reset();
    }
}

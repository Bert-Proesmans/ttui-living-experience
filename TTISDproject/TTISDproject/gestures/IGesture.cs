using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using static TTISDproject.gestures.GestureEventArgs;

namespace TTISDproject.gestures
{
    interface IGesture
    {
        event GestureRecognizedHandler OnRecognized;
        void Update(KinectSensor sensor, Skeleton skeleton, Point skel2DCenter);
        void Reset();
    }
}

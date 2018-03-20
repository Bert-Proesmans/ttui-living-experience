using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace TTISDproject.gestures
{
    interface IGestureSegment
    {
        GesturePartResult Update(KinectSensor sensor, Skeleton skeleton);
    }

    interface IJumpSegment
    {
        GesturePartResult Update(KinectSensor sensor, PointF[] kalman_result);
    }
}

using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTISDproject.gestures
{
    interface IGestureSegment
    {
        GesturePartResult Update(Skeleton skeleton);
    }
}

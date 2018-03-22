using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace TTISDproject.gestures
{
    class GestureEventArgs: EventArgs
    {
        public delegate void GestureRecognizedHandler(object sender, GestureEventArgs e);
        // TODO; Possibly add Gesture NOT recognized?

        public int TrackingID { get; private set; }

        public Point Skel2DCenter { get; private set; }

        public GestureEventArgs(int trackingID, Point skelCenter)
        {
            TrackingID = trackingID;
            Skel2DCenter = skelCenter;
        }
    }
}

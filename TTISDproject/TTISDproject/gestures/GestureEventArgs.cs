using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TTISDproject.gestures
{
    class GestureEventArgs: EventArgs
    {
        public delegate void GestureRecognizedHandler(object sender, GestureEventArgs e);
        // TODO; Possibly add Gesture NOT recognized?

        public int TrackingID { get; private set; }

        public GestureEventArgs(int trackingID)
        {
            TrackingID = trackingID;
        }
    }
}

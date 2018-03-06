using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TTISDproject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Status of the 3D to 2D calibration step
        /// </summary>
        private CalibrationStep calibrationStep;

        public CalibrationStep CalibrationStep
        {
            get
            {
                return calibrationStep;
            }

            set { 
                calibrationStep = value;
                RaisePropertyChanged(nameof(CalibrationStep));
            }
        }      

        /// <summary>
        /// Event handler collector.
        /// Register to this event to receive a notification when properties of this class changed.
        /// eg:
        ///     PropertyChanged += MainWindow_PropertyChanged;
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Helper method to notify property changed event listeners
        /// </summary>
        /// <param name="propertyName"></param>
        private void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }

                PropertyChanged += EnumPropertyChange;

         
                CalibrationStep = CalibrationStep.NotCalibrated;
                

            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        private void EnumPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CalibrationStep))
                switch (calibrationStep)
                {
                    case CalibrationStep.NotCalibrated:
                        string messageBoxText = "Please calibrate the application";
                        string caption = "calibration";
                        MessageBoxButton button = MessageBoxButton.OK;
                        MessageBoxImage icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        break;

                    case CalibrationStep.PointOne:
                        messageBoxText = "Step 1 completed, now calibrate step 2";
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        break;
                    case CalibrationStep.PointTwo:
                        messageBoxText = "Step 2 completed, now calibrate step 3";
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        break;
                    case CalibrationStep.PointThree:
                        messageBoxText = "Step 3 completed, now calibrate step 4";
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        break;
                    case CalibrationStep.PointFour:
                        messageBoxText = "Calibration completed, enjoy the app";
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        break;
                    case CalibrationStep.Calibrated:

                        break;
                    default:
                        break;
                }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Space)
            {
                switch(calibrationStep)
                {
                    case CalibrationStep.PointOne:
                        // Lock in point one
                        break;
                    case CalibrationStep.PointTwo:
                        // Lock in point two
                        break;
                    case CalibrationStep.PointThree:
                        // Lock in point three
                        break;
                    case CalibrationStep.PointFour:
                        // Lock in point four
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                /*
                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        // RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }
                */

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }
    }
}

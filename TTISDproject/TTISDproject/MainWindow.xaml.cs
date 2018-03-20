using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using TTISDproject.gestures;

namespace TTISDproject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        SkeletonView win2; //skeletonwindow

        private class TrackingData
        {
            public enum ThemeKeys
            {
                Animals,
                Presidents,
                Houses,
                Beaches,
            }

            public int TrackingId { get; private set; }
            public IGesture[] Gestures { get; private set; }
            public ThemeKeys Theme;

            public TrackingData(int trackingId, IGesture[] gestures)
            {
                TrackingId = trackingId;
                Gestures = gestures;
                // Default to first theme
                Theme = ThemeKeys.Animals;
            }
        }

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
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Brush used for drawing objects requiring attention
        /// </summary>
        private readonly Brush markerBrush = Brushes.Red;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// TopLeft corner of our 2D drawing scene
        /// </summary>
        private readonly Point Point2DStepOne = new Point(0, 0);

        /// <summary>
        /// TopRight corner of our 2D drawing scene
        /// </summary>
        private readonly Point Point2DStepTwo = new Point(RenderWidth, 0);

        /// <summary>
        /// BottomLeft corner of our 2D drawing scene
        /// </summary>
        private readonly Point Point2DStepThree = new Point(0, RenderHeight);

        /// <summary>
        /// BottomRight corner of our 2D drawing scene
        /// </summary>
        private readonly Point Point2DStepFour = new Point(RenderWidth, RenderHeight);

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
        /// Contains a mapping of all gesture identifiers per tracked skeleton
        /// </summary>
        private Dictionary<int, TrackingData> trackingMapper;

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

            set
            {
                calibrationStep = value;
                RaisePropertyChanged(nameof(CalibrationStep));
            }
        }

        /// <summary>
        /// Instance used to calibrate 3D to 2D mapping
        /// </summary>
        private CalibrationClass calibrationClass = null;

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

            trackingMapper = new Dictionary<int, TrackingData>();
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

            KeyUp += ButtonPressed;

            SensorSkeletonFrameReady(null, null);

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

            if (null != this.sensor)
            {
                // Setup calibrator to receive mappings between reality and screen image
                this.calibrationClass = new CalibrationClass(this.sensor);
                // Listen for key to advance the calibration process.
                KeyUp += Window_Calibrate;

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
                        /*
                        string caption = "calibration";
                        MessageBoxButton button = MessageBoxButton.OK;
                        MessageBoxImage icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointOne:
                        messageBoxText = "Calibrate Step 1";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointTwo:
                        messageBoxText = "Step 1 completed, now calibrate step 2";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointThree:
                        messageBoxText = "Step 2 completed, now calibrate step 3";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointFour:
                        messageBoxText = "Step 3 completed, now calibrate step 4";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;

                        // Add an event handler to be called whenever there is new color frame data
                        // this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                        break;
                    case CalibrationStep.Calibrated:
                        messageBoxText = "Calibration completed, enjoy the app";
                        this.statusBarText.Text = messageBoxText;
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
            /*
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }*/
        }

        private void Window_Calibrate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                Debug.WriteLine("space pressed");
                if (win2 != null)
                {
                    win2.Unsubscribe();
                }

                using (SkeletonFrame skeletonFrame = this.sensor.SkeletonStream.OpenNextFrame(200))
                {
                    Debug.WriteLine("testen op skeleton data");
                    if (skeletonFrame != null)
                    {

                        Skeleton[] skeletons = new Skeleton[0];
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);


                        foreach (Skeleton skel in skeletons)
                        {


                            if (skel.TrackingState == SkeletonTrackingState.Tracked)
                            {
                                Debug.WriteLine("1 tracked found");
                                SkeletonPoint point3D = skel.Position;

                                Debug.WriteLine(calibrationStep.ToString());

                                switch (calibrationStep)
                                {
                                    case CalibrationStep.NotCalibrated:
                                        // Start calibration
                                        CalibrationStep = CalibrationStep.PointOne;
                                        break;

                                    case CalibrationStep.PointOne:
                                        // Lock in point one
                                        calibrationClass.AddCalibrationPoint(Point2DStepOne, point3D);
                                        CalibrationStep = CalibrationStep.PointTwo;
                                        break;
                                    case CalibrationStep.PointTwo:
                                        // Lock in point two
                                        calibrationClass.AddCalibrationPoint(Point2DStepTwo, point3D);
                                        CalibrationStep = CalibrationStep.PointThree;
                                        break;
                                    case CalibrationStep.PointThree:
                                        // Lock in point three
                                        calibrationClass.AddCalibrationPoint(Point2DStepThree, point3D);
                                        CalibrationStep = CalibrationStep.PointFour;
                                        break;
                                    case CalibrationStep.PointFour:
                                        // Lock in point four.
                                        // This automatically forces calibration.
                                        calibrationClass.AddCalibrationPoint(Point2DStepFour, point3D);
                                        CalibrationStep = CalibrationStep.Calibrated;
                                        //now draw the position on the UI.
                                        //TODO add seperate subscribe/unsubscribe via listener.
                                        this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                                        break;
                                    default:
                                        string message = String.Format("Unexpected calibration step: {}", calibrationStep.ToString());
                                        Debug.WriteLine(message);
                                        break;
                                }


                            }
                            else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                            {
                                Debug.WriteLine("1 positiononly found");
                            }

                        }

                    }
                }

                Debug.WriteLine("space pressed");
                if (win2 != null)
                {
                    win2.Subscribe();
                }

            }
        }

        private TrackingData RetrieveTrackingData(int skeletonID)
        {
            TrackingData result;

            trackingMapper.TryGetValue(skeletonID, out result);
            if (result == null)
            {
                IGesture[] gestures = new IGesture[]
                {
                    // new RHSWaveGesture(),
                    // new JumpGesture(skel)
                };

                foreach (IGesture g in gestures)
                {
                    g.OnRecognized += OnGestureRecognized;
                }

                result = new TrackingData(skeletonID, null);

                trackingMapper[skeletonID] = result;
            }

            return result;
        }

        private void OnGestureRecognized(object sender, GestureEventArgs e)
        {
            string recognizer = sender.GetType().Name;
            int skel_id = e.TrackingID;
            Debug.WriteLine("Recognized gesture from {0} for skeleton id {1}", recognizer, skel_id);
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Debug.WriteLine("OnSensorSkeletonFrameReady");

            Skeleton[] skeletons = new Skeleton[0];

            if (e != null)
            {
                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                    }
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.White, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                // Draw playfield themes
                dc.DrawRectangle(Brushes.Green, null, new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0));
                dc.DrawRectangle(Brushes.Yellow, null, new Rect(RenderWidth / 2.0, 0, RenderWidth / 2.0, RenderHeight / 2.0));
                dc.DrawRectangle(Brushes.Tomato, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                // Draw edge markers
                double markerRadius = 20.0;
                dc.DrawEllipse(markerBrush, null, Point2DStepOne, markerRadius, markerRadius);
                dc.DrawEllipse(markerBrush, null, Point2DStepTwo, markerRadius, markerRadius);
                dc.DrawEllipse(markerBrush, null, Point2DStepThree, markerRadius, markerRadius);
                dc.DrawEllipse(markerBrush, null, Point2DStepFour, markerRadius, markerRadius);

                if (skeletons.Length != 0)
                {
                    var trackedSkeletons = skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked);
                    foreach (Skeleton skel in trackedSkeletons)
                    {
                        // Update gesture trackers with new frame information.
                        TrackingData data = RetrieveTrackingData(skel.TrackingId);
                        foreach(IGesture g in data.Gestures)
                        {
                            g.Update(this.sensor, skel);
                        }

                        Point skel2DCenter = this.calibrationClass.KinectToProjectionPoint(skel.Position);
                        // Debug.WriteLine("Skeleton position at ({0};{1})", skel2DCenter.X, skel2DCenter.Y);

                        // Render the position of each person onto our birds-eye view
                        dc.DrawEllipse(
                            centerPointBrush,
                            null,
                            skel2DCenter,
                            BodyCenterThickness,
                            BodyCenterThickness);
                    }
                }


                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        private void ButtonPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.J)
            {

                win2 = new SkeletonView();
                win2.Show();
                //this.Close();
            }
        }


    }
}

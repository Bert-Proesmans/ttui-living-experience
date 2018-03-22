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
using System.Globalization;
using System.Windows.Threading;

namespace TTISDproject
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        SkeletonView win2; //skeletonwindow

        public enum ThemeKeys
        {
            Main,
            Nature,
            History,
            Space,
            Art
        }

        private ThemeKeys Theme;
        private int SelectedPage;

        private class TrackingData
        {
            public int TrackingId { get; private set; }
            public IGesture[] Gestures { get; private set; }


            public TrackingData(int trackingId, IGesture[] gestures)
            {
                TrackingId = trackingId;
                Gestures = gestures;
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

            // Default to first theme
            Theme = ThemeKeys.Main;
            // Default to first page
            SelectedPage = 0;

            //Emgu.CV.Matrix<double> test = new Emgu.CV.Matrix<double>(5,5,1);
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

            KeyUp += Window_OpenDebug;

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

                PropertyChanged += CalibrationChanged;
                CalibrationStep = CalibrationStep.NotCalibrated;
            }

            if (null != this.sensor)
            {
                // Setup calibrator to receive mappings between reality and screen image
                this.calibrationClass = new CalibrationClass(this.sensor);
                // Listen for key to advance the calibration process.
                KeyUp += Window_Calibrate;
                KeyUp += Window_AutoCalibrate;

            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        private void CalibrationChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CalibrationStep))
                switch (calibrationStep)
                {
                    case CalibrationStep.NotCalibrated:
                        string messageBoxText = "Please calibrate the application";
                        this.statusBarText.Text = messageBoxText;
                        Debug.WriteLine("|||||||||||||||||||||||||||||||||");
                        Debug.WriteLine("PLEASE CALIBRATE !");
                        break;
                    case CalibrationStep.PointOne:
                        messageBoxText = "Calibrate Step 1";
                        this.statusBarText.Text = messageBoxText;
                        Debug.WriteLine("|||||||||||||||||||||||||||||||||");
                        Debug.WriteLine("CALIBRATE STARTED !");
                        break;
                    case CalibrationStep.PointTwo:
                        messageBoxText = "Step 1 completed, now calibrate step 2";
                        this.statusBarText.Text = messageBoxText;
                        Debug.WriteLine("|||||||||||||||||||||||||||||||||");
                        Debug.WriteLine("STEP ONE COMPLETE !");
                        break;
                    case CalibrationStep.PointThree:
                        messageBoxText = "Step 2 completed, now calibrate step 3";
                        this.statusBarText.Text = messageBoxText;
                        Debug.WriteLine("|||||||||||||||||||||||||||||||||");
                        Debug.WriteLine("STEP TWO COMPLETE !");
                        break;
                    case CalibrationStep.PointFour:
                        messageBoxText = "Step 3 completed, now calibrate step 4";
                        this.statusBarText.Text = messageBoxText;
                        Debug.WriteLine("|||||||||||||||||||||||||||||||||");
                        Debug.WriteLine("STEP THREE COMPLETE !");

                        // Add an event handler to be called whenever there is new color frame data
                        // this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
                        break;
                    case CalibrationStep.Calibrated:
                        messageBoxText = "Calibration completed, enjoy the app";
                        this.statusBarText.Text = messageBoxText;

                        Debug.WriteLine("----------------------------------");
                        Debug.WriteLine("STEP FOUR COMPLETE\t\tCALIBRATED !");
                        Debug.WriteLine("----------------------------------");
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

        private void Window_AutoCalibrate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                if (CalibrationStep == CalibrationStep.NotCalibrated)
                {
                    var dispatcherTimer = new DispatcherTimer();
                    dispatcherTimer.Tick += (s2, e2) =>
                    {
                        switch (CalibrationStep)
                        {
                            case CalibrationStep.NotCalibrated:
                            case CalibrationStep.PointOne:
                            case CalibrationStep.PointTwo:
                            case CalibrationStep.PointThree:
                            case CalibrationStep.PointFour:
                                var key = Key.Space;
                                var target = this;
                                var routedEvent = Keyboard.KeyUpEvent;
                                var eventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
                                {
                                    RoutedEvent = routedEvent
                                };
                                // Invoke calibrate
                                Window_Calibrate(this, eventArgs);
                                break;

                            case CalibrationStep.Calibrated:
                            default:
                                dispatcherTimer.Stop();
                                break;
                        }
                    };
                    dispatcherTimer.Interval = new TimeSpan(0, 0, 8);
                    dispatcherTimer.Start();
                }
            }
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

                Skeleton[] skeletons = null;
                using (SkeletonFrame skeletonFrame = this.sensor.SkeletonStream.OpenNextFrame(200))
                {
                    if (skeletonFrame != null)
                    {
                        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                    }
                }

                var trackedSkeleton = skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked).SingleOrDefault();
                if (trackedSkeleton != null)
                {
                    Debug.WriteLine("1 tracked found");
                    SkeletonPoint point3D = trackedSkeleton.Position;

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
                } else
                {
                    Debug.WriteLine("EXACTLY ONE person must be tracked to calibrate!");
                }

                if (win2 != null)
                {
                    win2.Subscribe();
                }

            }
        }

        private TrackingData RetrieveTrackingData(int skeletonID, Skeleton skel)
        {
            TrackingData result;

            trackingMapper.TryGetValue(skeletonID, out result);
            if (result == null)
            {
                IGesture[] gestures = new IGesture[]
                {
                    new RHSWaveGesture(),
                    new JumpGesture(skel)
                };

                gestures[0].OnRecognized += OnWaveRecognized;

                /* Give the kalman filter some time to converge before firing events */
                var kalman_wait_time = 1; // in seconds
                var dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Tick += (sender, e) =>
                {
                    Debug.WriteLine("Delayed subscription on JUMP");
                    gestures[1].OnRecognized += OnJumpRecognized;
                    dispatcherTimer.Stop();
                };
                dispatcherTimer.Interval = new TimeSpan(0, 0, kalman_wait_time);
                dispatcherTimer.Start();

                result = new TrackingData(skeletonID, gestures);

                trackingMapper[skeletonID] = result;
            }

            return result;
        }

        private void OnWaveRecognized(object sender, GestureEventArgs e)
        {
            string recognizer = sender.GetType().Name;
            int skel_id = e.TrackingID;
            Debug.WriteLine("Recognized gesture from {0} for skeleton id {1}", recognizer, skel_id);
            this.SelectedPage = this.SelectedPage + 1 % 2;
        }

        private void OnJumpRecognized(object sender, GestureEventArgs e)
        {
            string recognizer = sender.GetType().Name;
            int skel_id = e.TrackingID;
            Debug.WriteLine("Recognized gesture from {0} for skeleton id {1}", recognizer, skel_id);

            Point center = e.Skel2DCenter;
            // Left of center
            if (center.X < RenderWidth/2.0)
            {
                // Left top
                if (center.Y < RenderHeight/2.0)
                {
                    Theme = ThemeKeys.Space;
                }
                // Left bottom
                else
                {
                    Theme = ThemeKeys.Nature;
                }
            }
            // Right of center
            else
            {
                // Right top
                if(center.Y < RenderHeight/2.0)
                {
                    Theme = ThemeKeys.History;
                }
                // Right bottom
                else
                {
                    Theme = ThemeKeys.Art;
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
            // Debug.WriteLine("OnSensorSkeletonFrameReady");

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

                //// Draw playfield themes
                //dc.DrawRectangle(Brushes.Green, null, new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0));
                //dc.DrawRectangle(Brushes.Yellow, null, new Rect(RenderWidth / 2.0, 0, RenderWidth / 2.0, RenderHeight / 2.0));
                //dc.DrawRectangle(Brushes.Tomato, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                //dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                ////if theme1 selected
                switch (this.Theme)
                {
                    case ThemeKeys.Main:

                        System.Windows.Media.ImageSource imagetheme1 = new BitmapImage(new Uri("images/maincosmos.png", UriKind.Relative));
                        Rect rectTheme1 = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                        dc.DrawImage(imagetheme1, rectTheme1);
                        System.Windows.Media.ImageSource imagetheme2 = new BitmapImage(new Uri("images/mainhistory.png", UriKind.Relative));
                        Rect rectTheme2 = new Rect(RenderWidth / 2.0, 0, RenderWidth / 2.0, RenderHeight / 2.0);
                        dc.DrawImage(imagetheme2, rectTheme2);
                        System.Windows.Media.ImageSource imagetheme3 = new BitmapImage(new Uri("images/mainnature.png", UriKind.Relative));
                        Rect rectTheme3 = new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0);
                        dc.DrawImage(imagetheme3, rectTheme3);
                        System.Windows.Media.ImageSource imagetheme4 = new BitmapImage(new Uri("images/mainart.png", UriKind.Relative));
                        Rect rectTheme4 = new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0);
                        dc.DrawImage(imagetheme4, rectTheme4);
                        break;
                    case ThemeKeys.Space:
                        //showcosmospictures
                        if (this.SelectedPage == 1)
                        { //showpage2
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/blackhole.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "A black hole is a region of spacetime exhibiting such strong gravitational effects that nothing—not even particles and electromagnetic radiation such as light—can escape from inside it.The theory of general relativity predicts that a sufficiently compact mass can deform spacetime to form a black hole. The boundary of the region from which no escape is possible is called the event horizon. Although the event horizon has an enormous effect on the fate and circumstances of an object crossing it, no locally detectable features appear to be observed.In many ways a black hole acts like an ideal black body, as it reflects no light.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);


                        }
                        else
                        {//showpage1
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/Nebula.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "A nebula (Latin for cloud or fog) is an interstellar cloud of dust, hydrogen, helium and other ionized gases.Originally, nebula was a name for any diffuse astronomical object, including galaxies beyond the Milky Way.The Andromeda Galaxy, for instance, was once referred to as the Andromeda Nebula(and spiral galaxies in general as spiral nebulae) before the true nature of galaxies was confirmed in the early 20th century by Vesto Slipher, Edwin Hubble and others.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);
                        }

                        break;
                    case ThemeKeys.History:
                        //show history pictures
                        if (this.SelectedPage == 1)
                        { //showpage2
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/atillathefun.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "Attila (circa 406–453), frequently called Attila the Hun, was the ruler of the Huns from 434 until his death in March 453. He was also the leader of a tribal empire consisting of Huns, Ostrogoths, and Alans among others, on the territory of Central and Eastern Europe. During his reign, he was one of the most feared enemies of the Western and Eastern Roman Empires.He crossed the Danube twice and plundered the Balkans, but was unable to take Constantinople. His unsuccessful campaign in Persia was followed in 441 by an invasion of the Eastern Roman(Byzantine) Empire, the success of which emboldened Attila to invade the West.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);


                        }
                        else
                        {//showpage1
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/williamtheconquerer.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "William I (1028 – 9 September 1087), usually known as William the Conqueror and sometimes William the Bastard, was the first Norman King of England, reigning from 1066 until his death in 1087. A descendant of Rollo, he was Duke of Normandy, from 1035 onward. After a long struggle to establish his power, by 1060 his hold on Normandy was secure, and he launched the Norman conquest of England six years later. The rest of his life was marked by struggles to consolidate his hold over England and his continental lands and by difficulties with his eldest son.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);
                        }
                        break;
                    case ThemeKeys.Nature:
                        //show nature pictures
                        if (this.SelectedPage == 1)
                        { //showpage2
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/passionflower.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "Passiflora, known also as the passion flowers or passion vines, is a genus of about 550 species of flowering plants, the type genus of the family Passifloraceae.They are mostly tendril-bearing vines, with some being shrubs or trees. They can be woody or herbaceous. Passion flowers produce regular and usually showy flowers with a distinctive corona. The medical utility of only a few species of Passiflora has been scientifically studied. In initial study in 2001 for treatment of generalized anxiety disorder, maypop extract performed as well as oxazepam but with fewer short-term side effects.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);


                        }
                        else
                        {//showpage1
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/mountainlorel.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "Kalmia latifolia, commonly called mountain laurel, calico-bush or spoonwood, is a broadleaved evergreen shrub in the heather family, Ericaceae, that is native to the eastern United States. Its range stretches from southern Maine south to northern Florida, and west to Indiana and Louisiana. Mountain laurel is the state flower of Connecticut and Pennsylvania. It is the namesake of Laurel County in Kentucky and the city of Laurel, Mississippi (founded 1882). The plant was originally brought to Europe as an ornamental plant during the 18th century. It is still widely grown for its attractive flowers and year round evergreen leaves. All parts of this plant are toxic if ingested.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);
                        }
                        break;
                    case ThemeKeys.Art:
                        //show nature pictures
                        if (this.SelectedPage == 1)
                        { //showpage2
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/picasso.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "Pablo Picasso (25 October 1881 – 8 April 1973) was a Spanish painter, sculptor, printmaker, ceramicist, stage designer, poet and playwright who spent most of his adult life in France. Regarded as one of the most influential artists of the 20th century, he is known for co-founding the Cubist movement, the invention of constructed sculpture, the co-invention of collage, and for the wide variety of styles that he helped develop and explore. Among his most famous works are the proto-Cubist Les Demoiselles d'Avignon (1907), and Guernica (1937), a dramatic portrayal of the bombing of Guernica by the German and Italian airforces.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);


                        }
                        else
                        {//showpage1
                            System.Windows.Media.ImageSource imageblackhole = new BitmapImage(new Uri("images/rubens.png", UriKind.Relative));
                            Rect rectBlackhole = new Rect(0.0, 0.0, RenderWidth / 2.0, RenderHeight / 2.0);
                            dc.DrawImage(imageblackhole, rectBlackhole);


                            Point point = new Point(RenderWidth / 2.0, 10);
                            string teststring = "Sir Peter Paul Rubens (28 June 1577 – 30 May 1640) was a Flemish artist. He is considered the most influential artist of Flemish Baroque tradition. Rubens' highly charged compositions reference erudite aspects of classical and Christian history. His unique and immensely popular Baroque style emphasized movement, color, and sensuality, which followed the immediate, dramatic artistic style promoted in the Counter-Reformation. Rubens specialized in making altarpieces, portraits, landscapes, and history paintings of mythological and allegorical subjects.";
                            System.Windows.Media.FormattedText text = new FormattedText(teststring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 12, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(text, point);


                            dc.DrawRectangle(Brushes.LightGreen, null, new Rect(0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));
                            dc.DrawRectangle(Brushes.LightCyan, null, new Rect(RenderWidth / 2.0, RenderHeight / 2.0, RenderWidth / 2.0, RenderHeight / 2.0));

                            Point pointPrev = new Point(0 + 100, (RenderHeight / 2.0) + 150);
                            string prevstring = "previous";
                            System.Windows.Media.FormattedText prevtext = new FormattedText(prevstring, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(prevtext, pointPrev);

                            Point pointNext = new Point((RenderWidth / 2.0) + 100, RenderHeight / 2.0 + 150);
                            string prevnext = "next";
                            System.Windows.Media.FormattedText nexttext = new FormattedText(prevnext, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Verdana"), 36, Brushes.Black);
                            text.MaxTextWidth = 300;
                            text.MaxTextHeight = 1000;
                            dc.DrawText(nexttext, pointNext);
                        }
                        break;
                }

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
                        Point skel2DCenter = this.calibrationClass.KinectToProjectionPoint(skel.Position);
                        // Debug.WriteLine("Skeleton position at ({0};{1})", skel2DCenter.X, skel2DCenter.Y);

                        // Update gesture trackers with new frame information.
                        TrackingData data = RetrieveTrackingData(skel.TrackingId, skel);
                        foreach (IGesture g in data.Gestures)
                        {
                            g.Update(this.sensor, skel, skel2DCenter);
                        }

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

        private void Window_OpenDebug(object sender, KeyEventArgs e)
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

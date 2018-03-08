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
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

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
        private readonly Point Point2DStepTwo = new Point(200, 0);

        /// <summary>
        /// BottomLeft corner of our 2D drawing scene
        /// </summary>
        private readonly Point Point2DStepThree = new Point(0, 200);

        /// <summary>
        /// BottomRight corner of our 2D drawing scene
        /// </summary>
        private readonly Point Point2DStepFour = new Point(200, 200);

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
                        messageBoxText = "Step 1 completed, now calibrate step 2";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointTwo:
                        messageBoxText = "Step 2 completed, now calibrate step 3";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointThree:
                        messageBoxText = "Step 3 completed, now calibrate step 4";
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = messageBoxText;
                        break;
                    case CalibrationStep.PointFour:
                        messageBoxText = "Calibration completed, enjoy the app";
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
                using (SkeletonFrame skeletonFrame = this.sensor.SkeletonStream.OpenNextFrame(200))
                {
                    if (skeletonFrame != null)
                    {

                        if (skeletonFrame.SkeletonArrayLength < 1)
                        {
                            // TODO; Notify no skeleton found
                            return;
                        }
                        else if (skeletonFrame.SkeletonArrayLength > 1)
                        {
                            // TODO; Notify multiple people in frame
                            return;
                        }

                        // Copy skeleton data into managed buffer, this is how it
                        // should be done..
                        Skeleton[] skelCalibrator = new Skeleton[1];
                        skeletonFrame.CopySkeletonDataTo(skelCalibrator);
                        // Get position of single detected skeleton
                        SkeletonPoint point3D = skelCalibrator[0].Position;

                        switch (calibrationStep)
                        {
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
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }



        private void ButtonPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.J)
            {

                SkeletonView win2 = new SkeletonView();
                win2.Show();
                //this.Close();
            }
        }


    }
}

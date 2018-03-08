﻿using Microsoft.Kinect;
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
                        /*
                        caption = "calibration";
                        button = MessageBoxButton.OK;
                        icon = MessageBoxImage.Exclamation;
                        MessageBox.Show(messageBoxText, caption, button, icon);
                        */
                        this.statusBarText.Text = "Calibration completed, enjoy the app";


                        // Add an event handler to be called whenever there is new color frame data
                        this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;
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

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        // RenderClippedEdges(skel, dc);

                        Point skel2DCenter = this.calibrationClass.KinectToProjectionPoint(skel.Position);
                        dc.DrawEllipse(
                            centerPointBrush, 
                            null, 
                            skel2DCenter, 
                            BodyCenterThickness, 
                            BodyCenterThickness);

                        /*
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
                        */
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }
        




        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }
        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }



    }
}

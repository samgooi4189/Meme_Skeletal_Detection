// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
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
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using System.Windows.Forms;

namespace SkeletalTracking
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        bool closing = false;
        const int skeletonCount = 6; 
        Skeleton[] allSkeletons = new Skeleton[skeletonCount];

        private float head_position, rightH_position, leftH_position;
        private bool init_flag = false;
        private bool pausetime = false;


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor old = (KinectSensor)e.OldValue;

            StopKinect(old);

            KinectSensor sensor = (KinectSensor)e.NewValue;

            if (sensor == null)
            {
                return;
            }

            


            var parameters = new TransformSmoothParameters
            {
                Smoothing = 0.3f,
                Correction = 0.0f,
                Prediction = 0.0f,
                JitterRadius = 1.0f,
                MaxDeviationRadius = 0.5f
            };
            sensor.SkeletonStream.Enable(parameters);

            sensor.SkeletonStream.Enable();

            sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(sensor_AllFramesReady);
            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30); 
            sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

            try
            {
                sensor.Start();
            }
            catch (System.IO.IOException)
            {
                kinectSensorChooser1.AppConflictOccurred();
            }
        }

        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (closing)
            {
                return;
            }

            //Get a skeleton
            Skeleton first =  GetFirstSkeleton(true, e);

            //initialize the initial points for head and hands
            if (!init_flag && first != null)
            {
                head_position = first.Joints[JointType.Head].Position.Y;
                rightH_position = first.Joints[JointType.HandRight].Position.Y;
                leftH_position = first.Joints[JointType.HandLeft].Position.Y;
                init_flag = true;
            }

            if (first == null)
            {
                headImage.Visibility = Visibility.Collapsed;
                headImage2.Visibility = Visibility.Collapsed;
                init_flag = false;
                return; 
            }

            //activate control when hand is above head
            if (first.Joints[JointType.HandRight].Position.Y > head_position) {
                if (first.Joints[JointType.HandRight].Position.X > first.Joints[JointType.Head].Position.X && !pausetime)
                {
                    SendKeys.SendWait("{RIGHT}");
                    pausetime = true;
                }
                else if(!pausetime)
                {
                    SendKeys.SendWait("{LEFT}");
                    pausetime = true;
                }
            }

            if (first.Joints[JointType.HandRight].Position.Y < head_position) {
                pausetime = false;
            }

            headImage.Visibility = Visibility.Visible;

            //set scaled position
            //ScalePosition(headImage, first.Joints[JointType.Head]);
            ScalePosition(leftEllipse, first.Joints[JointType.HandLeft]);
            ScalePosition(rightEllipse, first.Joints[JointType.HandRight]);
            GetCameraPoint(first,true, e);

            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                try{
                    (from s in allSkeletons
                     where s.TrackingState == SkeletonTrackingState.Tracked
                     select s).ElementAt(1);
                }catch(ArgumentOutOfRangeException ex)
                    {
                        headImage2.Visibility = Visibility.Collapsed;
                        return;
                    }

                    Skeleton second = GetFirstSkeleton(false, e);
                    headImage2.Visibility = Visibility.Visible;
                    GetCameraPoint(second, false, e);
                
            }

        }

        void GetCameraPoint(Skeleton first, bool flag,  AllFramesReadyEventArgs e)
        {

            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (depth == null ||
                    kinectSensorChooser1.Kinect == null)
                {
                    return;
                }
                

                //Map a joint location to a point on the depth map
                //head
                DepthImagePoint headDepthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.Head].Position);
                //left hand
                DepthImagePoint leftDepthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.HandLeft].Position);
                //right hand
                DepthImagePoint rightDepthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.HandRight].Position);


                //Map a depth point to a point on the color image
                //head
                ColorImagePoint headColorPoint =
                    depth.MapToColorImagePoint(headDepthPoint.X, headDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);
                //left hand
                ColorImagePoint leftColorPoint =
                    depth.MapToColorImagePoint(leftDepthPoint.X, leftDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30);
                //right hand
                ColorImagePoint rightColorPoint =
                    depth.MapToColorImagePoint(rightDepthPoint.X, rightDepthPoint.Y,
                    ColorImageFormat.RgbResolution640x480Fps30 );


                //Set location
                if(flag)
                    CameraPosition(headImage, headColorPoint);
                else
                {
                    CameraPosition(headImage2, headColorPoint);
                }
                CameraPosition(leftEllipse, leftColorPoint);
                CameraPosition(rightEllipse, rightColorPoint);
            }        
        }


        Skeleton GetFirstSkeleton(bool flag, AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return null; 
                }

                
                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                //get the first tracked skeleton
                
                
                    Skeleton first = (from s in allSkeletons
                                      where s.TrackingState == SkeletonTrackingState.Tracked
                                      select s).FirstOrDefault();
                
                if(!flag){
                    first = (from s in allSkeletons
                                      where s.TrackingState == SkeletonTrackingState.Tracked
                                      select s).ElementAt(1);
                }

                return first;

            }
        }

        private void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if (sensor.IsRunning)
                {
                    //stop sensor 
                    sensor.Stop();

                    //stop audio if not null
                    if (sensor.AudioSource != null)
                    {
                        sensor.AudioSource.Stop();
                    }


                }
            }
        }

        private void CameraPosition(FrameworkElement element, ColorImagePoint point)
        {
            //Divide by 2 for width and height so point is right in the middle 
            // instead of in top/left corner
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 2);
            

        }
        
         

        private void ScalePosition(FrameworkElement element, Joint joint)
        {
            //convert the value to X/Y
            //Joint scaledJoint = joint.ScaleTo(1280, 720); 
            
            //convert & scale (.3 = means 1/3 of joint distance)
            Joint scaledJoint = joint.ScaleTo(1280, 720, .3f, .3f);

            Canvas.SetLeft(element, scaledJoint.Position.X);
            Canvas.SetTop(element, scaledJoint.Position.Y); 
            
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //reset init_flag to false;
            init_flag = false;
            closing = true; 
            StopKinect(kinectSensorChooser1.Kinect); 
        }



    }
}

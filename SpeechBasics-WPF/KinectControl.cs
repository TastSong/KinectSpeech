using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace Microsoft.Samples.Kinect.SpeechBasics
{
    class KinectControl
    {
        /// <summary>
        ///
        /// </summary>
        KinectSensor sensor;
        /// <summary>
        /// 
        /// </summary>
        BodyFrameReader bodyFrameReader;
        /// <summary>
        ///
        /// </summary>
        private Body[] bodies = null;
        /// <summary>
        ///
        /// </summary>
        int screenWidth, screenHeight;

        /// <summary>
        ///
        /// </summary>
        DispatcherTimer timer = new DispatcherTimer();

        /// <summary>
        ///
        /// </summary>
        public float mouseSensitivity = MOUSE_SENSITIVITY;

        /// <summary>
        /// 
        /// </summary>
        public float timeRequired = TIME_REQUIRED;
        /// <summary>
        /// 
        /// </summary>
        public float pauseThresold = PAUSE_THRESOLD;
        /// <summary>
        ///
        /// </summary>
        public bool doClick = DO_CLICK;
        /// <summary>
        /// 
        /// </summary>
        public bool useGripGesture = USE_GRIP_GESTURE;
        /// <summary>
        /// 
        /// </summary>
        public float cursorSmoothing = CURSOR_SMOOTHING;

        // 
        public const float MOUSE_SENSITIVITY = 1.8f;
        public const float TIME_REQUIRED = 2f;
        public const float PAUSE_THRESOLD = 60f;
        public const bool DO_CLICK = true;
        public const bool USE_GRIP_GESTURE = true;
        public const float CURSOR_SMOOTHING = 0.9f;

        /// <summary>

        /// </summary>
        bool alreadyTrackedPos = false;

        /// <summary>
        ///
        /// </summary>
        float timeCount = 0;
        /// <summary>
        ///
        /// </summary>
        Point lastCurPos = new Point(0, 0);

        /// <summary>
        /// 
        /// </summary>
        bool wasLeftGrip = false;
        /// <summary>
        /// 
        /// </summary>
        bool wasRightGrip = false;

        public KinectControl()
        {
 
            sensor = KinectSensor.GetDefault();

            bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

 
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;


            timer.Interval = new TimeSpan(0, 0, 0, 0, 100); 
　　　　    timer.Tick += new EventHandler(Timer_Tick);
　　　　    timer.Start();

            sensor.Open();
        }


        
        /// <summary>
        ///
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Timer_Tick(object sender, EventArgs e)
        {
            if (!doClick || useGripGesture) return;

            if (!alreadyTrackedPos) {
                timeCount = 0;
                return;
            }
            
          //  Point curPos = MouseControl.GetCursorPosition();
      
          //  if ((lastCurPos - curPos).Length < pauseThresold)
          //  {
          //      if ((timeCount += 0.1f) > timeRequired)
          //      {

          //          MouseControl.DoMouseClick();
          //          timeCount = 0;
          //      }
          //  }
          //  else
          //  {
          //      timeCount = 0;
          //  }

          //  lastCurPos = curPos;
        }

        /// <summary>
        ///阅读的身体框架
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }
             

                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (!dataReceived) 
            {
                alreadyTrackedPos = false;
                return;
            }

            foreach (Body body in this.bodies)
            {

                if (body.IsTracked)
                {

                    CameraSpacePoint handLeft = body.Joints[JointType.HandLeft].Position;
                    CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                    CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;
                    CameraSpacePoint head = body.Joints[JointType.Head].Position;                  


                    if (true)//(handRight.Z - spineBase.Z < -0.15f) 
                    {             

                        float x = handRight.X - spineBase.X + 0.05f;                                              
                        float y = spineBase.Y - handRight.Y + 0.51f;
                      //  Point curPos = MouseControl.GetCursorPosition();                      
                        float smoothing = 1 - cursorSmoothing;

                 
                        alreadyTrackedPos = true;

                        if (doClick && useGripGesture)
                        {
                     
                            bool isRightArmStretching = ((handRight.Y - head.Y) > 0.04);
                            if (body.HandRightState == HandState.Closed && isRightArmStretching)
                            {
                                KeyboardControl.pressRight();
                                System.Threading.Thread.Sleep(800);
                            }
                            bool isLeftArmStretching = ((handLeft.Y - head.Y) > 0.04);
                            if (body.HandLeftState == HandState.Closed && isLeftArmStretching)
                            {
                                KeyboardControl.pressLeft();
                                System.Threading.Thread.Sleep(800);
                            }
                            if(isRightArmStretching && isLeftArmStretching && body.HandLeftState == HandState.Closed && body.HandRightState == HandState.Closed)
                            {
                                KeyboardControl.quitGame();
                                System.Threading.Thread.Sleep(800);
                            }
                        
                        }
                    }
                    
                    else
                    {
                       // wasLeftGrip = true;
                       // wasRightGrip = true;
                       // alreadyTrackedPos = false;
                    }


                    break;
                }
            }
        }

        public void Close()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }

    }
}

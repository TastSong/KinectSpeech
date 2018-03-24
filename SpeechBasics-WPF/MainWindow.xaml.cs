//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SpeechBasics
{
    using System;
    using System.Collections.Generic;    
    using System.ComponentModel;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;    
    using System.Windows;    
    using System.Windows.Documents;
    using System.Windows.Media;
    using Microsoft.Kinect;    
    using Microsoft.Speech.AudioFormat;
    using Microsoft.Speech.Recognition;

    /// <summary>
    /// 交互逻辑主窗口
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "In a full-fledged application, the SpeechRecognitionEngine object should be properly disposed. For the sake of simplicity, we're omitting that code in this sample.")]
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 中灰画笔的资源键。
        /// </summary>
        private const string MediumGreyBrushKey = "MediumGreyBrush";

        /// <summary>
        ///将每个方向和方向之间的映射立即映射到右边.
        /// </summary>
        private static readonly Dictionary<Direction, Direction> TurnRight = new Dictionary<Direction, Direction>
            {
                //{ Direction.Up, Direction.Right },
                //{ Direction.Right, Direction.Down },
                { Direction.Down, Direction.Exit },
                { Direction.Exit, Direction.Up }
            };

        /// <summary>
        /// 在每一个方向和它左边的方向之间映射。
        /// </summary>
        private static readonly Dictionary<Direction, Direction> TurnLeft = new Dictionary<Direction, Direction>
            {
                { Direction.Up, Direction.Exit },
                //{ Direction.Right, Direction.Up },
                //{ Direction.Down, Direction.Right },
                { Direction.Exit, Direction.Down }
            };

        /// <summary>
        /// 在每个方向和它所代表的位移单位之间进行映射。
        /// </summary>
        private static readonly Dictionary<Direction, Point> Displacements = new Dictionary<Direction, Point>
            {
                { Direction.Up, new Point { X = 0, Y = -1 } },
             //   { Direction.Right, new Point { X = 1, Y = 0 } },
                { Direction.Down, new Point { X = 0, Y = 1 } },
                { Direction.Exit, new Point { X = -1, Y = 0 } }
            };

        /// <summary>
        /// 活跃的Kinect传感器。
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// 流32 b-16b转换。
        /// </summary>
        private KinectAudioStream convertStream = null;

        /// <summary>
        /// 语音识别引擎使用Kinect的音频数据。
        /// </summary>
        private SpeechRecognitionEngine speechEngine = null;

        /// <summary>
        /// 海龟面对的方向。
        /// </summary>
        private Direction curDirection = Direction.Up;

        /// <summary>
        /// 用于选择已识别文本的所有UI span元素的列表。
        /// </summary>
        private List<Span> recognitionSpans;

        /// <summary>
        ///初始化主窗口类的新实例。
        /// </summary>
        /// 
        KinectControl kinectCtrl = new KinectControl();
        public MainWindow()
        {
            this.InitializeComponent();
        }

        /// <summary>
        ///列举乌龟可能面对的方向。
        /// </summary>
        private enum Direction
        {
            /// <summary>
            /// 代表上升
            /// </summary>
            Up,

            /// <summary>
            /// 代表下降
            /// </summary>
            Down,

            /// <summary>
            /// 表示要离开
            /// </summary>
            Exit

            ///// <summary>
            ///// 代表正确
            ///// </summary>
            //Right
        }

        /// <summary>
        /// 获取用于语音识别器（声学模型）的元数据，最适合于
        /// 从Kinect设备处理音频。
        /// </summary>
        /// <returns>
        /// 如果发现了识别信息，则可以用“未编码”来表示。
        /// </returns>
        private static RecognizerInfo TryGetKinectRecognizer()
        {
            IEnumerable<RecognizerInfo> recognizers;

            // 这需要在未安装预期识别器时捕获该情况。
            // 默认情况下，总是期望x86语音运行时。
            try
            {
                recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            }
            catch (COMException)
            {
                return null;
            }

            foreach (RecognizerInfo recognizer in recognizers)
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        /// <summary>
        /// 执行初始化任务。
        /// </summary>
        /// <param name="sender">对象发送事件</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            //只支持一个传感器
            this.kinectSensor = KinectSensor.GetDefault();

            if (this.kinectSensor != null)
            {
                // 打开传感器
                this.kinectSensor.Open();

                // 获取音频流
                IReadOnlyList<AudioBeam> audioBeamList = this.kinectSensor.AudioSource.AudioBeams;
                System.IO.Stream audioStream = audioBeamList[0].OpenInputStream();

                // 创建转换流
                this.convertStream = new KinectAudioStream(audioStream);
            }
            else
            {
                // 在失败时，设置状态文本
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
                return;
            }

            RecognizerInfo ri = TryGetKinectRecognizer();

            if (null != ri)
            {
                this.recognitionSpans = new List<Span> { forwardSpan, backSpan, exitSpan };

                this.speechEngine = new SpeechRecognitionEngine(ri.Id);

                /****************************************************************
                * 
                * 使用此代码以编程方式创建语法，而不是从
                * 一个语法文件。
                * 
                * var directions = new Choices();
                * directions.Add(new SemanticResultValue("forward", "FORWARD"));
                * directions.Add(new SemanticResultValue("forwards", "FORWARD"));
                * directions.Add(new SemanticResultValue("straight", "FORWARD"));
                * directions.Add(new SemanticResultValue("backward", "BACKWARD"));
                * directions.Add(new SemanticResultValue("backwards", "BACKWARD"));
                * directions.Add(new SemanticResultValue("back", "BACKWARD"));
                * directions.Add(new SemanticResultValue("turn left", "LEFT"));
                * directions.Add(new SemanticResultValue("turn right", "RIGHT"));
                *
                * var gb = new GrammarBuilder { Culture = ri.Culture };
                * gb.Append(directions);
                *
                * var g = new Grammar(gb);
                * 
                ****************************************************************/

                // 从语法定义XML文件创建语法。
                using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Properties.Resources.SpeechGrammar)))
                {
                    var g = new Grammar(memoryStream);
                    this.speechEngine.LoadGrammar(g);
                }

                this.speechEngine.SpeechRecognized += this.SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected += this.SpeechRejected;

                // 让convertStream知道语音是活跃的
                this.convertStream.SpeechActive = true;

                // 对于长时间的识别会议（几个小时或更长时间），关闭声学模型可能是有益的。
                // 这将防止识别的准确性随着时间的推移而降低。
                ////speechEngine.UpdateRecognizerSetting("AdaptationOn", 0);

                this.speechEngine.SetInputToAudioStream(
                    this.convertStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                this.speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                this.statusBarText.Text = Properties.Resources.NoSpeechRecognizer;
            }
        }

        /// <summary>
        /// un-initialization执行任务。
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (null != this.convertStream)
            {
                this.convertStream.SpeechActive = false;
            }

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= this.SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= this.SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
            }

            if (null != this.kinectSensor)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// 从识别指令中去除任何高亮度。
        /// </summary>
        private void ClearRecognitionHighlights()
        {
            foreach (Span span in this.recognitionSpans)
            {
                span.Foreground = (Brush)this.Resources[MediumGreyBrushKey];
                span.FontWeight = FontWeights.Normal;
            }
        }

        /// <summary>
        /// 用于识别语音事件的处理程序。
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // 言语表达的信心，下面是我们对言论的态度，就好像它没有被听到一样
            const double ConfidenceThreshold = 0.3;

            // 直角的度数.
            const int DegreesInRightAngle = 90;

            // 像素的数量每次都应该向前或向后移动.
            const int DisplacementAmount = 60;

            this.ClearRecognitionHighlights();

            if (e.Result.Confidence >= ConfidenceThreshold)
            {
                switch (e.Result.Semantics.Value.ToString())
                {
                    case "FORWARD":
                        forwardSpan.Foreground = Brushes.DeepSkyBlue;
                        forwardSpan.FontWeight = FontWeights.Bold;
                        //turtleTranslation.X = (playArea.Width + turtleTranslation.X + (DisplacementAmount * Displacements[this.curDirection].X)) % playArea.Width;
                        //turtleTranslation.Y = (playArea.Height + turtleTranslation.Y + (DisplacementAmount * Displacements[this.curDirection].Y)) % playArea.Height;
                        KeyboardControl.pressRight();
                        System.Threading.Thread.Sleep(800);
                        break;

                    case "BACKWARD":
                        backSpan.Foreground = Brushes.DeepSkyBlue;
                        backSpan.FontWeight = FontWeights.Bold;
                        //turtleTranslation.X = (playArea.Width + turtleTranslation.X - (DisplacementAmount * Displacements[this.curDirection].X)) % playArea.Width;
                        //turtleTranslation.Y = (playArea.Height + turtleTranslation.Y - (DisplacementAmount * Displacements[this.curDirection].Y)) % playArea.Height;
                        KeyboardControl.pressLeft();
                        System.Threading.Thread.Sleep(800);
                        break;

                    case "EXIT":
                        exitSpan.Foreground = Brushes.DeepSkyBlue;
                        exitSpan.FontWeight = FontWeights.Bold;
                        //this.curDirection = TurnLeft[this.curDirection];

                        //// 我们用左转弯表示对显示的海龟的反时针方向旋转.
                        //turtleRotation.Angle -= DegreesInRightAngle;
                        KeyboardControl.quitGame();
                        System.Threading.Thread.Sleep(800);
                        break;

                    //case "RIGHT":
                    //    rightSpan.Foreground = Brushes.DeepSkyBlue;
                    //    rightSpan.FontWeight = FontWeights.Bold;
                    //    this.curDirection = TurnRight[this.curDirection];

                    //    // 我们向右转是指对显示的海龟的顺时针方向旋转.
                    //    turtleRotation.Angle += DegreesInRightAngle;
                    //    break;
                }
            }
        }

        /// <summary>
        /// 拒绝语音事件的处理程序.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            this.ClearRecognitionHighlights();
        }
    }
}
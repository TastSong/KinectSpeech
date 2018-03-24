//------------------------------------------------------------------------------
// <copyright file="KinectAudioStream.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SpeechBasics
{
    using System;
    using System.IO;
 
    /// <summary>
    /// 包装器流类支持32-16bit转换和支持语音调用
    /// </summary>
    internal class KinectAudioStream : Stream
    {
        /// <summary>
        /// 以32位的IEEE浮点格式保存kinect音频流
        /// </summary>
        private Stream kinect32BitStream;

        /// <summary>
        /// 初始化一个新实例 <see cref="KinectAudioStream" /> class.
        /// </summary>
        /// <param name="input">Kinect audio stream</param>
        public KinectAudioStream(Stream input)
        {
            this.kinect32BitStream = input;
        }

        /// <summary>
        ///获取或设置一个值，指示语音识别是否活动
        /// </summary>
        public bool SpeechActive { get; set; }

        /// <summary>
        /// 可以读取属性 
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// 可以写属性 
        /// </summary>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// 可以寻求属性
        /// </summary>
        public override bool CanSeek
        {
            // 语言不调用，但正确设置值
            get { return false; }
        }

        /// <summary>
        /// Position Property产权地位
        /// </summary>
        public override long Position
        {
            // 演讲得到这个职位
            get { return 0; }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// 流的长度。没有实现。
        /// </summary>
        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// 冲流。没有实现.
        /// </summary>
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 流寻求。没有实现，总是返回0。
        /// </summary>
        /// <param name="offset">相对于源参数的字节偏移</param>
        /// <param name="origin">一种类型的SeekOrigin的值，表示用于获得新位置的引用点</param>
        /// <returns>Always returns 0</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            // 即使能找==假，说话还是叫找。返回0，使语音更愉快，而不是NotImplementedException（）
            return 0;
        }

        /// <summary>
        /// 设置流的长度。没有实现。
        /// </summary>
        /// <param name="value">流长度</param>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 写进流。没有实现。
        /// </summary>
        /// <param name="buffer">缓冲区写</param>
        /// <param name="offset">抵消到缓冲</param>
        /// <param name="count">Number of bytes to write</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 从流中读取并将32位的IEEE浮点数转换为16位签名整数
        /// </summary>
        /// <param name="buffer">输入缓冲器</param>
        /// <param name="offset">抵消到缓冲区</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>bytes read</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Kinect提供32位的浮动样本。语音要求16位整型的样本。
            const int SampleSizeRatio = sizeof(float) / sizeof(short); // = 2. 

            // 语音读取频率很高，允许读（在msec）之间的等待时间。
            const int SleepDuration = 50;

            // 为从Kinect接收32位的浮动分配缓冲区
            int readcount = count * SampleSizeRatio;
            byte[] kinectBuffer = new byte[readcount];

            int bytesremaining = readcount;

            // 语音期望返回所有被请求的字节
            while (bytesremaining > 0)
            {
                // 如果我们不再处理语音命令，退出
                if (!this.SpeechActive)
                {
                    return 0;
                }

                int result = this.kinect32BitStream.Read(kinectBuffer, readcount - bytesremaining, bytesremaining);
                bytesremaining -= result;

                // 语音阅读速度快于实时——等待更多数据的到来。
                if (bytesremaining > 0)
                {
                    System.Threading.Thread.Sleep(SleepDuration);
                }
            }

            // 将每个浮动音频样本转换为短格式
            for (int i = 0; i < count / sizeof(short); i++)
            {
                // 从字节数组中提取一个32位的IEEE值
                float sample = BitConverter.ToSingle(kinectBuffer, i * sizeof(float));

                // Make sure it is in the range [-1, +1]
                if (sample > 1.0f)
                {
                    sample = 1.0f;
                }
                else if (sample < -1.0f)
                {
                    sample = -1.0f;
                }

                // 比例浮动到范围（短。MinValue,短。MaxValue)然后
                // 转换为16位与适当的舍入
                short convertedSample = Convert.ToInt16(sample * short.MaxValue);

                // 在输出字节数组中放置产生的16位样例
                byte[] local = BitConverter.GetBytes(convertedSample);
                System.Buffer.BlockCopy(local, 0, buffer, offset + (i * sizeof(short)), sizeof(short));
            }

            return count;
        }
    }
}

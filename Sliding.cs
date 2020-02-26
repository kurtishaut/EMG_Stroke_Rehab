using System;
using System.Collections;
using System.Collections.Generic;
using LSL;

public class Sliding
{
    //high pass filter for removing DC offset
    public class filter
    {
        //object used for lock
        private static readonly object locker = new object();

        //filter coefficient for 50,59,61 and 500Hz respectfully
        private static readonly float[] a = new float[] { (float)0.8642, (float)0.1564, (float)0.8392, (float)0.611 };


        //variables used for RealTime filtering and 
        //stores previous valuves
        static private int i, channels;
        static private float[] e60, x, e;
        static private float[] oldData, olde60, oldx, olde;

        //constructor to initialize variables
        static filter()
        {
            channels = 2;
            reset();
        }

        static public void reset()
        {
            oldData = new float[channels];
            olde = new float[channels];
            olde60 = new float[channels];
            oldx = new float[channels];
            e = new float[channels];
            e60 = new float[channels];
            x = new float[channels];
        }

        static public float[] process(float[] data)
        {
            lock (locker)
            {
                for (i = 0; i < channels; i++)
                {
                    //Filtering 60Hz grounds noise
                    //e60[i] = a[2] * (olde60[i] + (data[i] - oldData[i])) + (a[1] * data[i] + olde60[i] * (a[1] - 1));

                    float d = data[(i == 0 ? Settings.flexSensor : Settings.extSensor) - 1];

                    //50 to 500 Hz Filter 
                    x[i] = a[0] * (oldx[i] + (d - oldData[i])); //High pass
                    e[i] = a[3] * x[i] + olde[i] * (1 - a[3]); //Low pass
                    oldData[i] = d;
                }
                //saves data
                e.CopyTo(olde, 0);
                x.CopyTo(oldx, 0);
                //e60.CopyTo(olde60, 0);
            }
            return e;
        }
    }

    public class Smooth
    {
        private static readonly object locker = new object();
        private static liblsl.StreamOutlet m_LSLOutlet;

        public static float[] output;

        //Sliding Window Method
        private static Queue<float[]> outputQueue;
        private static float[] weights;

        static Smooth()
        {
            liblsl.StreamInfo info = new liblsl.StreamInfo("EVA System", "Smooth EMG", output.Length, 2000 /*EMG_Socket.frequency*/, liblsl.channel_format_t.cf_float32);
            m_LSLOutlet = new liblsl.StreamOutlet(info);
        }

        static public void reset()
        {
            outputQueue = new Queue<float[]>();
            weights = new float[(int)(1.0f /*CalibrationSettings.windowRMS*/ * 1000)];

            // CalibrationSettings.windowRMS defaults to 1.0f 

            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = i * 2.0f / weights.Length; //linear weights (oldest -> 0x, newest - 2x)
            }
        }

        //Sliding Window Method + weights
        static public void sliding_rms(Object info)
        {
            float[] data = filter.process((float[])info);
            lock (locker)
            {
                //get the absolute value of the data
                data = new float[2] { Math.Abs(data[0]), Math.Abs(data[1]) };

                //add newest data to the top of the queue
                outputQueue.Enqueue(data);
                //remove oldest data from bottom of queue when full
                if (outputQueue.Count > weights.Length)
                    data = outputQueue.Dequeue();

                output[0] = 0;
                output[1] = 0;
                int i = 0;
                foreach (float[] q in outputQueue)
                {
                    output[0] += weights[i] * q[0];
                    output[1] += weights[i] * q[1];
                    i++;
                }
            }

            m_LSLOutlet.push_sample(output);
        }
    }
}

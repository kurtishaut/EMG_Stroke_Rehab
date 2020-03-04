using System;
using LSL;
using System.Threading;
using EVA_Library.Processing;
using EVA_Library.EMG_Collection;
using System.Collections.Generic;

namespace EVA_Library
{
    namespace EMG_Validation
    {
        public class Validator
        {
            private static readonly object locker = new object();

            private static liblsl.StreamOutlet m_LSLOutlet;
            public static bool side;
            static public int count
            {
                get
                {
                    return Smooth.count;
                }
                private set { }
            }
            //temp variables for calibration
            public static float[] flex_Max, ext_Max, flex_MVIC, ext_MVIC, max, rest_Avg;
            public static int invalid;  //specific invalid data (filter has yet to converge)

            //variables used to verify coactivation
            static public float flex_norm, ext_norm;
            //data streamed to LSL
            static public float[] data;
            //

            static public AutoResetEvent valid;
            
            static Validator()
            {
                ext_Max = new float[2];
                flex_Max = new float[2];
                rest_Avg = new float[2];
                flex_MVIC = new float[2];
                ext_MVIC = new float[2];
                max = new float[2];
                data = new float[8];
                valid = new AutoResetEvent(false);
                liblsl.StreamInfo info =new liblsl.StreamInfo("EVA System", "Data", 8, 2000, liblsl.channel_format_t.cf_float32);
                m_LSLOutlet = new liblsl.StreamOutlet(info);
            }

            //function use to calculate percentage of muscle activity.
            public static void evaluate(Object info)
            {
                lock (locker)
                {
                    Smooth.sliding_rms(info); //process EMG Data
                    flex_norm = Math.Abs((Smooth.output[0] - rest_Avg[0]) / (flex_Max[0] - rest_Avg[0]));
                    ext_norm = Math.Abs((Smooth.output[1] - rest_Avg[1]) / (ext_Max[1] - rest_Avg[1]));

                    data[0] = flex_norm; data[1] = ext_norm;

                    if (0.5 * flex_Max[0] <= Smooth.output[0]) { }
                    //valid.Set();
                    else if (0.5 * ext_Max[1] <= Smooth.output[1]) { }
                        //valid.Set();
                }

                m_LSLOutlet.push_sample(data);
            }

            //reset variables involved with processing
            public static void reset()
            {
                filter.reset();
                Smooth.reset();
            }

            //temporary calibration which tracks maximum amplitude
            public static void AmpCal(Object info)
            {
                lock (locker)
                {
                    Smooth.sliding_rms(info); // should be called by add EMG_Socket.add instead
                    if (invalid != 0)
                    {
                        --invalid;
                        return;
                    }
                    if (max[0] < Smooth.output[0]) max[0] = Smooth.output[0];
                    if (max[1] < Smooth.output[1]) max[1] = Smooth.output[1];
                }
            }
        }
    }


    namespace Processing
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
                        e[i] = a[3] * x[i] + olde[i] * (1-a[3]); //Low pass
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

            //variables for moving rms algorithm
            private static float[] mrms;
            public static float[] output;
            public static int count;

            //Exponential Weighting Method
            private static float weight;
            private static readonly float forgetfactor;
            private static bool mrms_go;

            //Sliding Window Method
            private static Queue<float[]> outputQueue;
            private static float[] weights;

            static Smooth()
            {
                mrms = new float[2];
                output = new float[2];
                count = 0;
                mrms_go = true;
                weight = 1;
                forgetfactor = (float)0.9975;
                liblsl.StreamInfo info = new liblsl.StreamInfo("EVA System", "Smooth EMG", output.Length, EMG_Socket.frequency, liblsl.channel_format_t.cf_float32);
                m_LSLOutlet = new liblsl.StreamOutlet(info);
            }

            static public void reset()
            {
                mrms = new float[2] { 0, 0 };
                mrms_go = true;
                weight = 1;
                count = 0;

                outputQueue = new Queue<float[]>();
                weights = new float[(int)(CalibrationSettings.windowRMS * 1000)];
                for(int i =0; i< weights.Length; i++)
                {
                    weights[i] = i * 2.0f / weights.Length; //linear weights (oldest -> 0x, newest - 2x)
                }
            }

            //(NOT IN USE) Exponential Weighting Method
            static public void moving_rms(Object info)
            {
                float[] data = filter.process((float[])info);
                lock (locker)
                {
                    if (mrms_go)
                    {
                        mrms[0] = (float)Math.Sqrt((double)(data[0] * data[0]));
                        mrms[1] = (float)Math.Sqrt((double)(data[1] * data[1]));
                        mrms_go = false;
                    }
                    else
                    {
                        mrms[0] = (1 - 1 / weight) * mrms[0] + Math.Abs(data[0]) / weight;
                        mrms[1] = (1 - 1 / weight) * mrms[1] + Math.Abs(data[1]) / weight;

                    }
                    weight = weight * forgetfactor + 1;
                    mrms.CopyTo(output, 0);
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
}

using System;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using EVA_Library.EMG_Validation;
using EVA_Library.Processing;

using LSL;

namespace EVA_Library
{
    namespace EMG_Collection
    {
        public class EMG_Socket
        {
            private static readonly object locker = new object();
            private static float[] AccumNoise = new float[2];

            //booleans for validating connection
            public static bool connected, running;


            //The following are used for TCP/IP connections
            private static TcpClient commandSocket;
            private static TcpClient emgSocket;
            private const int commandPort = 50040;  //server command port
            private const int emgPort = 50041;  //port for EMG data
            private const string server = "localhost";
            
            //Time span for calobrating EMG
            public const float time = 5;
            public static float[] collectionWindow = new float[2] { 1, 3 };

            //information for EMG Stream
            public const int channels = 6, frequency = 2000;
        
            //The following are storage for acquired data
            private float[] emgData = new float[16];
            private BinaryReader reader;
            

            //used for debuging
            public Int32 count
            {
                protected set;
                get;
            }

            //The following are streams and readers/writers for communication
            private NetworkStream commandStream;
            private NetworkStream emgStream;
            private StreamReader commandReader;
            private StreamWriter commandWriter;

            //Server commands
            private const string COMMAND_QUIT = "QUIT";
            private const string COMMAND_START = "START";
            private const string COMMAND_STOP = "STOP";

            //Variable for using LSL
            private liblsl.StreamOutlet m_LSLOutlet;

            //Connects to Delsys System
            public int open()
            {
                try
                {
                    //Establish TCP/IP connection to server using URL entered
                    commandSocket = new TcpClient(server, commandPort);
                    //Set up communication streams
                    commandStream = commandSocket.GetStream();
                    commandReader = new StreamReader(commandStream, Encoding.ASCII);
                    commandWriter = new StreamWriter(commandStream, Encoding.ASCII);
                    commandReader.ReadLine();
                    commandReader.ReadLine();   //get extra line terminator
                    SetupOutlet();
                    connected = true;   //indicate that we are connected
                }
                catch (Exception connectException)
                {
                    Console.WriteLine(connectException.ToString());
                    return 0;
                }
                return 1;
            }

            public void close()
            {
                //send QUIT command
                SendCommand(COMMAND_QUIT);
                connected = false;  //no longer connected  

                //Close all streams and connections
                commandReader.Close();
                commandWriter.Close();
                commandStream.Close();
                commandSocket.Close();
                emgStream.Close();
                emgSocket.Close();
            }

            //Send a command to the server and get the response
            private string SendCommand(string command)
            {
                string response = "";

                //Check if connected
                if (connected)
                {
                    //Send the command
                    commandWriter.WriteLine(command);
                    commandWriter.WriteLine();  //terminate command
                    commandWriter.Flush();  //make sure command is sent immediately

                    //Read the response line and display    
                    response = commandReader.ReadLine();
                    commandReader.ReadLine();   //get extra line terminator
                }
                else
                    throw new Exception("Not Connected to Delsys Socket.");
                return response; //returns response from delsys
            }
            
            //Stops data Stream from Delsys
            public int stop()
            {
                reader.Close(); //close the reader. This also disconnects
                try
                {
                    if (SendCommand(COMMAND_STOP).StartsWith("OK"))
                    {
                        running = false;
                        return 1;
                    }
                }
                catch (Exception e) { }
                return 0;
            }


            //Start Main EMG Aquisition
            public int start()
            {
                if (connected)
                {
                    //reset processed data 
                    Validator.reset();
                    count = 0;
                    try
                    {

                        //Establish data connections with data stream
                        emgSocket = new TcpClient(server, emgPort);
                        emgData = new float[16];
                        emgStream = emgSocket.GetStream();
                        emgStream.ReadTimeout = 100;    //set timeout
                        reader = new BinaryReader(emgStream); //Create a binary reader to read the data

                        if (SendCommand(COMMAND_START).StartsWith("OK"))
                        {
                            running = true;
                            return 1;
                        }
                    }
                    catch (Exception e) { }
                }
                else
                    throw new Exception("Trying to Start Data Stream Before Opening Socket");

                return 0;
            }

            //Config LSL Outlet for Recording EMG data
            private void SetupOutlet()
            {
                liblsl.StreamInfo info = new liblsl.StreamInfo("EVA System", "EMG", channels, frequency, liblsl.channel_format_t.cf_float32);
                m_LSLOutlet = new liblsl.StreamOutlet(info);
            }

            
            public void emgWorker()
            {
                float[] data = new float[2];
                while (running)
                {
                    try
                    {
                        //Demultiplex the data and save for UI display
                        for (int sn = 0; sn < 16; ++sn)
                        {
                            emgData[sn] = reader.ReadSingle();
                        }

                        m_LSLOutlet.push_sample(emgData); //sends emg data to LSL Lab Recorder

                        ThreadPool.QueueUserWorkItem(Validator.evaluate, emgData);

                        /*
                         * This would be the optimal placement for controller 
                         * 
                         * Eg.
                         * ThreadPool.QueueUserWorkItem
                        */

                        ++count;
                    }
                    catch
                    {
                        //ignore timeouts, but force a check of the running flag
                    }

                }
            }


            //funtion used to gather EMG data to calibrate
            public void calibrate(object state)
            {
                int samples, max;
                int numSamples = (int)(collectionWindow[1] - collectionWindow[0] * 2000);
                for (int i = 0; i < 2; i++)
                {
                    samples = 0;
                    AccumNoise = new float[2];
                    Validator.invalid = 000; //changed from 500
                    max = (int)(time * 2000)+Validator.invalid;

                    while(samples <= max)
                    {
                        try
                        {
                            //Demultiplex the data and save for UI display
                            for (int sn = 0; sn < 16; ++sn)
                            {
                               emgData[sn] = reader.ReadSingle();
                            }

                            m_LSLOutlet.push_sample(emgData); //sends emg data to LSL Lab Recorder

                            ThreadPool.QueueUserWorkItem(Validator.AmpCal, emgData);
                            //creates thread for EMG Calibration using individual EMG Data
                            if (samples>(collectionWindow[0] *2000) && samples<(collectionWindow[1] * 2000))
                            {
                                ThreadPool.QueueUserWorkItem(add, emgData);
                            }                            
                            ++samples;
                        }
                        catch
                        {
                            //ignore timeouts, but force a check of the running flag
                        }
                    }
                    Validator.valid.Set();
                    switch (i)
                    {
                        case 0:
                            Validator.data[2] = Validator.max[0];
                            Validator.max.CopyTo(Validator.flex_Max, 0);
                            Validator.flex_MVIC[0] = AccumNoise[0] / numSamples;
                            Validator.flex_MVIC[1] = AccumNoise[1] / numSamples;
                            Validator.data[6] = Validator.flex_MVIC[0];
                            break;
                        case 1:
                            Validator.data[3] = Validator.max[1];
                            Validator.max.CopyTo(Validator.ext_Max,0);
                            Validator.ext_MVIC[0] = AccumNoise[0] / numSamples;
                            Validator.ext_MVIC[1] = AccumNoise[1] / numSamples;
                            Validator.data[7] = Validator.ext_MVIC[1];
                            break;
                    }
                    if(i<1)Validator.valid.WaitOne();
                }
            }

            static private void add(Object info)
            {
                lock (locker)
                {
                    Smooth.sliding_rms(info);
                    if(Validator.invalid != 0)
                    {
                        Validator.invalid--;
                        return;
                    }
                    AccumNoise[0] += Smooth.output[0];
                    AccumNoise[1] += Smooth.output[1];
                }
            }

            //Find average muscle activity at rest
            public void AvgNoise(object state)
            {
                AccumNoise = new float[2];
                Validator.invalid = 000;
                int samples = (int)(time * 2000)+Validator.invalid;
                while (samples!=0)
                {
                    try
                    {
                        //Demultiplex the data and save for UI display
                        for (int sn = 0; sn < 16; ++sn)
                        {
                            emgData[sn] = reader.ReadSingle();
                        }

                        m_LSLOutlet.push_sample(emgData); //sends emg data to LSL Lab Recorder

                        ThreadPool.QueueUserWorkItem(add, emgData);
                        --samples;
                    }
                    catch
                    {
                        //ignore timeouts, but force a check of the running flag
                    }

                }
                Validator.valid.Set();
                Validator.rest_Avg[0] = AccumNoise[0] / ((int)(time * 2000));
                Validator.rest_Avg[1] = AccumNoise[1] / ((int)(time * 2000));
                Validator.data[4] = Validator.rest_Avg[0];
                Validator.data[5] = Validator.rest_Avg[1];
            }
        }
    }
}

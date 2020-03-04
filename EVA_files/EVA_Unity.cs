#define DEV

using System;
using System.Threading;
using System.Timers;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using EVA_Library.EMG_Collection;
using EVA_Library.EMG_Validation;
using EVA_Profile;

public abstract class EVA_Unity : MonoBehaviour
{
    /// <summary>
    /// Base class for connecting EVA behaviors to Unity 
    /// </summary>

    protected AutoResetEvent check;
    protected ManualResetEvent done;

    protected static string note;

    static readonly protected object locker = new object();

    //UI Refs
    [Header("EVA UI")]

    [SerializeField] private Text instruct;
    [SerializeField] private Button calibrateButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button skipButton;

    [SerializeField] private Slider LeftBar;
    [SerializeField] private Slider RightBar;

    [SerializeField] private GameObject MessageBox;
    [SerializeField] private Text messageText;

    [SerializeField] private CalibratePanel calibratePanel;
    [SerializeField] protected CommentPanel commentPanel;

    //Threshold sliders (independent from right / left)
    private ThresholdSlider FlexThresholdSlider; //slider for FLEX
    private ThresholdSlider ExtThresholdSlider; //slider for EXT

    //Countdown timer
    private const int countdownTime = 3;

    //USE for EMG data
    private EMG_Socket EMG;

    //Threads for acquiring emg and smooth data
    private Thread emgThread;

    //Thread for running game
    private Thread calibrationThread;
    private Thread runningThread;
    private Thread gameThread;

    private bool skipping = false;
    private static Queue<Action> mainQueue;

    public float Flex { get; private set; }
    public float Ext { get; private set; }

    private string FlexText = "Left";
    private string ExtText = "Right";

    public bool GameRunning { get { return runningThread != null ? runningThread.IsAlive : false; } }

    private System.Timers.Timer countdownTimer;

    protected virtual void Awake()
    {
        InitializeComponent();
        EMG = new EMG_Socket();

        mainQueue = new Queue<Action>();

        //display user's name when signed in
        if (ProfileIO.CurrentProfile != null)
        {
            note = $"Welcome {ProfileIO.CurrentProfile.username}";
        }
    }

    //Setup the UI 
    private void InitializeComponent()
    {
        calibrateButton.onClick.AddListener(calibrateButton_Click);
        calibrateButton.gameObject.SetActive(false);
        calibrateButton.interactable = true;

        connectButton.onClick.AddListener(connectButton_Click);
        connectButton.gameObject.SetActive(true);
        connectButton.interactable = true;

        quitButton.onClick.AddListener(quitButton_Click);
        quitButton.gameObject.SetActive(true);
        quitButton.interactable = true;

        startButton.onClick.AddListener(startButton_Click);
        startButton.gameObject.SetActive(true);
        startButton.interactable = false;

        stopButton.onClick.AddListener(stopButton_Click);
        stopButton.gameObject.SetActive(false);
        stopButton.interactable = false;

        skipButton.onClick.AddListener(skipButton_Click);
        skipButton.gameObject.SetActive(false);
        skipButton.interactable = true;

        commentPanel.AddListener(() => {
            done.Set(); //release the threads
            commentPanel.gameObject.SetActive(false);
        });

        if (ProfileIO.CurrentProfile != null && ProfileIO.CurrentProfile.recordingArm == Arm.Left)
        {
            FlexThresholdSlider = RightBar.GetComponent<ThresholdSlider>();
            ExtThresholdSlider = LeftBar.GetComponent<ThresholdSlider>();

            FlexText = "Right";
            ExtText = "Left";
        }
        else
        {
            FlexThresholdSlider = LeftBar.GetComponent<ThresholdSlider>();
            ExtThresholdSlider = RightBar.GetComponent<ThresholdSlider>();

            FlexText = "Left";
            ExtText = "Right";
        }

        countdownTimer = new System.Timers.Timer(1000);
        countdownTimer.Elapsed += countdownTimerTick;
        countdownTimer.Enabled = false;

        check = new AutoResetEvent(false);
        done = new ManualResetEvent(false);
    }

    //Connect button handler
    protected virtual void connectButton_Click()
    {
#if UNITY_EDITOR || DEV
        if (Dev.enabled)
        {
            connectButton.gameObject.SetActive(false);
            calibrateButton.gameObject.SetActive(true);
            return;
        }
#endif

        if (EMG.open() == 1)
        {
            connectButton.gameObject.SetActive(false);
            calibrateButton.gameObject.SetActive(true);

            new Validator();
            new EVA_Library.Processing.Smooth();
        }
    }

    //Calibrate button handler
    protected virtual void calibrateButton_Click()
    {
        calibrateButton.interactable = false;
        quitButton.interactable = false;

        check = new AutoResetEvent(false);
        done = new ManualResetEvent(false);

        Validator.reset();

        //Start the calibration process
        calibrationThread = new Thread(calibrate);
        calibrationThread.Start();
    }

    //Quit button handler
    protected virtual void quitButton_Click()
    {
        //Check if running and display error message if not
        if (EMG_Socket.running)
        {
            messageText.text = "Can't quit while acquiring data!";
            MessageBox.gameObject.SetActive(true);
            return;
        }

        //abort Threads
        calibrationThread?.Abort();
        runningThread?.Abort();

        try
        {
            EMG.close();
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
        }

        calibrateButton.interactable = true; //enable connect button
        quitButton.interactable = false; //disable quit button
        startButton.interactable = false; //disable start button

        if (!EMG_Socket.running)
        {
            SceneManager.LoadScene(0, LoadSceneMode.Single); //load the main scene
        }
    }

    //Start button handler
    protected virtual void startButton_Click()
    {
        startButton.gameObject.SetActive(false);
        stopButton.gameObject.SetActive(true);
        stopButton.interactable = true;
        quitButton.interactable = false;

        done.Reset();

        runningThread = new Thread(start);
        runningThread.Start();
    }

    //Stop button handler
    protected virtual void stopButton_Click()
    {
        lock (locker)
        {
            note = "Processing...";
        }

        commentPanel.gameObject.SetActive(true);

#if UNITY_EDITOR || DEV
        if (Dev.enabled)
        {
            lock (locker)
            {
                note = "Rest Time";
            }
            startButton.gameObject.SetActive(true);
            stopButton.gameObject.SetActive(false);
            return;
        }
#endif

        //Send stop command to server
        if (EMG.stop() == 1)
        {
            lock (locker)
            {
                note = "Rest Time";
            }
            startButton.gameObject.SetActive(true);
            stopButton.gameObject.SetActive(false);
        }
    }

    protected virtual void skipButton_Click()
    {
        skipping = true;
        skipButton.interactable = false;
    }

    private void start()
    {
        lock (locker)
        {
            note = "Loading...";
        }

        try
        {
            if (EMG.start() == 1)
            {
                //Create data acquisition threads
                Validator.reset();
                emgThread = new Thread(EMG.emgWorker);
                emgThread.Start();
            }
        }
        catch { }

        QueueOnMainThread(() => calibratePanel.Show("Tutorial Starting in", HandPosition.Relax, countdownTime, false));
        countdownTimer.Start();
        for (int i = countdownTime; i > 0; i--)
        {
            lock (locker)
            {
                note = $"Tutorial Starting in {i}";
            }
            check.WaitOne();
        }
        countdownTimer.Stop();

        lock (locker)
        {
            note = "Tutorial Started";
        }

        //turn on the skip button
        skipping = false;
        QueueOnMainThread(() =>
        {
            quitButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(true);
            skipButton.interactable = true;
        });

        //run the tutoruial
        Tutorial();

        //turn off the skip button
        QueueOnMainThread(() => {
            quitButton.gameObject.SetActive(true);
            skipButton.gameObject.SetActive(false);
        });

        lock (locker)
        {
            note = "Game Running";
        }

        //start the Game thread
        gameThread = new Thread(Game);
        gameThread.Start();

        //wait for game thread to complete
        done.WaitOne();

        //stop collecting emgData
        emgThread?.Abort();

        lock (locker)
        {
            note = "Game Stopped";
        }

        //close the EMG reader
#if UNITY_EDITOR || DEV
        if (!Dev.enabled)
        {
            EMG.stop();
        }
#else
        EMG.stop();
#endif
        //show the appropriate buttons
        QueueOnMainThread(() =>
        {
            startButton.gameObject.SetActive(true);
            stopButton.gameObject.SetActive(false);
            stopButton.interactable = false;

            quitButton.interactable = true;
        });

        gameThread?.Abort();
    }

    protected virtual void Tutorial()
    {
        int holdTime = 3;
        countdownTimer.Start();

        int count = 0;

        //Coroutine runs while measuring for rest
        IEnumerator MatchSlidersRest()
        {
            while (count < holdTime && !skipping)
            {
                //Lerp UP the flex slider thresholds to be ABOVE activation
                if (FlexThresholdSlider.PercentMax > 0)
                    Settings.activationFlexThreshold = Mathf.Clamp(Mathf.Lerp(Settings.activationFlexThreshold, FlexThresholdSlider.Value + 0.05f, 0.1f * Time.deltaTime), 0.05f, 0.95f);
                //Lerp UP the ext slider thresholds to be ABOVE activation
                if (ExtThresholdSlider.PercentMax > 0)
                    Settings.activationExtThreshold = Mathf.Clamp(Mathf.Lerp(Settings.activationExtThreshold, ExtThresholdSlider.Value + 0.05f, 0.1f * Time.deltaTime), 0.05f, 0.95f);
                yield return new WaitForEndOfFrame();
            }
        }

        //show a message and a wait a second before starting
        QueueOnMainThread(() => {
            calibratePanel.Show("Try relaxing", HandPosition.Relax);
            skipButton.interactable = true;
            skipping = false;
        });
        check.WaitOne();

        //QueueOnMainThread(() => StartCoroutine(MatchSlidersRest()));
        while (count < holdTime && !skipping)
        {
            if (FlexThresholdSlider.PercentMax <= 0 && ExtThresholdSlider.PercentMax <= 0)
            {
                if (count == 0)
                {
                    QueueOnMainThread(() => calibratePanel.Show("Try relaxing", HandPosition.Relax, holdTime, true));
                }
                count++;
            }
            else
            {
                count = 0;
                QueueOnMainThread(() => calibratePanel.Show("Try relaxing", HandPosition.Relax));
            }
            check.WaitOne();
        }
        //QueueOnMainThread(() => StopCoroutine(MatchSlidersRest()));

        //Coroutine runs while measuring for flex
        IEnumerator MatchSlidersFlex()
        {
            while (count < holdTime && !skipping)
            {
                //Lerp DOWN the flex slider threshold to be BELOW activation
                if (FlexThresholdSlider.PercentMax <= 0)
                    Settings.activationFlexThreshold = Mathf.Clamp(Mathf.Lerp(Settings.activationFlexThreshold, FlexThresholdSlider.Value - 0.05f, 0.1f * Time.deltaTime), 0.05f, 0.95f);
                //Lerp UP the ext slider thresholds to be ABOVE activation
                if (ExtThresholdSlider.PercentMax > 0)
                    Settings.activationExtThreshold = Mathf.Clamp(Mathf.Lerp(Settings.activationExtThreshold, ExtThresholdSlider.Value + 0.05f, 0.1f * Time.deltaTime), 0.05f, 0.95f);
                yield return new WaitForEndOfFrame();
            }
        }

        //show a message and wait a second before starting
        QueueOnMainThread(() => {
            calibratePanel.Show($"Try movinng hand {FlexText}", HandPosition.Flex);
            skipButton.interactable = true;
            skipping = false;
        });
        check.WaitOne();

        count = 0;
        //QueueOnMainThread(() => StartCoroutine(MatchSlidersFlex()));
        while (count < holdTime && !skipping)
        {
            if (FlexThresholdSlider.PercentMax > 0)
            {
                if (count == 0)
                {
                    QueueOnMainThread(() => calibratePanel.Show($"Try movinng hand {FlexText}", HandPosition.Flex, holdTime, true));
                }
                count++;
            }
            else
            {
                count = 0;
                QueueOnMainThread(() => calibratePanel.Show($"Try movinng hand {FlexText}", HandPosition.Flex));
            }
            check.WaitOne();
        }
        //QueueOnMainThread(() => StopCoroutine(MatchSlidersFlex()));

        //Coroutine runs while measuring for ext
        IEnumerator MatchSlidersExt()
        {
            while (count < holdTime && !skipping)
            {
                //Lerp UP the flex slider threshold to be ABOVE activation
                if (FlexThresholdSlider.PercentMax > 0)
                    Settings.activationFlexThreshold = Mathf.Clamp(Mathf.Lerp(Settings.activationFlexThreshold, FlexThresholdSlider.Value + 0.05f, 0.1f * Time.deltaTime), 0.05f, 0.95f);
                //Lerp DOWN the ext slider threshold to be BELOW activation
                if (ExtThresholdSlider.PercentMax <= 0)
                    Settings.activationExtThreshold = Mathf.Clamp(Mathf.Lerp(Settings.activationExtThreshold, ExtThresholdSlider.Value - 0.05f, 0.1f * Time.deltaTime), 0.05f, 0.95f);
                yield return new WaitForEndOfFrame();
            }
        }

        //show a message and wait a second before starting
        QueueOnMainThread(() => {
            calibratePanel.Show($"Try movinng hand {ExtText}", HandPosition.Ext);
            skipButton.interactable = true;
            skipping = false;
        });
        check.WaitOne();

        count = 0;
        //QueueOnMainThread(() => StartCoroutine(MatchSlidersExt()));
        while (count < holdTime && !skipping)
        {
            if (ExtThresholdSlider.PercentMax > 0)
            {
                if (count == 0)
                {
                    QueueOnMainThread(() => calibratePanel.Show($"Try movinng hand {ExtText}", HandPosition.Ext, holdTime, true));
                }
                count++;
            }
            else
            {
                count = 0;
                QueueOnMainThread(() => calibratePanel.Show($"Try movinng hand {ExtText}", HandPosition.Ext));
            }
            check.WaitOne();
        }
        //QueueOnMainThread(() => StopCoroutine(MatchSlidersExt()));

        countdownTimer.Stop();
        QueueOnMainThread(() => calibratePanel.Hide());
    }

    protected abstract void Game();

    private void calibrate()
    {
        //Starts the EMG ( to be used on seperate thread )
        void EMGStart()
        {
            try
            {
                EMG.start();
            }
            catch { }
        }

        countdownTimer.Start();
        QueueOnMainThread(() => calibratePanel.Show("Get ready to Relax your arm", HandPosition.Relax, countdownTime, false));

        //start the EMG on a seperate thread 
        Thread startThread = new Thread(EMGStart);
        startThread.Start();

        //Relax your arm in 3 2 1
        for (int i = countdownTime; i > 0; i--)
        {
            lock (locker)
            {
                note = $"Relax your arm in {i}";
            }
            check.WaitOne();
        }
        countdownTimer.Stop();

        lock (locker)
        {
            note = "Loading...";
        }

        startThread.Join(); //wait for the emg to start

#if UNITY_EDITOR || DEV
        if (Dev.enabled)
            calibrateDev();
        else
#endif
            calibrateRest();

        //Activate your flexors in 3 2 1
        countdownTimer.Start();
        QueueOnMainThread(() => calibratePanel.Show($"Get ready to move your hand {FlexText}", HandPosition.Flex, countdownTime, false));

        startThread = new Thread(EMGStart);
        startThread.Start();

        for (int i = countdownTime; i > 0; i--)
        {
            lock (locker)
            {
                note = $"Move your hand {FlexText} in {i}";
            }
            check.WaitOne();
        }
        countdownTimer.Stop();

        lock (locker)
        {
            note = "Loading...";
        }

        startThread.Join(); //wait for the emg to start

#if UNITY_EDITOR || DEV
        if (Dev.enabled)
            calibrateDev();
        else
#endif
            calibrateFlex();

        //Activate your extensor in 3 2 1

        countdownTimer.Start();
        QueueOnMainThread(() => calibratePanel.Show($"Get ready to move your hand {ExtText}", HandPosition.Ext, countdownTime, false));

        startThread = new Thread(EMGStart);
        startThread.Start();

        for (int i = countdownTime; i > 0; i--)
        {
            lock (locker)
            {
                note = $"Move your hand {ExtText} in {i}";
            }
            check.WaitOne();
        }
        countdownTimer.Stop();

        lock (locker)
        {
            note = "Loading...";
        }
        startThread.Join(); //wait for the emg to start

#if UNITY_EDITOR || DEV
        if (Dev.enabled)
            calibrateDev();
        else
#endif
            calibrateExt();

        //Calibration finished
        lock (locker)
        {
            note = "Calibration Complete!";
        }

        QueueOnMainThread(() =>
        {
            calibratePanel.Hide();
            startButton.interactable = true;
            quitButton.interactable = true;
        });

        emgThread?.Abort();
    }

#if UNITY_EDITOR || DEV
    protected virtual void calibrateDev()
    {
        countdownTimer.Start();

        for (int i = Mathf.FloorToInt(EMG_Socket.time) + 1; i > 0; i--)
        {
            if (i == Mathf.FloorToInt(EMG_Socket.time))
            {
                QueueOnMainThread(() => calibratePanel.Show("Continue Relaxing", HandPosition.Relax, EMG_Socket.time, true));
            }
            else
            {
                lock (locker)
                {
                    note = $"Please Hold for {Mathf.Min(i, EMG_Socket.time)} seconds";
                }
            }
            check.WaitOne();
        }

        lock (locker)
        {
            note = "Processing...";
        }

        check.WaitOne();
        countdownTimer.Stop();
    }
#endif

    protected virtual void calibrateRest()
    {
        //Please hold for 5 seconds 
        if (EMG_Socket.running)
        {
            //start the EMG thread
            emgThread = new Thread(EMG.AvgNoise);
            emgThread.Start();

            //UI display 
            QueueOnMainThread(() => calibratePanel.Show("Continue Relaxing", HandPosition.Relax, EMG_Socket.time, true));
            lock (locker)
            {
                note = $"Please hold for {EMG_Socket.time} seconds.";
            }
            Validator.valid.WaitOne(); //Wait for AvgNoise to Complete
        }
        lock (locker)
        {
            note = "Processing...";
        }
        EMG.stop();
    }

    protected virtual void calibrateFlex()
    {

        //Continue activating for 5 seconds
        if (EMG_Socket.running)
        {
            //start the EMG thread 
            emgThread = new Thread(EMG.calibrate);
            emgThread.Start();

            QueueOnMainThread(() => calibratePanel.Show($"Continue holding hand {FlexText}", HandPosition.Flex, EMG_Socket.time, true));
            lock (locker)
            {
                note = $"Continue holding for {EMG_Socket.time} seconds.";
            }
            Validator.valid.WaitOne();
        }
        lock (locker)
        {
            note = "Processing...";
        }
        EMG.stop();
    }

    protected virtual void calibrateExt()
    {
        //Continue activating for 5 seconds
        if (EMG_Socket.running)
        {
            //continue the EMG thread
            Validator.valid.Set();

            QueueOnMainThread(() => calibratePanel.Show($"Continue holding hand {ExtText}", HandPosition.Ext, EMG_Socket.time, true));
            lock (locker)
            {
                note = $"Continue holding for {EMG_Socket.time} seconds.";
            }
            Validator.valid.WaitOne();
        }
        lock (locker)
        {
            note = "Processing...";
        }
        EMG.stop();
    }

    private void countdownTimerTick(object sender, ElapsedEventArgs e)
    {
        check.Set();
    }

    /// <summary>
    /// adds Action to run on the main thread
    /// used for UI and any Unity methods that must be called on the main thread
    /// </summary>
    /// <param name="action"></param>
    protected void QueueOnMainThread(Action action)
    {
        if (mainQueue == null)
        {
            mainQueue = new Queue<Action>();
        }
        mainQueue.Enqueue(action);
    }

    /// <summary>
    /// Run all the Actions sent to the main thread
    /// </summary>
    private void ExectuteQueueOnMainThread()
    {
        //make sure there are actions to execute
        while (mainQueue != null && mainQueue.Count > 0)
        {
            //Dequeue and Invoke
            Action queuedAction = mainQueue.Dequeue();
            queuedAction?.Invoke();
        }
    }

    protected virtual void Update()
    {
        ExectuteQueueOnMainThread();

        instruct.text = note;

        FlexThresholdSlider.Threshold = Settings.activationFlexThreshold;
        ExtThresholdSlider.Threshold = Settings.activationExtThreshold;


#if UNITY_EDITOR || DEV
        if (Input.GetKeyDown(KeyCode.F11))
        {
            Dev.enabled = !Dev.enabled;
            Debug.Log("Dev mode: " + (Dev.enabled ? "ON" : "OFF"));
        }

        if (Dev.enabled)
        {
            //return;
        }
#endif

        if (EMG_Socket.running)
        {
            try
            {
                lock (locker)
                {
                    FlexThresholdSlider.Value = Validator.flex_norm;
                    ExtThresholdSlider.Value = Validator.ext_norm;

                    Flex = FlexThresholdSlider.PercentMax;
                    Ext = ExtThresholdSlider.PercentMax;
                }

            }
            catch (ArgumentOutOfRangeException)
            { }
        }
        else
        {
            lock (locker)
            {
                FlexThresholdSlider.Value = 0;
                ExtThresholdSlider.Value = 0;
            }
        }

        UpdateUI();
        
        UpdateDifficulty();

        UpdateLSLInfo();
    }

    //game specific methods

    protected abstract void UpdateLSLInfo();
    protected abstract void UpdateUI();
    protected abstract void UpdateDifficulty();

    public void Pause()
    {
        if (GameRunning)
        {
            Settings.gamePaused = true;
            lock (locker)
            {
                note = "Game Paused";
            }
        }
    }

    public void Resume()
    {
        if (GameRunning)
        {
            Settings.gamePaused = false;
            lock (locker)
            {
                note = "Game Running";
            }
        }
    }
}
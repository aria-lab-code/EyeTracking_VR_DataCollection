using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;
using ViveSR.anipal.Eye;
using ViveSR.anipal;
using ViveSR;
using UnityEngine.UI;
using Valve.VR.InteractionSystem;
using System.Diagnostics;

public class GazeDataCollection : MonoBehaviour
{
    #region "file ID information"
    /// <summary>
    /// Define user ID information. The developers can define the user ID format such as "ABC_001".
    /// The ID is used for the name of the text file that records the measured eye movement data.
    /// </summary>
    public int UserIDNum = 0;       // TODO: Change this value for each participant
    public string Path;             // TODO: Change this to the path to the SSD directory
    private static string UserID;
    private static string testNum;
    private static string File_Path;
    #endregion

    #region "GUI interactions"
    public TextMesh breakMessage, countdownMessage;
    public Canvas BreakCanvas, CountdownCanvas;
    private static GameObject GazeObject1, GazeObject2, GazeObject3, TrackObjectLine, TrackObjectArc;
    private bool continueClicked;
    #endregion

    private bool firstFrame;

    private const int SHORT_BREAK = 5;
    private float gameTime;
    private static bool rapidTesting;
    

    #region "EyeDataParameters"
    public EyeParameter eye_parameter = new EyeParameter();
    public GazeRayParameter gaze = new GazeRayParameter();
    private static EyeData_v2 eyeData = new EyeData_v2();
    private static bool eye_callback_registered = false;
    private const int maxframe_count = 120 * 60;
    private static UInt64 eye_valid_L, eye_valid_R;
    private static Vector3 gaze_origin_L, gaze_origin_R, origin_L, origin_R;
    private static Vector3 gaze_direct_L, gaze_direct_R, direct_L, direct_R;
    private static double gaze_sensitive;
    private static Stopwatch timer = new Stopwatch();
    private static Vector3 forward;
    #endregion

    private GameObject visual;



    void InitLineRenderer(LineRenderer lr)
    {
        lr.startWidth = 0.005f;
        lr.endWidth = 0.005f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// Parameters for time-related information.
    /// </summary>
    public static int cnt_callback = 0;
    public int Endbuffer = 3, SaccadeTimer = 10;
    private static Matrix4x4 localToWorldTransform;

    private static long MeasureTime, CurrentTime, EndTime = 0, timeSpan;
    private static float time_stamp, sync_time_stamp;
    private static int frame;

    private const float mm_to_m = 1.0f / 1000.0f;
    private const int NUM_SAVED_DATA_POINTS = 29;

    private static Vector3 currentPos = Vector3.zero, currentVel = Vector3.zero;

    /// <summary>
    /// Start is called before the first frame update. The Start() function is performed only one time.
    /// </summary>
    void Start()
    {
        UserID = UserIDNum.ToString();
        Path = Directory.GetCurrentDirectory();
        File_Path = Path + "\\gazedata_" + UserID + ".npy";
        //Sync_File_Path = Path + "\\gazedata_sync_" + UserID + ".npy";
        timer.Start();
        SRanipal_Eye_Framework.Instance.EnableEyeDataCallback = true;
        rapidTesting = false;
        frame = 0;

        continueClicked = false;
        forward = Camera.main.transform.forward;

        InputUserID();                              // Check if the file with the same ID exists.
        Invoke("SystemCheck", 0.5f);                // System check.
        SRanipal_Eye_v2.LaunchEyeCalibration();     // Perform calibration for eye tracking.
        //Invoke("Measurement", 0.5f);                // Start the measurement of ocular movements in a separate callback function.

        firstFrame = true;
        GazeObject1 = GameObject.Find("Gaze Focusable Object 1");
        GazeObject2 = GameObject.Find("Gaze Focusable Object 2");
        GazeObject3 = GameObject.Find("Gaze Focusable Object 3");
        TrackObjectLine = GameObject.Find("Tracking Object 1");
        TrackObjectArc = GameObject.Find("Tracking Object 2");
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        TrackObjectLine.SetActive(false);
        TrackObjectArc.SetActive(false);
        
    }

    void Data_txt()
    {
        string variable =
        "time(100ns)" + "," +
        "time_stamp(ms)" + "," +
        "frame" + "," +
        //"eye_valid_L" + "," +
        //"eye_valid_R" + "," +
        //"gaze_origin_L.x(mm)" + "," +
        //"gaze_origin_L.y(mm)" + "," +
        //"gaze_origin_L.z(mm)" + "," +
        //"gaze_origin_R.x(mm)" + "," +
        //"gaze_origin_R.y(mm)" + "," +
        //"gaze_origin_R.z(mm)" + "," +
        "gaze_direct_L.x" + "," +
        "gaze_direct_L.y" + "," +
        "gaze_direct_L.z" + "," +
        "gaze_direct_R.x" + "," +
        "gaze_direct_R.y" + "," +
        "gaze_direct_R.z" + "," +
        //"gaze_sensitive" + "," +
        "forward.x" + "," +
        "forward.y" + "," +
        "forward.z" + "," +
        Environment.NewLine;

        File.AppendAllText(UserID + "_" + testNum + ".txt", variable);
    }

    /// <summary>
    /// Return the ending time of saccade task to EyeCallback function.
    /// </summary>
    static long GetEndTime()
    {
        return EndTime;
    }


    /// <summary>
    /// Checks if the filename with the same user ID already exists. If so, you need to change the name of UserID.
    /// </summary>
    void InputUserID()
    {
        UnityEngine.Debug.Log(File_Path);
        if (File.Exists(File_Path))
        {
            UnityEngine.Debug.Log("File with the same UserID already exists. Please change the UserID in the C# code.");
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }

    void Measurement()
    {
        EyeParameter eye_parameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);
        Data_txt();

        if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING)
        {
            UnityEngine.Debug.Log("Not working");
            return;
        }

        UnityEngine.Debug.Log(SRanipal_Eye_Framework.Instance.EnableEyeDataCallback.ToString());
        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == true && eye_callback_registered == false)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
            eye_callback_registered = true;
        }

        else if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == false && eye_callback_registered == true)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
            eye_callback_registered = false;
        }
    }

    void Release()
    {
        if (eye_callback_registered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
            eye_callback_registered = false;
        }
    }

    /// <summary>
    /// Check if the system works properly.
    /// </summary>
    void SystemCheck()
    {
        if (SRanipal_Eye_API.GetEyeData_v2(ref eyeData) == ViveSR.Error.WORK)
        {
            UnityEngine.Debug.Log("Device is working properly.");
        }

        if (SRanipal_Eye_API.GetEyeParameter(ref eye_parameter) == ViveSR.Error.WORK)
        {
            UnityEngine.Debug.Log("Eye parameters are measured.");
        }
        
        Error result_eye_init = SRanipal_API.Initial(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, IntPtr.Zero);

        if (result_eye_init == Error.WORK)
        {
            UnityEngine.Debug.Log("[SRanipal] Initial Eye v2: " + result_eye_init);
        }
        else
        {
            UnityEngine.Debug.LogError("[SRanipal] Initial Eye v2: " + result_eye_init);
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }

    /// <summary>
    /// Changes the flag to indicate that one of the menu continue buttons has been clicked.
    /// </summary>
    public void ContinueClicked()
    {
        continueClicked = true;
    }

    struct RawGazeRays
    {
        public Vector3 origin;
        public Vector3 dir;


        public RawGazeRays Absolute(Transform t)
        {
            var ans = new RawGazeRays();
            ans.origin = t.TransformPoint(origin);
            ans.dir = t.TransformDirection(dir);
            return ans;
        }
    }

    void GetGazeRays(out RawGazeRays r)
    {
        r = new RawGazeRays();
        if (eye_callback_registered)
        {
            SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out r.origin, out r.dir, eyeData);
        }
        else
        {
            SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out r.origin, out r.dir);
        }
    }

    void RenderGazeRays(RawGazeRays gr)
    {
        LineRenderer lr = visual.GetComponent<LineRenderer>();
        lr.SetPosition(0, gr.origin);
        lr.SetPosition(1, gr.origin + gr.dir * 20);

    }


    /// <summary>
    /// Update is called once per frame.
    /// </summary>
    void Update()
    {
        frame++;
        localToWorldTransform = Camera.main.transform.localToWorldMatrix;
        forward = Vector3.Scale(Camera.main.transform.forward, new Vector3(-1, 1, 1));
        if (rapidTesting)
        {
            RawGazeRays localGazeRays;
            GetGazeRays(out localGazeRays);
            RawGazeRays gazeRays = localGazeRays.Absolute(Camera.main.transform);

            Ray gaze = new Ray(gazeRays.origin, gazeRays.dir);
            RaycastHit hit;
            if (Physics.Raycast(gaze, out hit))
            {
                if (hit.transform.gameObject.CompareTag("GazeObject1"))
                {
                    GazeObject1.GetComponent<HighlightAtGaze>().GazeFocusChanged(true);
                    GazeObject2.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject3.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("GazeObject2"))
                {
                    GazeObject1.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject2.GetComponent<HighlightAtGaze>().GazeFocusChanged(true);
                    GazeObject3.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("GazeObject3"))
                {
                    GazeObject1.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject2.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject3.GetComponent<HighlightAtGaze>().GazeFocusChanged(true);
                }
                else
                {
                    GazeObject1.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject2.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject3.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }   
            }
        }
        
        if (firstFrame)
        {
            StartCoroutine(Sequence());
            firstFrame = false;
        }
    }
    
    
    /// <summary>
    /// Callback function to record the eye movement data.
    /// Note that SRanipal_Eye_v2 does not work in the function below. It only works under UnityEngine.
    /// </summary>
    /// <param name="eye_data"></param>
    private static void EyeCallback(ref EyeData_v2 eye_data)
    {
        if (cnt_callback > maxframe_count)
        {
            return;
        }
        EyeParameter eye_parameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);
        eyeData = eye_data;
        

        // Measure eye movements at the frequency of 120Hz until framecount reaches the max framecount set.
        Error error = SRanipal_Eye_API.GetEyeData_v2(ref eyeData);
        if (error == ViveSR.Error.WORK)
        {
            // -----------------------------------------------------------------------------------------
            //  Measure each parameter of eye data that are specified in the guideline of SRanipal SDK.
            // -----------------------------------------------------------------------------------------
            MeasureTime = DateTime.Now.Ticks;
            timeSpan = timer.ElapsedMilliseconds;
            time_stamp = eyeData.timestamp;
            eye_valid_L = eyeData.verbose_data.left.eye_data_validata_bit_mask;
            eye_valid_R = eyeData.verbose_data.right.eye_data_validata_bit_mask;
            gaze_origin_L = eyeData.verbose_data.left.gaze_origin_mm * mm_to_m; // right handed coordinate system
            gaze_origin_R = eyeData.verbose_data.right.gaze_origin_mm * mm_to_m;
            gaze_direct_L = eyeData.verbose_data.left.gaze_direction_normalized;
            gaze_direct_R = eyeData.verbose_data.right.gaze_direction_normalized;
            //gaze_direct_Combined = eyeData.verbose_data.combine.gaze_direction_normalized;
            gaze_sensitive = eye_parameter.gaze_ray_parameter.sensitive_factor;
            //UnityEngine.Debug.Log("Data Measured");


            //origin_L = localToWorldTransform.MultiplyPoint(gaze_origin_L);
            //origin_R = localToWorldTransform.MultiplyPoint(gaze_origin_R);

            //direct_L = localToWorldTransform.MultiplyVector(gaze_direct_L);
            //direct_R = localToWorldTransform.MultiplyVector(gaze_direct_R);

            string value =
                MeasureTime.ToString() + "," +
                timeSpan.ToString() + "," +
                frame.ToString() + "," +
                //eye_valid_L.ToString() + "," +
                //eye_valid_R.ToString() + "," +
                //gaze_origin_L.x.ToString() + "," +
                //gaze_origin_L.y.ToString() + "," +
                //gaze_origin_L.z.ToString() + "," +
                //gaze_origin_R.x.ToString() + "," +
                //gaze_origin_R.y.ToString() + "," +
                //gaze_origin_R.z.ToString() + "," +
                gaze_direct_L.x.ToString() + "," +
                gaze_direct_L.y.ToString() + "," +
                gaze_direct_L.z.ToString() + "," +
                gaze_direct_R.x.ToString() + "," +
                gaze_direct_R.y.ToString() + "," +
                gaze_direct_R.z.ToString() + "," +
                //gaze_sensitive.ToString() + "," +
                forward.x.ToString() + "," +
                forward.y.ToString() + "," +
                forward.z.ToString() + "," +
                Environment.NewLine;
            
            File.AppendAllText(UserID + "_" + testNum + ".txt", value);

            cnt_callback++;
        }
        
    }

    /// <summary>
    /// Saccade task sequence.
    /// </summary>
    private IEnumerator Sequence()
    {
        UnityEngine.Debug.Log("Sequence started");
        // DO NOT REMOVE THE PRIVACY STATEMENT, REQUIRED BY HTC 
        // Participants should see the paper version during the consent process https://docs.google.com/document/d/13ehQgG4bj30qM26owmaHe9gsbGhAz9uMMaSYZKIm2cA/edit?usp=sharing
        breakMessage.text = "Welcome to the virtual environment. The following is a version of the privacy statement you should have already seen during the consent process." +
            "\nIf you have not seen this do not continue until the staff provide you with a physical copy of this and have explained it and answered any questions to your satifaction." +
            "\n\n Privacy Statement: While using this virtual environment, data about your facial expressions will be saved." +
            "\n This includes head position and orientation, gaze origin, gaze direction, gaze sensitivity scale, validity of data, time stamps of the data, and details concerning items in the virtual environment." +
            "\nWe will not collect images of your eyes, and the data collected from this environment should not be able to identify you when used independently of our other records." +
            "\nWe will never sell this data to another party, and we will work to maintain its confidentiality to the best of our ability." +
            "\nWe will not share this information with individuals outside of our research team without your consent, and we will not use this data to discriminate against any party." +
            "\nThis data wil not be used to make decisions regarding eligibility or terms for any services, including loans. We will not use third party services to process this data without your consent." +
            "\nWe will use this data to learn how paitients experiencing limited neck mobility may regain a portion of autonomy by controlling an assistive neck brace. " +
            "\nBecause we are using this data for a healthcare purpose, we will comply with regulations such as HIPAA as it applies to any data collected." +
            "\nWe will follow other procedures to ensure all of your data is protected and not misused. If you are concerned that your data will be or has been misused, or are concerned about the data being saved, " +
            "\ndiscontinue participation in the study immediately and contact the University of Utah IRB. This privacy statement was last modified August 18, 2022." +
            "\n\nPress continue if you agree with the privacy statement and are ready to begin.";
        yield return StartCoroutine(DisplayBreakMenu());

        breakMessage.text = "For the Rapid Movement section of the test, 3 cubes wil spawn in various locations in front of you and begin to move towards you." +
            "\nLook at the cubes to reset them before they reach you. This test will last for 60 seconds. Press continue when you are ready to begin.";
        yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidMovementTest());
        
        breakMessage.text = "Rapid Movement Test Complete! When you are ready to begin the Linear Pursuit section of the test, press continue.";
        yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(LinearPursuit());

        breakMessage.text = "Linear Pursuit Test Complete! When you are ready to begin the Arc Pursuit section of the test, press continue.";
        yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(ArcPursuit());

        breakMessage.text = "All Tests Complete! Thank you!";
        yield return StartCoroutine(DisplayBreakMenu());
    }

    /// <summary>
    /// Suspends the game until the continue button is pressed.
    /// This can be used to give the user a break between sections where the data gathered can be ignored.
    /// </summary>
    private IEnumerator DisplayBreakMenu()
    {
        continueClicked = false;
        BreakCanvas.enabled = true;
        BreakCanvas.gameObject.SetActive(true);

        while (!continueClicked)
        {
            yield return null;
        }

        BreakCanvas.enabled = false;
        BreakCanvas.gameObject.SetActive(false);
        continueClicked = false;
    }

    /// <summary>
    /// Displays a countdown timer for the user so they can prepare for the next task.
    /// </summary>
    /// <param name="duration">duration of the countdown in seconds</param>
    /// <param name="message">a message to precede each number during the countdown</param>
    private IEnumerator DisplayCountdown(int duration, string message)
    {
        CountdownCanvas.gameObject.SetActive(true);
        for (int i = duration; i > 0; i--)
        {
            countdownMessage.text = message + i.ToString();
            yield return new WaitForSeconds(1);
        }

        countdownMessage.text = "";
        CountdownCanvas.gameObject.SetActive(false);
    }

    private IEnumerator RapidMovementTest()
    {
        testNum = "0";
        GazeObject1.SetActive(true);
        GazeObject2.SetActive(true);
        GazeObject3.SetActive(true);
        cnt_callback = 0;
        rapidTesting = true;
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < 60)
        {
            //StartCoroutine(UpdateGazeObjects());
            yield return null;
        }
        rapidTesting = false;
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        Release();

    }

    private IEnumerator LinearPursuit()
    {
        testNum = "1";
        TrackObjectLine.SetActive(true);
        cnt_callback = 0;
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < 60)
        {
            yield return null;
        }
        TrackObjectLine.SetActive(false);
        Release();
    }

    private IEnumerator ArcPursuit()
    {
        testNum = "2";
        TrackObjectArc.SetActive(true);
        cnt_callback = 0;
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < 60)
        {
            yield return null;
        }
        TrackObjectArc.SetActive(false);
        Release();
    }

}


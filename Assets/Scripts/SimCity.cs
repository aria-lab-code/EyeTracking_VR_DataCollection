using System;
using System.Collections;
using System.Collections.Generic;
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
using Unity.Barracuda;

public class SimCity : MonoBehaviour
{
    private static int frame;
    public NNModel modelAsset;
    public static bool useModel;
    public bool use_model;
    private static IWorker worker;
    private static bool first_output;
    public bool use_quadrant;
    public static bool useQuadrant;
    // public GameObject GazeObject1;
    // public GameObject GazeObject2, GazeObject3, TrackObjectLine, TrackObjectArc;

    public static int playerID;
    private static Model m_RuntimeModel;
    public bool is_MLP;
    private static Transform player;
    
    private static long MeasureTime;
    private static Vector3 gaze_direct_L, gaze_direct_R, forward;
    private static ArrayList gaze_L_buff, gaze_R_buff, forward_buff;  // Create a buffer to hold a small history of states
    private const int BUFFER_CAPACITY = 3;
    private static Tensor input, hn, cn;

    /*#region "GUI interactions"
    public TextMesh breakMessage, countdownMessage;
    public Canvas BreakCanvas, CountdownCanvas;
    private bool continueClicked;
    #endregion*/

    public EyeParameter eye_parameter = new EyeParameter();
    public GazeRayParameter gaze = new GazeRayParameter();
    private static EyeData_v2 eyeData = new EyeData_v2();
    private static bool eye_callback_registered = false;

    private bool firstFrame;
    private bool rapidTesting;

    private const int SECONDS_PER_TRIAL = 30;
    private const int SHORT_BREAK = 3;
    private float gameTime;

    public bool calibrate;

    // Start is called before the first frame update
    void Start()
    {
        Invoke("SystemCheck", 0.5f);                // System check.
        if (calibrate)
        {
            SRanipal_Eye_v2.LaunchEyeCalibration();
        }
        SRanipal_Eye_Framework.Instance.EnableEyeDataCallback = true;

        player = Camera.main.transform.parent.parent;
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
        hn = new Tensor(1, 1, 9, 1);
        cn = new Tensor(1, 1, 9, 1);
        if (is_MLP)
        {
            input = new Tensor(1, 1, 27, 1);
        }
        else
        {
            input = new Tensor(1, 1, 9, 1);
        }
        
        first_output = true;
        rapidTesting = false;
        DisableHeadTracking.Disable = false;

        // Create buffers for storing the history of gaze vectors.
        gaze_L_buff = new ArrayList();
        gaze_R_buff = new ArrayList();
        forward_buff = new ArrayList();

        firstFrame = true;
        // GazeObject1 = GameObject.Find("Gaze Focusable Object 1");
        //  GazeObject2 = GameObject.Find("Gaze Focusable Object 2");
        // GazeObject3 = GameObject.Find("Gaze Focusable Object 3");
        // TrackObjectLine = GameObject.Find("Tracking Object 1");
        // TrackObjectArc = GameObject.Find("Tracking Object 2");

        /*
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        TrackObjectLine.SetActive(false);
        TrackObjectArc.SetActive(false);
        */
        
        // continueClicked = false;
        useModel = use_model;
        useQuadrant = use_quadrant;
    }

    void Measurement()
    {
        EyeParameter eye_parameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);

        if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING)
        {
            UnityEngine.Debug.Log("Not working");
            return;
        }

        UnityEngine.Debug.Log(SRanipal_Eye_Framework.Instance.EnableEyeDataCallback.ToString());
        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == true && eye_callback_registered == false)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GetData));
            eye_callback_registered = true;
        }
        else if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback == false && eye_callback_registered == true)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GetData));
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
        
        SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out r.origin, out r.dir, eyeData);
    }

    /// <summary>
    /// Changes the flag to indicate that one of the menu continue buttons has been clicked.
    /// </summary>
    public void ContinueClicked()
    {
        // continueClicked = true;
    }
    
    // Update is called once per frame
    void Update()
    {
        setForwardVector(player.forward);

        if (useModel)
        {
            if (is_MLP){
                ModelCallMLP();
            }
            else
            {
                ModelCallLSTM();
            }
        }
        else if (useQuadrant)
        {
            QuadrantBaseline();
        }
        else
        {
            VectorBaseline();
        }

        if (rapidTesting)
        {
            RawGazeRays localGazeRays;
            GetGazeRays(out localGazeRays);
            RawGazeRays gazeRays = localGazeRays.Absolute(Camera.main.transform);

            Ray gaze = new Ray(gazeRays.origin, gazeRays.dir);
            RaycastHit hit;
            /*if (Physics.Raycast(gaze, out hit))
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
            }*/
        }

        if (firstFrame)
        {
            StartCoroutine(Sequence());
            firstFrame = false;
        }
    }

    private static void ModelCallLSTM()
    {
        for (int i=0; i<3; i++)
        {
            input[0, 0, i, 0] = gaze_direct_L[i];
            input[0, 0, i+3, 0] = gaze_direct_R[i];
            input[0, 0, i+6, 0] = forward[i];
        }
        
        var Inputs = new Dictionary<string, Tensor>(){
            {"input", input},
            {"h0", hn},
            {"c0", cn}
        };

        worker.Execute(Inputs);
        var output = worker.PeekOutput("output");
        hn = worker.PeekOutput("hn");
        cn = worker.PeekOutput("cn");
        var new_forward = new Vector3(output[0,0,0,0]-0.05f, output[0,0,0,1], output[0,0,0,2]).normalized;

        Quaternion rotation = Quaternion.LookRotation(new_forward);
        if (DisableHeadTracking.Disable)
        {
            player.rotation = Quaternion.Slerp(player.rotation, rotation, Time.deltaTime*5.0f);
        }
    }

    private static void ModelCallMLP()
    {
        const int CONTEXT_SIZE = 3;
        const int DATA_PER_TIMESTEP = 9;

        if (gaze_L_buff.Count < CONTEXT_SIZE)
        {
            return;
        }

        // Build the input vector
        for (int c = 0; c < CONTEXT_SIZE; c++)
        {
            var ind = c * DATA_PER_TIMESTEP;

            Vector3 l_c = (Vector3)gaze_L_buff[c];
            Vector3 r_c = (Vector3)gaze_R_buff[c];
            Vector3 f_c = (Vector3)forward_buff[c];

            input[0, 0, ind, 0] = l_c.x;
            input[0, 0, ind + 1, 0] = l_c.y;
            input[0, 0, ind + 2, 0] = l_c.z;
            input[0, 0, ind + 3, 0] = r_c.x;
            input[0, 0, ind + 4, 0] = r_c.y;
            input[0, 0, ind + 5, 0] = r_c.z;
            input[0, 0, ind + 6, 0] = f_c.x;
            input[0, 0, ind + 7, 0] = f_c.y;
            input[0, 0, ind + 8, 0] = f_c.z;
        }

        //input[0, 0, 0, 0] = 0.3640442f;
        //input[0, 0, 1, 0] = 0.2774048f;
        //input[0, 0, 2, 0] = 0.8890991f;
        //input[0, 0, 3, 0] = 0.3574524f;
        //input[0, 0, 4, 0] = 0.2452698f;
        //input[0, 0, 5, 0] = 0.9011383f;
        //input[0, 0, 6, 0] = 0.3591461f;
        //input[0, 0, 7, 0] = 0.2465515f;
        //input[0, 0, 8, 0] = 0.900116f;
        //input[0, 0, 9, 0] = 0.3615723f;
        //input[0, 0, 10, 0] = 0.2754974f;
        //input[0, 0, 11, 0] = 0.8907013f;
        //input[0, 0, 12, 0] = 0.3569946f;
        //input[0, 0, 13, 0] = 0.2446289f;
        //input[0, 0, 14, 0] = 0.9014893f;
        //input[0, 0, 15, 0] = 0.3574524f;
        //input[0, 0, 16, 0] = 0.2452698f;
        //input[0, 0, 17, 0] = 0.9011383f;
        //input[0, 0, 18, 0] = 0.3592377f;
        //input[0, 0, 19, 0] = 0.2737427f;
        //input[0, 0, 20, 0] = 0.8921814f;
        //input[0, 0, 21, 0] = 0.3565521f;
        //input[0, 0, 22, 0] = 0.2440643f;
        //input[0, 0, 23, 0] = 0.901825f;
        //input[0, 0, 24, 0] = 0.3565521f;
        //input[0, 0, 25, 0] = 0.2440643f;
        //input[0, 0, 26, 0] = 0.901825f;

        var Inputs = new Dictionary<string, Tensor>(){
            {"onnx::Gemm_0", input},
        };

        worker.Execute(Inputs);
        var output = worker.PeekOutput("11");
        var forward = new Vector3(output[0, 0, 0, 0], output[0, 0, 0, 1], output[0, 0, 0, 2]);

        var new_forward = forward.normalized;

        Quaternion rotation = Quaternion.LookRotation(new_forward);
        if (DisableHeadTracking.Disable)
        {
            player.rotation = Quaternion.Slerp(player.rotation, rotation, Time.deltaTime * 5.0f);
        }
    }


    /// <summary>
    /// Rotate the player object by finding vector between current forward direction and eye gaze
    /// direction. Rotate in direction of this vector.
    /// </summary>
    private void VectorBaseline()
    {
        //Compute

        float angle_boundary = 5.0f;  //boundary of eye angle
        float rotate_speed = 4f;  //each rotate angle

        // eye angle in x direction > angle_boundry : rotate the 
        Vector3 gaze_direct_avg_world = player.rotation * (gaze_direct_L + gaze_direct_R).normalized;

        var angle = Vector3.Angle(gaze_direct_avg_world, forward);
        var global_angle = Vector3.Angle(gaze_direct_avg_world, new Vector3(0, 0, 1));
        if ((angle > angle_boundary || angle < -1 * angle_boundary) && (global_angle < 70f && global_angle > -70f))
        {
            player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(gaze_direct_avg_world), Time.deltaTime*rotate_speed);
        }
        
        var obj_weight = 10/ player.rotation.z;
        if(player.rotation.z == 0) obj_weight = 0;
    }

    private void QuadrantBaseline()
    {        
        float angle_boundary = 5.0f;
        float rotate_speed = 0.5f;

        Vector3 gaze_direct_avg_world = player.rotation * (gaze_direct_L + gaze_direct_R).normalized;

        Vector3 gaze_direct =  (gaze_direct_L + gaze_direct_R).normalized;

        var angle = Vector3.Angle(gaze_direct_avg_world, forward);
        var global_angle = Vector3.Angle(gaze_direct_avg_world, new Vector3(0, 0, 1));
        
        //UnityEngine.Debug.Log(global_angle);
        if ((angle > angle_boundary || angle < -1 * angle_boundary) && (global_angle < 70f && global_angle > -70f))
        {
            print(gaze_direct_avg_world);
            
            if ( gaze_direct.x <  gaze_direct.y)
            {
                
                if (gaze_direct.x > -1 * gaze_direct.y)
                {
                    player.Rotate(-rotate_speed, 0f, 0f);
                    //player.rotation = Quaternion.Slerp(player.rotation, up, Time.deltaTime*rotate_speed);
                }
                else
                {
                    player.Rotate(0, -rotate_speed, 0f, Space.World);
                    //player.rotation = Quaternion.Slerp(player.rotation, left, Time.deltaTime*rotate_speed);
                }
            }
            else
            {
                if ( gaze_direct.x > -1 *  gaze_direct.y)
                {
                    player.Rotate(0f, rotate_speed, 0f, Space.World);
                }
                else
                {
                    player.Rotate(rotate_speed, 0f, 0f);
                    //player.rotation = Quaternion.Slerp(player.rotation, down, Time.deltaTime*rotate_speed);
                }
            }
        }
    }

    private void ResetHead()
    {
        player.rotation = Quaternion.LookRotation(new Vector3(0,0,1));
    }

    /// <summary>
    /// Callback function to record the eye movement data.
    /// Note that SRanipal_Eye_v2 does not work in the function below. It only works under UnityEngine.
    /// </summary>
    private static void GetData(ref EyeData_v2 eye_data)
    {
        EyeParameter eye_parameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);
        eyeData = eye_data;

        Error error = SRanipal_Eye_API.GetEyeData_v2(ref eyeData);
        if (error == ViveSR.Error.WORK)
        {
            // gaze_direct_L = eyeData.verbose_data.left.gaze_direction_normalized;
            // gaze_direct_R = eyeData.verbose_data.right.gaze_direction_normalized;

            // gaze_direct_L.x = gaze_direct_L.x * -1;
            // gaze_direct_R.x = gaze_direct_R.x * -1;

            setGazeVectors(eyeData.verbose_data.left.gaze_direction_normalized, eyeData.verbose_data.right.gaze_direction_normalized);
        }
    }

    private static void setGazeVectors(Vector3 gazeL, Vector3 gazeR)
    {
        gazeL.x *= -1;
        gazeR.x *= -1;

        gaze_direct_L = gazeL;
        gaze_direct_R = gazeR;

        gaze_R_buff.Add(gaze_direct_R);
        if (gaze_R_buff.Count > BUFFER_CAPACITY)
        {
            gaze_R_buff.RemoveAt(0);
        }

        gaze_L_buff.Add(gaze_direct_L);
        if (gaze_L_buff.Count > BUFFER_CAPACITY)
        {
            gaze_L_buff.RemoveAt(0);
        }
    }

    private static void setForwardVector(Vector3 forwardVec)
    {
        forward = forwardVec;

        forward_buff.Add(gaze_direct_R);
        //forward_buff.Add(forward);
        if (forward_buff.Count > BUFFER_CAPACITY)
        {
            forward_buff.RemoveAt(0);
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
        /*breakMessage.text =
            "Welcome to the virtual environment. The following is a version of the privacy statement you should have already seen during the consent process." +
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
            "\n\nPress continue if you agree with the privacy statement and are ready to begin.";*/
        //yield return StartCoroutine(DisplayBreakMenu());

        /*breakMessage.text =
            "For the Rapid Movement section of the test, 3 cubes wil spawn in\n" +
            "various locations in front of you and begin to move towards you.\n" +
            "Look at the cubes to reset them before they reach you.\n" +
            "\n" +
            "This test will last for " + SECONDS_PER_TRIAL + " seconds.\n" +
            "Press continue when you are ready to begin.";*/
        //yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidMovementTest());
        
        /*breakMessage.text =
            "Rapid Movement Test Complete!\n" +
            "\n" +
            "When you are ready to begin the Linear Pursuit section of the test, press continue.";*/
        //yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(LinearPursuit());

        /*breakMessage.text =
            "Linear Pursuit Test Complete!\n" +
            "\n" +
            "When you are ready to begin the Arc Pursuit section of the test, press continue.";*/
        //yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(ArcPursuit());

        /*breakMessage.text =
            "All Tests Complete! Thank you!";*/
        //yield return StartCoroutine(DisplayBreakMenu());
    }

    /// <summary>
    /// Suspends the game until the continue button is pressed.
    /// This can be used to give the user a break between sections where the data gathered can be ignored.
    /// </summary>
    //private IEnumerator DisplayBreakMenu()
    //{
        /*continueClicked = false;
        BreakCanvas.enabled = true;
        BreakCanvas.gameObject.SetActive(true);

        while (!continueClicked)
        {
            yield return null;
        }

        BreakCanvas.enabled = false;
        BreakCanvas.gameObject.SetActive(false);
        continueClicked = false;*/
    //}

    /// <summary>
    /// Displays a countdown timer for the user so they can prepare for the next task.
    /// </summary>
    /// <param name="duration">duration of the countdown in seconds</param>
    /// <param name="message">a message to precede each number during the countdown</param>
    private IEnumerator DisplayCountdown(int duration, string message)
    {
        //CountdownCanvas.gameObject.SetActive(true);
        for (int i = duration; i > 0; i--)
        {
            //countdownMessage.text = message + i.ToString();
            yield return new WaitForSeconds(1);
        }

        //countdownMessage.text = "";
        //CountdownCanvas.gameObject.SetActive(false);
    }

    private IEnumerator RapidMovementTest()
    {
        /*GazeObject1.SetActive(true);
        GazeObject2.SetActive(true);
        GazeObject3.SetActive(true);*/
        rapidTesting = true;
        DisableHeadTracking.Disable = true;
        ResetModel();
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < SECONDS_PER_TRIAL)
        {
            //StartCoroutine(UpdateGazeObjects());
            yield return null;
        }
        rapidTesting = false;
        DisableHeadTracking.Disable = false;
        /*GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        */
        ResetHead();
        Release();
    }

    private IEnumerator LinearPursuit()
    {
        //TrackObjectLine.SetActive(true);
        DisableHeadTracking.Disable = true;
        ResetModel();
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < SECONDS_PER_TRIAL)
        {
            yield return null;
        }
        DisableHeadTracking.Disable = false;
        //TrackObjectLine.SetActive(false);
        ResetHead();
        Release();
    }

    private IEnumerator ArcPursuit()
    {
        //TrackObjectArc.SetActive(true);
        DisableHeadTracking.Disable = true;
        ResetModel();
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < SECONDS_PER_TRIAL)
        {
            yield return null;
        }
        DisableHeadTracking.Disable = false;
       // TrackObjectArc.SetActive(false);
        ResetHead();
        Release();
    }

    private void ResetModel()
    {
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
        hn = new Tensor(1, 1, 9, 1);
        cn = new Tensor(1, 1, 9, 1);
        if (is_MLP)
        {
            input = new Tensor(1, 1, 27, 1);
        }
        else { input = new Tensor(1, 1, 9, 1); }
        for (int i = 0; i < 9; i++)
        {
            hn[0, 0, i, 0] = 0;
            cn[0, 0, i, 0] = 0;
        }
        first_output = true;
        frame = 0;
    }

    void Release()
    {
        if (eye_callback_registered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GetData));
            eye_callback_registered = false;
        }
    }
}

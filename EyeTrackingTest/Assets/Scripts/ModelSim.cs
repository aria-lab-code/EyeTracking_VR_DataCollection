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

public class ModelSim : MonoBehaviour
{
    private static int frame;
    public NNModel modelAsset;
    public static bool useModel;
    public bool use_model;
    private static IWorker worker;
    private static bool first_output;
    public bool use_quadrant;
    public static bool useQuadrant;

    public static int playerID;
    private static Model m_RuntimeModel;
    private static Transform player;

    
    private static long MeasureTime;
    private static Vector3 gaze_direct_L, gaze_direct_R, forward;
    private static Tensor input, hn, cn;

    #region "GUI interactions"
    public TextMesh breakMessage, countdownMessage;
    public Canvas BreakCanvas, CountdownCanvas;
    private static GameObject GazeObject1, GazeObject2, GazeObject3, TrackObjectLine, TrackObjectArc;
    private bool continueClicked;
    #endregion

    public EyeParameter eye_parameter = new EyeParameter();
    public GazeRayParameter gaze = new GazeRayParameter();
    private static EyeData_v2 eyeData = new EyeData_v2();
    private static bool eye_callback_registered = false;

    private bool firstFrame;
    private bool rapidTesting;

    public static bool testing;

    private const int SHORT_BREAK = 5;
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
        input = new Tensor(1, 1, 9, 1);
        first_output = true;
        rapidTesting = false;
        testing = false;

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
        continueClicked = false;
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
        continueClicked = true;
    }
    
    // Update is called once per frame
    void Update()
    {
        forward = player.forward;

        if (useModel)
        {
            ModelCall();
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

    private static void ModelCall()
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
        if (testing)
        {
            player.rotation = Quaternion.Slerp(player.rotation, rotation, Time.deltaTime*5.0f);
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
            gaze_direct_L = eyeData.verbose_data.left.gaze_direction_normalized;
            gaze_direct_R = eyeData.verbose_data.right.gaze_direction_normalized;

            gaze_direct_L.x = gaze_direct_L.x * -1;
            gaze_direct_R.x = gaze_direct_R.x * -1;

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
        GazeObject1.SetActive(true);
        GazeObject2.SetActive(true);
        GazeObject3.SetActive(true);
        rapidTesting = true;
        testing = true;
        ResetModel();
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < 60)
        {
            //StartCoroutine(UpdateGazeObjects());
            yield return null;
        }
        rapidTesting = false;
        testing = false;
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        ResetHead();
        Release();
    }

    private IEnumerator LinearPursuit()
    {
        TrackObjectLine.SetActive(true);
        testing = true;
        ResetModel();
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < 60)
        {
            yield return null;
        }
        testing = false;
        TrackObjectLine.SetActive(false);
        ResetHead();
        Release();
    }

    private IEnumerator ArcPursuit()
    {
        TrackObjectArc.SetActive(true);
        testing = true;
        ResetModel();
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < 60)
        {
            yield return null;
        }
        testing = false;
        TrackObjectArc.SetActive(false);
        ResetHead();
        Release();
    }

    private void ResetModel()
    {
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
        hn = new Tensor(1, 1, 9, 1);
        cn = new Tensor(1, 1, 9, 1);
        input = new Tensor(1, 1, 9, 1);
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

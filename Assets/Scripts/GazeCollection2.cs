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
using System.Collections.Generic;
using TMPro;
using System.Linq;

public class GazeCollection2 : MonoBehaviour
{
    #region "file ID information"
    /// <summary>
    /// Define user ID information. The developers can define the user ID format such as "ABC_001".
    /// The ID is used for the name of the text file that records the measured eye movement data.
    /// </summary>
    public string UserIDNum;       // Display the UserID in the 
    public string Path;             // TODO: Change this to the path to the SSD directory
    private static string UserID;
    private static int trialNum;
    private static string File_Path;
    private static string Record_name = "ScoreRecord" + ".txt";
    #endregion

    #region "GUI interactions"
    public TextMesh breakMessage2, countdownMessage2;
    public Canvas BreakCanvas2, CountdownCanvas2;
    private static GameObject GazeObject1, GazeObject2, GazeObject3, TrackObjectLine, TrackObjectArc;
    private static GameObject GazeObject4, GazeObject5, GazeObject6, AvoidObject1, AvoidObject2, AvoidObject3;
    private bool continueClicked;
    private bool calibrationClicked;
    public List<GameObject> GazeObjs;
    public Button calibrationButton;
    public Button continueButton;

    //public TextMesh infoMessage;
    #endregion
    private const int randomSeed = 43;
    private bool firstFrame;

    private const int SHORT_BREAK = 5;
    private float gameTime;
    private static TestType Testing;
    private const float totalGameTime = 90;
    

    #region "EyeDataParameters"
    public EyeParameter eye_parameter = new EyeParameter();
    public GazeRayParameter gaze = new GazeRayParameter();
    private static EyeData_v2 eyeData = new EyeData_v2();
    private static bool eye_callback_registered = false;
    private const int maxframe_count = 120 * (int)totalGameTime;
    private static UInt64 eye_valid_L, eye_valid_R;
    private static Vector3 gaze_origin_L, gaze_origin_R, origin_L, origin_R;
    private static Vector3 gaze_direct_L, gaze_direct_R, direct_L, direct_R;
    private static double gaze_sensitive;
    private static Stopwatch timer = new Stopwatch();
    private static Vector3 forward;
    private static Quaternion rotation;

    #endregion

    public static int score = 0;
    public static int total_score = 0;
    private Dictionary<string, int> score_record = new Dictionary<string, int>();

    public List<GameObject> hands;


    /// <summary>
    /// Parameters for time-related information.
    /// </summary>
    public static int cnt_callback = 0;

    private static Matrix4x4 localToWorldTransform;

    private static long MeasureTime, CurrentTime, EndTime = 0, timeSpan;
    private static float time_stamp, sync_time_stamp;
    private static int frame;

    private const float mm_to_m = 1.0f / 1000.0f;

    private static Vector3 currentPos = Vector3.zero, currentVel = Vector3.zero;
    private bool calibrated = false;
    [Header("RayCastSetting")]
    public LayerMask RayCastLayers;

    public static void RemoveFirstLine(string path)
    {
        List<string> quotelist = File.ReadAllLines(path).ToList();
        quotelist.RemoveAt(0);
        using (StreamWriter sw = new StreamWriter(path, false))
        {
            foreach (string s in quotelist)
                sw.WriteLine(s);
        }
        //File.WriteAllLines(path, quotelist.ToArray());
    }

    /// <summary>
    /// Start is called before the first frame update. The Start() function is performed only one time.
    /// </summary>
    void Start()
    {
        Path = Directory.GetCurrentDirectory();
        File_Path = Path + "\\userIDList.txt";

        using (StreamReader sr = new StreamReader(File_Path))
        {
            UserID = sr.ReadLine().Trim();
        }
        trialNum = 0;
        UserIDNum = UserID;
        RemoveFirstLine(File_Path);

        timer.Start();
        SRanipal_Eye_Framework.Instance.EnableEyeDataCallback = true;
        Testing = TestType.None;
        frame = 0;

        continueClicked = false;
        calibrationClicked = false;
        forward = Camera.main.transform.forward;
        
        Invoke("SystemCheck", 0.5f);                // System check.
       
        
        calibrated = false; // Should be False at Test Time
        while(!calibrated)
            calibrated = SRanipal_Eye_v2.LaunchEyeCalibration();     // Perform calibration for eye tracking.


        firstFrame = true;
        GazeObject1 = GameObject.Find("Gaze Focusable Object 1");
        GazeObject2 = GameObject.Find("Gaze Focusable Object 2");
        GazeObject3 = GameObject.Find("Gaze Focusable Object 3");

        TrackObjectLine = GameObject.Find("Tracking Object 1");
        TrackObjectArc = GameObject.Find("Tracking Object 2");

        GazeObject4 = GameObject.Find("Gaze Focusable Object4");
        GazeObject5 = GameObject.Find("Gaze Focusable Object5");
        GazeObject6 = GameObject.Find("Gaze Focusable Object6");
        AvoidObject1 = GameObject.Find("Gaze Avoid Object1");
        AvoidObject2 = GameObject.Find("Gaze Avoid Object2");
        AvoidObject3 = GameObject.Find("Gaze Avoid Object3");

        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);

        TrackObjectLine.SetActive(false);
        TrackObjectArc.SetActive(false);

        GazeObject4.SetActive(false);
        GazeObject5.SetActive(false);
        GazeObject6.SetActive(false);
        AvoidObject1.SetActive(false);
        AvoidObject2.SetActive(false);
        AvoidObject3.SetActive(false);
    }

    void RapidMovementObjectData_txt()
    {
        string variable =
        "time(100ns)" + "," +
        "time_stamp(ms)" + "," +
        "object1.x" + "," +
        "object1.y" + "," +
        "object1.z" + "," +
        "object2.x" + "," +
        "object2.y" + "," +
        "object2.z" + "," +
        "object3.x" + "," +
        "object3.y" + "," +
        "object3.z" + "," +
        Environment.NewLine;
        File.AppendAllText($"Object{UserID}_RapidMovement_{trialNum.ToString()}.txt", variable);
    }

    void LinearPursuitObjectData_txt()
    {
        string variable =
        "time(100ns)" + "," +
        "time_stamp(ms)" + "," +
        "object1.x" + "," +
        "object1.y" + "," +
        "object1.z" + "," +
        Environment.NewLine;
        File.AppendAllText($"Object{UserID}_LinearPursuit_{trialNum.ToString()}.txt", variable);
    }

    void ArcPursuitObjectData_txt()
    {
        string variable =
        "time(100ns)" + "," +
        "time_stamp(ms)" + "," +
        "object1.x" + "," +
        "object1.y" + "," +
        "object1.z" + "," +
        Environment.NewLine;
        File.AppendAllText($"Object{UserID}_ArcPursuit_{trialNum.ToString()}.txt", variable);
    }

    void AvoidMovementObjectData_txt()
    {
        string variable =
        "time(100ns)" + "," +
        "time_stamp(ms)" + "," +
        "object1.x" + "," +
        "object1.y" + "," +
        "object1.z" + "," +
        "object2.x" + "," +
        "object2.y" + "," +
        "object2.z" + "," +
        "object3.x" + "," +
        "object3.y" + "," +
        "object3.z" + "," +
        "AvoidObject1.x" + "," +
        "AvoidObject1.y" + "," +
        "AvoidObject1.z" + "," +
        "AvoidObject2.x" + "," +
        "AvoidObject2.y" + "," +
        "AvoidObject2.z" + "," +
        "AvoidObject3.x" + "," +
        "AvoidObject3.y" + "," +
        "AvoidObject3.z" + "," +
        Environment.NewLine;
        File.AppendAllText($"Object{UserID}_AvoidMovement_{trialNum.ToString()}.txt", variable);
    }

    void Data_txt()
    {
        string variable =
        "time(100ns)" + "," +
        "time_stamp(ms)" + "," +
        "frame" + "," +
        "gaze_direct_L.x" + "," +
        "gaze_direct_L.y" + "," +
        "gaze_direct_L.z" + "," +
        "gaze_direct_R.x" + "," +
        "gaze_direct_R.y" + "," +
        "gaze_direct_R.z" + "," +
        "forward.x" + "," +
        "forward.y" + "," +
        "forward.z" + "," +
        "rotation.w," +
        "rotation.x," +
        "rotation.y," +
        "rotation.z" +
        Environment.NewLine;

        File.AppendAllText("User" + UserID + "_" + Testing.ToString().Replace("Test", "") + "_" + trialNum.ToString() + ".txt", variable);
    }

    void Record_txt()
    {
        if (!File.Exists(Record_name))
        {
            string variable =
                "time(100ns)" + "," +
                "User_ID" + ",";
            foreach (string key in score_record.Keys)
            {
                variable += key + ",";
            }
            variable = variable.TrimEnd(',') + Environment.NewLine;
            File.AppendAllText(Record_name, variable);
        }
        MeasureTime = DateTime.Now.Ticks;
        string value =
            MeasureTime.ToString() + "," +
            UserID.ToString() + ",";

        foreach (int v in score_record.Values)
        {
            value += v.ToString() + ",";
        }
        value = value.TrimEnd(',') + Environment.NewLine;
        File.AppendAllText(Record_name, value);
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
    public void ContinueClicked2()
    {
        continueClicked = true;
    }

    public void CalibrationClicked()
    {
        calibrationClicked = true;
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

    void GetGazeRays(out RawGazeRays r, GazeIndex gazeIndex)
    {
        r = new RawGazeRays();
        if (eye_callback_registered)
        {
            SRanipal_Eye_v2.GetGazeRay(gazeIndex, out r.origin, out r.dir, eyeData);
        }
        else
        {
            SRanipal_Eye_v2.GetGazeRay(gazeIndex, out r.origin, out r.dir);
        }
    }

    /// <summary>
    /// Update is called once per frame.
    /// The main purpose here now is to control the view of each frame
    /// </summary>
    void Update()
    {
        frame++;
        localToWorldTransform = Camera.main.transform.localToWorldMatrix;
        forward = Vector3.Scale(Camera.main.transform.forward, new Vector3(-1, 1, 1));
        rotation = Camera.main.transform.rotation;
        RawGazeRays localGazeRays;
        GetGazeRays(out localGazeRays, GazeIndex.COMBINE);
        RawGazeRays gazeRays = localGazeRays.Absolute(Camera.main.transform);

        Ray gaze = new Ray(gazeRays.origin, gazeRays.dir);
        RaycastHit hit;

        if (Testing == TestType.RapidMovementTest)
        {
            if (!BreakCanvas2.enabled)
            {
                RapidMovementObjectCallback();
            }
            if (Physics.Raycast(gaze, out hit, 999f, RayCastLayers))
            {
                foreach (var obj in GazeObjs)
                {
                    obj.GetComponent<HighlightAtGaze>()?.GazeFocusChanged(obj == hit.transform.gameObject);
                }
            }
        }
        else if (Testing == TestType.SmoothLinearTest)
        {
            total_score++;
            if (!BreakCanvas2.enabled)
            {
                LinearPursuitObjectCallback();
            }
            if (Physics.Raycast(gaze, out hit, 999f, RayCastLayers))
            {
                if (hit.transform.gameObject.CompareTag("GazeObject1"))
                {
                    TrackObjectLine.GetComponent<SmoothPursuitLinear>().GazeFocusChanged(true);
                    score++;
                }
                else
                {
                    TrackObjectLine.GetComponent<SmoothPursuitLinear>().GazeFocusChanged(false);
                }
            }
            else
            {
                TrackObjectLine.GetComponent<SmoothPursuitLinear>().GazeFocusChanged(false);
            }
        }
        else if (Testing == TestType.SmoothArcTest)
        {
            total_score++;
            if (!BreakCanvas2.enabled)
            {
                ArcPursuitObjectCallback();
            }
            if (Physics.Raycast(gaze, out hit, 999f, RayCastLayers))
            {
                if (hit.transform.gameObject.CompareTag("GazeObject1"))
                {
                    TrackObjectArc.GetComponent<SmoothPursuitArc>().GazeFocusChanged(true);
                    score++;
                }
                else
                {
                    TrackObjectArc.GetComponent<SmoothPursuitArc>().GazeFocusChanged(false);
                }
            }
            else
            {
                TrackObjectArc.GetComponent<SmoothPursuitArc>().GazeFocusChanged(false);
            }
        }
        else if (Testing == TestType.RapidAvoidTest)
        {
            if (!BreakCanvas2.enabled)
            {
                AvoidMovementObjectCallback();
            }
            if (Physics.Raycast(gaze, out hit, 999f, RayCastLayers))
            {
                if (hit.transform.gameObject.CompareTag("AvoidObject1"))
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(true);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("AvoidObject2"))
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(true);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("AvoidObject3"))
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(true);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("GazeObject4"))
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(true);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("GazeObject5"))
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(true);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                }
                else if (hit.transform.gameObject.CompareTag("GazeObject6"))
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(true);
                }
                else
                {
                    AvoidObject1.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject2.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    AvoidObject3.GetComponent<AvoidObstacleTest>().GazeFocusChanged(false);
                    GazeObject4.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject5.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
                    GazeObject6.GetComponent<HighlightAtGaze>().GazeFocusChanged(false);
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
    /// The Callback functions is to record the data(gaze/forward data, object data) to the txt file.
    /// </summary>
    #region Callback

    void RapidMovementObjectCallback()
    {
        MeasureTime = DateTime.Now.Ticks;
        timeSpan = timer.ElapsedMilliseconds;
        time_stamp = eyeData.timestamp;
        string[] obj1Position = {GazeObject1.transform.position.x.ToString(),
                                 GazeObject1.transform.position.y.ToString(),
                                 GazeObject1.transform.position.z.ToString() 
                                 };
        string[] obj2Position ={GazeObject2.transform.position.x.ToString(),
                                 GazeObject2.transform.position.y.ToString(),
                                 GazeObject2.transform.position.z.ToString() };
        string[] obj3Position = {GazeObject3.transform.position.x.ToString(),
                                 GazeObject3.transform.position.y.ToString(),
                                 GazeObject3.transform.position.z.ToString() };
        string value =
MeasureTime.ToString() + "," +
timeSpan.ToString() + "," +
obj1Position[0] + "," +
obj1Position[1] + "," +
obj1Position[2] + "," +
obj2Position[0] + "," +
obj2Position[1] + "," +
obj2Position[2] + "," + 
obj3Position[0] + "," +
obj3Position[1] + "," +
obj3Position[2] + "," +

Environment.NewLine;
        File.AppendAllText($"Object{UserID}_RapidMovement_{trialNum.ToString()}.txt", value);
    }
    void AvoidMovementObjectCallback()
    {
        MeasureTime = DateTime.Now.Ticks;
        timeSpan = timer.ElapsedMilliseconds;
        time_stamp = eyeData.timestamp;
        string[] obj1Position = {GazeObject1.transform.position.x.ToString(),
                                 GazeObject1.transform.position.y.ToString(),
                                 GazeObject1.transform.position.z.ToString() };
        string[] obj2Position ={GazeObject2.transform.position.x.ToString(),
                                 GazeObject2.transform.position.y.ToString(),
                                 GazeObject2.transform.position.z.ToString() };
        string[] obj3Position = {GazeObject3.transform.position.x.ToString(),
                                 GazeObject3.transform.position.y.ToString(),
                                 GazeObject3.transform.position.z.ToString() };
        string[] obj4Position = {AvoidObject1.transform.position.x.ToString(),
                                 AvoidObject1.transform.position.y.ToString(),
                                 AvoidObject1.transform.position.z.ToString() };
        string[] obj5Position = {AvoidObject2.transform.position.x.ToString(),
                                 AvoidObject2.transform.position.y.ToString(),
                                 AvoidObject2.transform.position.z.ToString() };
        string[] obj6Position = {AvoidObject3.transform.position.x.ToString(),
                                 AvoidObject3.transform.position.y.ToString(),
                                 AvoidObject3.transform.position.z.ToString() };
        string value =
MeasureTime.ToString() + "," +
timeSpan.ToString() + "," +
obj1Position[0] + "," +
obj1Position[1] + "," +
obj1Position[2] + "," +
obj2Position[0] + "," +
obj2Position[1] + "," +
obj2Position[2] + "," +
obj3Position[0] + "," +
obj3Position[1] + "," +
obj3Position[2] + "," +
obj4Position[0] + "," +
obj4Position[1] + "," +
obj4Position[2] + "," +
obj5Position[0] + "," +
obj5Position[1] + "," +
obj5Position[2] + "," +
obj6Position[0] + "," +
obj6Position[1] + "," +
obj6Position[2] + "," +
Environment.NewLine;
        File.AppendAllText($"Object{UserID}_AvoidMovement_{trialNum}.txt", value);
    }
    void LinearPursuitObjectCallback()
    {
        MeasureTime = DateTime.Now.Ticks;
        timeSpan = timer.ElapsedMilliseconds;
        time_stamp = eyeData.timestamp;
        string[] obj1Position = {TrackObjectLine.transform.position.x.ToString(),
                                 TrackObjectLine.transform.position.y.ToString(),
                                 TrackObjectLine.transform.position.z.ToString() };
        string value =
MeasureTime.ToString() + "," +
timeSpan.ToString() + "," +
obj1Position[0] + "," +
obj1Position[1] + "," +
obj1Position[2] + "," +
Environment.NewLine;
        File.AppendAllText($"Object{UserID}_LinearPursuit_{trialNum}.txt", value);
    }
    void ArcPursuitObjectCallback()
    {
        MeasureTime = DateTime.Now.Ticks;
        timeSpan = timer.ElapsedMilliseconds;
        time_stamp = eyeData.timestamp;
        string[] obj1Position = {TrackObjectLine.transform.position.x.ToString(),
                                 TrackObjectLine.transform.position.y.ToString(),
                                 TrackObjectLine.transform.position.z.ToString() };
        string value =
MeasureTime.ToString() + "," +
timeSpan.ToString() + "," +
obj1Position[0] + "," +
obj1Position[1] + "," +
obj1Position[2] + "," +
Environment.NewLine;
        File.AppendAllText($"Object{UserID}_ArcPursuit_{trialNum}.txt", value);
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
        //if (error == ViveSR.Error.WORK)
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

            gaze_sensitive = eye_parameter.gaze_ray_parameter.sensitive_factor;
            
            string value =
                MeasureTime.ToString() + "," +
                timeSpan.ToString() + "," +
                frame.ToString() + "," +
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
                rotation.w.ToString() + "," +
                rotation.x.ToString() + "," +
                rotation.y.ToString() + "," +
                rotation.z.ToString() +
                Environment.NewLine;

            File.AppendAllText("User" + UserID + "_" + Testing.ToString().Replace("Test", "") + "_" + trialNum.ToString() + ".txt", value);

            cnt_callback++;
        }
        
    }
    #endregion
    /// <summary>
    /// Saccade task sequence.
    /// The function controll the test process, including the information message(breakMessage2)
    /// 
    /// </summary>
    private IEnumerator Sequence()
    {
        UnityEngine.Debug.Log("Sequence started");

        // DO NOT REMOVE THE PRIVACY STATEMENT, REQUIRED BY HTC 
        // Participants should see the paper version during the consent process https://docs.google.com/document/d/13ehQgG4bj30qM26owmaHe9gsbGhAz9uMMaSYZKIm2cA/edit?usp=sharing

        //****Introduction about the game****
        
        breakMessage2.text = "Welcome to the virtual environment.\n" +
                             "In this test, please remain stationary while you control\n" +
                             " your movements using only your neck and eyes.\n" +
                             "\n" +
                             "Please click <Continue> to start the practice section.";
        yield return StartCoroutine(DisplayBreakMenu());
        breakMessage2.text = "Before we commence the practice session, it's essential to\n" +
                             " familiarize yourself with the button functionality.\n" +
                             "Upon highlighting the button, you will notice a change in its color to light blue.\n" +
                             "During this time, you may click the button using the trigger button\n" +
                             " on the controller.\n" +
                             "Select and click the <Continue> button to initiate the practice.\n" +
                             "Should you accidentally click <Rest>, please be aware that you\n" +
                             " will be required to perform the calibration process again.";
        yield return StartCoroutine(DisplayCalibrationMenu());
        
        breakMessage2.text = "The blue cube is the focused cube. The player should try to follow\n" +
                             " the cube in the Linear Pursuit section and the Arc Pursuit Section.\n" +
                             "When the player looks at the cube, it will turn green.\n" +
                             "Please press <Continue> to start the practice.";
        yield return StartCoroutine(DisplayBreakMenu());
        breakMessage2.text = "You have at most 30 seconds to track the cube.\n" +
                             "Please try looking at the cube.\n" +
                             "Once you finish this practice, click <Continue>.";

        yield return StartCoroutine(DisplayFocus());
        breakMessage2.text = "In the Rapid Movement Section, the blue cubes will\n" +
                             " relocate when the player looks at it.\n" +
                             "Please click <Continue> to start the practice.";
        yield return StartCoroutine(DisplayBreakMenu());
        breakMessage2.text = "You have at most 30 seconds to search for the cubes.\n Please try to search the cubes\n" +
                             "Once you finish the practice, press <Continue> to read the following introduction.";
        yield return StartCoroutine(DisplayFocus2());
        breakMessage2.text = "In the Rapid Avoid Section, there are three yellow cubes, which represent noise.\n" +
                            "Please ignore the yellow cubes and search the blue cube.\n" +
                            "The yellow cubes will keep moving towards the player but never collide with the player.";
        yield return StartCoroutine(DisplayBreakMenu());
        breakMessage2.text = "Please try to search the blue cubes and ignore the yellow cubes.\n" +
                            "Once you finish the practice, press <Continue> to read the following introduction.";
        yield return StartCoroutine(DisplayAvoid());
        breakMessage2.text = "This concludes the guide. Please click <Continue> to start the game.";
        yield return StartCoroutine(DisplayBreakMenu());
        //****End of Introduction****


        UnityEngine.Random.seed = randomSeed;
        breakMessage2.text = "During the Linear Pursuit section, your goal is to track the cube along its designated path.\n" +
                             "As the player, you will participate in three rounds, with rest intervals between each round.\n" +
                             "Please click <Continue> to procee the test";
        yield return StartCoroutine(DisplayBreakMenu());
        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(LinearPursuit());
        score_record.Add($"linear_score_{trialNum}", score);
        score_record.Add($"linear_total_score_{trialNum}", total_score);
        breakMessage2.text = "Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());
        trialNum++;
        
        breakMessage2.text = "You have completed the first round of the Linear Pursuit Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n"+
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the next round of the Linear Pursuit section."; 
        yield return StartCoroutine(DisplayCalibrationMenu());
        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(LinearPursuit());
        score_record.Add($"linear_score_{trialNum}", score);
        score_record.Add($"linear_total_score_{trialNum}", total_score);
        breakMessage2.text = "Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());
        trialNum++;

        breakMessage2.text = "You have completed the second round of the Linear Pursuit Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the final round of the Linear Pursuit section.";
        yield return StartCoroutine(DisplayCalibrationMenu());
        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(LinearPursuit());
        score_record.Add($"linear_score_{trialNum}", score);
        score_record.Add($"linear_total_score_{trialNum}", total_score);
        breakMessage2.text = "Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());

        trialNum = 0;

        //****Arc Pursuit section(3 games)
        breakMessage2.text = "You have completed the Linear Pursuit Test!\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with Arc Pursuit section.";
        
        yield return StartCoroutine(DisplayCalibrationMenu());
        breakMessage2.text = "During the Arc Pursuit section, your goal is to track the cube along its designated path.\n" +
                             "As the player, you will participate in three rounds, with rest intervals between each round.\n" +
                             "Please click <Continue> to procee the test";
        yield return StartCoroutine(DisplayBreakMenu());
        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(ArcPursuit());

        score_record.Add($"arc_score_{trialNum}", score);
        score_record.Add($"arc_total_score_{trialNum}", total_score);
        breakMessage2.text = "Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());

        trialNum++;

        breakMessage2.text = "You have completed the first round of the Arc Pursuit Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the next round of the Arc Pursuit section.";

        yield return StartCoroutine(DisplayCalibrationMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(ArcPursuit());

        score_record.Add($"arc_score_{trialNum}", score);
        score_record.Add($"arc_total_score_{trialNum}", total_score);
        breakMessage2.text = "Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());
        trialNum++;

        breakMessage2.text = "You have completed the second round of the Arc Pursuit Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the final round of the Arc Pursuit section.";

        yield return StartCoroutine(DisplayCalibrationMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(ArcPursuit());

        score_record.Add($"arc_score_{trialNum}", score);
        score_record.Add($"arc_total_score_{trialNum}", total_score);
        breakMessage2.text = "Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());

        trialNum = 0;
        
        breakMessage2.text = "You have completed the Arc Pursuit Test!\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with Rapid Movement section.";
        yield return StartCoroutine(DisplayCalibrationMenu());
        breakMessage2.text = "For the Rapid Movement section of the test, three cubes will appear in different locations in front of you and start moving towards you.\n" +
                             "Look at the cubes to reset their movement before they reach you.\n" +
                             "As the player, you will participate in three rounds, with rest intervals between each round.\n" +
                             "Press <Continue> when you are ready to start.";
        yield return StartCoroutine(DisplayBreakMenu());
        
        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidMovementTest());
        score_record.Add($"rapid_score_{trialNum}", score);
        score_record.Add($"rapid_total_score_{trialNum}", total_score);
        breakMessage2.text = "Rapid Movement Test Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());

        trialNum++;
        
        breakMessage2.text = "You have completed the first round of the Rapid Movement Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the next round of the Rapid Movement section.";

        yield return StartCoroutine(DisplayCalibrationMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidMovementTest());

        score_record.Add($"rapid_score_{trialNum}", score);
        score_record.Add($"rapid_total_score_{trialNum}", total_score);
        breakMessage2.text = "Rapid Movement Test Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());
        trialNum++;


        breakMessage2.text = "You have completed the second round of the Rapid Movement Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the final round of the Rapid Movement section.";

        yield return StartCoroutine(DisplayCalibrationMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidMovementTest());

        score_record.Add($"rapid_score_{trialNum}", score);
        score_record.Add($"rapid_total_score_{trialNum}", total_score);
        breakMessage2.text = "Rapid Movement Test Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());

        trialNum = 0;
        breakMessage2.text = "You have completed the Rapid Movement Test!\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with Rapid Avoid section.";
        yield return StartCoroutine(DisplayCalibrationMenu());
        

        breakMessage2.text = "For the Rapid Avoid section of the test, six cubes will spawn in different locations in front of you and start moving towards you.\n" +
                             "Your goal is to look at the blue cubes to reset their movement before they reach you, while avoiding looking at the yellow cubes.\n" +
                             "As the player, you will participate in three rounds, with rest intervals between each round.\n" +
                             "Press <Continue> when you are ready to begin.";
        yield return StartCoroutine(DisplayBreakMenu());
        
        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidAvoidTest());
        score_record.Add($"avoid_score_{trialNum}", score);
        score_record.Add($"avoid_total_score_{trialNum}", total_score);
        breakMessage2.text = "Rapid Avoid Test Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());
        trialNum++;

        breakMessage2.text = "You have completed the first round of the Rapid Avoid Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the next round of the Rapid Avoid section.";

        yield return StartCoroutine(DisplayCalibrationMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidAvoidTest());

        score_record.Add($"avoid_score_{trialNum}", score);
        score_record.Add($"avoid_total_score_{trialNum}", total_score);
        breakMessage2.text = "Rapid Avoid Test Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());
        trialNum++;

        breakMessage2.text = "You have completed the second round of the Rapid Avoid Test.\n" +
                             "If you would like to take a break, you can click <Rest> and remove the headset now.\n" +
                             "When you're ready to resume, please click <Calibration> upon your return.\n" +
                             "Alternatively, you can click <Continue> to proceed with the final round of the Rapid Avoid section.";

        yield return StartCoroutine(DisplayCalibrationMenu());

        yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
        yield return StartCoroutine(RapidAvoidTest());

        score_record.Add($"avoid_score_{trialNum}", score);
        score_record.Add($"avoid_total_score_{trialNum}", total_score);
        breakMessage2.text = "Rapid Avoid Test Score:" + score.ToString() + "/" + total_score.ToString();
        yield return StartCoroutine(DisplayBreakMenu());

        yield return StartCoroutine(DisplayBreakMenu());
        breakMessage2.text = "Congratulations!\n" +
                             "You have successfully completed all the tests!\n" +
                             "Thank you for participating in our test!";
        Record_txt();
        yield return StartCoroutine(DisplayBreakMenu());

    }

    private IEnumerator DisplayFocus()
    {
        continueClicked = false;
        BreakCanvas2.enabled = true;
        BreakCanvas2.gameObject.SetActive(true);
        calibrationButton.gameObject.SetActive(false);
        TrackObjectLine.SetActive(true);
        Testing = TestType.SmoothLinearTest;
        gameTime = Time.time;
        while (!continueClicked && Time.time-gameTime<30)
        {
            yield return null;
        }

        Testing = TestType.None;
        TrackObjectLine.SetActive(false);
        BreakCanvas2.enabled = false;
        BreakCanvas2.gameObject.SetActive(false);
        continueClicked = false;
        Testing = TestType.None;
    }

    private IEnumerator DisplayFocus2()
    {
        continueClicked = false;
        BreakCanvas2.enabled = true;
        BreakCanvas2.gameObject.SetActive(true);
        calibrationButton.gameObject.SetActive(false);
        GazeObject1.SetActive(true);
        GazeObject2.SetActive(true);
        GazeObject3.SetActive(true);
        Testing = TestType.RapidMovementTest;
        gameTime = Time.time;
        while (!continueClicked && Time.time - gameTime < 30)
        {
            yield return null;
        }

        Testing = TestType.None;
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        BreakCanvas2.enabled = false;
        BreakCanvas2.gameObject.SetActive(false);
        continueClicked = false;
        Testing = TestType.None;
    }
    private IEnumerator DisplayAvoid()
    {
        continueClicked = false;
        BreakCanvas2.enabled = true;
        BreakCanvas2.gameObject.SetActive(true);
        calibrationButton.gameObject.SetActive(false);
        GazeObject4.SetActive(true);
        GazeObject5.SetActive(true);
        GazeObject6.SetActive(true);
        AvoidObject1.SetActive(true);
        AvoidObject2.SetActive(true);
        AvoidObject3.SetActive(true);
        Testing = TestType.RapidAvoidTest;
        gameTime = Time.time;
        while (!continueClicked && Time.time - gameTime < 30)
        {
            yield return null;
        }

        Testing = TestType.None;
        GazeObject4.SetActive(false);
        GazeObject5.SetActive(false);
        GazeObject6.SetActive(false);
        AvoidObject1.SetActive(false);
        AvoidObject2.SetActive(false);
        AvoidObject3.SetActive(false);
        BreakCanvas2.enabled = false;
        BreakCanvas2.gameObject.SetActive(false);
        continueClicked = false;
        Testing = TestType.None;
    }
    /// <summary>
    /// Suspends the game until the continue button is pressed.
    /// This can be used to give the user a break between sections where the data gathered can be ignored.
    /// </summary>
    private IEnumerator DisplayBreakMenu()
    {
        UnityEngine.Debug.Log("Enter Display break menu");
        continueClicked = false;
        BreakCanvas2.enabled = true;
        BreakCanvas2.gameObject.SetActive(true);

        calibrationButton.gameObject.SetActive(false);
        UnityEngine.Debug.Log($"continueClicked:{continueClicked}");
        while (!continueClicked)
        {
            //UnityEngine.Debug.Log($"continueClicked_inwhile:{continueClicked}");
            yield return null;
        }
        
        //UnityEngine.Debug.Log($"continueClicked_outwhile:{continueClicked}");
        BreakCanvas2.enabled = false;
        BreakCanvas2.gameObject.SetActive(false);
        continueClicked = false;
    }

    /// <summary>
    /// Suspends the game until the continue button is pressed.
    /// This can be used to give the user a break between sections where the data gathered can be ignored.
    /// </summary>
    private IEnumerator DisplayCalibrationMenu()
    {
        UnityEngine.Debug.Log("Enter Dispaly rest menu");
        calibrationButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "Rest";
        continueClicked = false;
        calibrationClicked = false;
        BreakCanvas2.enabled = true;
        BreakCanvas2.gameObject.SetActive(true);
        calibrationButton.gameObject.SetActive(true);
        while (!calibrationClicked && !continueClicked)
        {
            yield return null;
        }
        if (calibrationClicked)
        {
            continueClicked = false;
            calibrationClicked = false;
            calibrationButton.gameObject.GetComponentInChildren<TextMeshProUGUI>().text = "Caliboration";
            calibrated = false;
            breakMessage2.text = "When you are ready to proceed with the game, kindly select the <Calibration> button.";
            continueButton.gameObject.SetActive(false);
            continueButton.enabled = false;
            
            while (!calibrationClicked)
                yield return null;
            while (!calibrated)
                calibrated = SRanipal_Eye_v2.LaunchEyeCalibration();
            calibrated = true;
            breakMessage2.text = "Please click <Continue> to proceed with the game.";
            continueButton.gameObject.SetActive(true);
            continueButton.enabled = true;
            calibrationButton.gameObject.SetActive(false);
            while (!continueClicked)
            {
                yield return null;
            }
        }
        //UnityEngine.Debug.Log($"continueClicked_outwhile:{continueClicked}");
        BreakCanvas2.enabled = false;
        BreakCanvas2.gameObject.SetActive(false);
        calibrationButton.gameObject.SetActive(false);
        continueClicked = false;
        calibrationClicked = false;
    }

    /// <summary>
    /// Displays a countdown timer for the user so they can prepare for the next task.
    /// </summary>
    /// <param name="duration">duration of the countdown in seconds</param>
    /// <param name="message">a message to precede each number during the countdown</param>
    private IEnumerator DisplayCountdown(int duration, string message)
    {
        CountdownCanvas2.gameObject.SetActive(true);
        for (int i = duration; i > 0; i--)
        {
            countdownMessage2.text = message + i.ToString();
            yield return new WaitForSeconds(1);
        }

        countdownMessage2.text = "";
        CountdownCanvas2.gameObject.SetActive(false);
    }

    private IEnumerator RapidMovementTest()
    {
        EnableHand(false);
        GazeObject1.SetActive(true);
        GazeObject2.SetActive(true);
        GazeObject3.SetActive(true);
        cnt_callback = 0;
        RapidMovementObjectData_txt();
        Testing = TestType.RapidMovementTest;

        total_score = score = 0;
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < totalGameTime)
        {
            //StartCoroutine(UpdateGazeObjects());
            if (continueClicked)
            {
                var global_angle = Vector3.Angle(forward, new Vector3(0, 0, 1));
                breakMessage2.text = $"global angle:{global_angle}, (x,y,z)={forward.x}, {forward.y}, {forward.z}";
                BreakCanvas2.enabled = true;
                BreakCanvas2.gameObject.SetActive(true);
            }
            else
            {
                BreakCanvas2.enabled = false;
                BreakCanvas2.gameObject.SetActive(false);
            }
            yield return null;
        }
        Testing = TestType.None;
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);

        EnableHand(true);
        Release();
    }

    private IEnumerator LinearPursuit()
    {
        EnableHand(false);
        TrackObjectLine.SetActive(true);
        
        cnt_callback = 0;
        LinearPursuitObjectData_txt();
        score = total_score = 0;
        Testing = TestType.SmoothLinearTest;
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < totalGameTime)
        {
            if (continueClicked)
            {
                var global_angle = Vector3.Angle(forward, new Vector3(0, 0, 1));
                breakMessage2.text = $"global angle:{global_angle}, (x,y,z)={forward.x}, {forward.y}, {forward.z}";
                
                BreakCanvas2.enabled = true;
                BreakCanvas2.gameObject.SetActive(true);
            }
            else
            {
                BreakCanvas2.enabled = false;
                BreakCanvas2.gameObject.SetActive(false);
            }
            yield return null;
        }

        Testing = TestType.None;
        TrackObjectLine.SetActive(false);

        EnableHand(true);
        Release();
    }

    private IEnumerator ArcPursuit()
    {
        EnableHand(false);
        TrackObjectArc.SetActive(true);
        cnt_callback = 0;
        ArcPursuitObjectData_txt();
        score = total_score = 0;
        Testing = TestType.SmoothArcTest;
        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < totalGameTime)
        {
            if (continueClicked)
            {
                var global_angle = Vector3.Angle(forward, new Vector3(0, 0, 1));
                breakMessage2.text = $"global angle:{global_angle}, (x,y,z)={forward.x}, {forward.y}, {forward.z}";
                BreakCanvas2.enabled = true;
                BreakCanvas2.gameObject.SetActive(true);
            }
            else
            {
                BreakCanvas2.enabled = false;
                BreakCanvas2.gameObject.SetActive(false);
            }
            yield return null;
        }
        Testing = TestType.None;
        TrackObjectArc.SetActive(false);
        EnableHand(true);
        Release();
    }

    private IEnumerator RapidAvoidTest()
    {
        EnableHand(false);
        GazeObject4.SetActive(true);
        GazeObject5.SetActive(true);
        GazeObject6.SetActive(true);
        AvoidObject1.SetActive(true);
        AvoidObject2.SetActive(true);
        AvoidObject3.SetActive(true);
        cnt_callback = 0;
        score = total_score = 0;
        AvoidMovementObjectData_txt();
        Testing = TestType.RapidAvoidTest;

        Invoke("Measurement", 0f);
        gameTime = Time.time;
        while (Time.time - gameTime < totalGameTime)
        {
            yield return null;
        }
        Testing = TestType.None;
        GazeObject4.SetActive(false);
        GazeObject5.SetActive(false);
        GazeObject6.SetActive(false);
        AvoidObject1.SetActive(false);
        AvoidObject2.SetActive(false);
        AvoidObject3.SetActive(false);

        EnableHand(true);
        Release();
    }
    void EnableHand(bool enable)
    {
        foreach( GameObject h in hands)
        {
            h.SetActive(enable);
        }
    }

    public enum TestType
    {
        None,
        RapidMovementTest,
        SmoothLinearTest,
        SmoothArcTest,
        RapidAvoidTest
    }
}


using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Unity.Barracuda;
using UnityEngine;
using UnityEngine.SceneManagement;
using ViveSR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;

public class ModelSim : MonoBehaviour
{
    public enum ModelType
    {
        None,
        BaselineQuadrant,
        BaselineVector,
        LSTM,
        MLP
    }

    public enum TestType
    {
        None,
        SmoothLinearTest,
        SmoothArcTest,
        RapidMovementTest,
    }

    private static int _playerID;
    private static Transform _player;
    private Quaternion _playerRotationStart;
    private Quaternion _playerLocalRotationStart;

    private ModelType _modelType;
    private readonly static ModelType[][] MODEL_TYPE_ORDERINGS = new ModelType[][]
    {
        new ModelType[] { ModelType.BaselineVector, ModelType.LSTM, ModelType.MLP },
        new ModelType[] { ModelType.BaselineVector, ModelType.MLP, ModelType.LSTM },
        new ModelType[] { ModelType.LSTM, ModelType.BaselineVector, ModelType.MLP },
        new ModelType[] { ModelType.LSTM, ModelType.MLP, ModelType.BaselineVector },
        new ModelType[] { ModelType.MLP, ModelType.BaselineVector, ModelType.LSTM },
        new ModelType[] { ModelType.MLP, ModelType.LSTM, ModelType.BaselineVector }
    };
    private ModelType[] _modelTypeOrdering;
    private readonly static string[] MODEL_ALIASES = { "A", "B", "C" };

    public NNModel ModelAssetLSTM;
    private static Model _modelLSTM;
    private static Tensor _tensorLSTMInput, _tensorLSTMHidden, _tensorLSTMContext;
    private static IWorker _workerLSTM; // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html

    public NNModel ModelAssetMLP;
    private static Model _modelMLP;
    private static Tensor _tensorMLPInput;
    private static IWorker _workerMLP;

    private static Vector3 _vecGazeL, _vecGazeR, _vecForward;
    private static List<Vector3> _bufferGazeL, _bufferGazeR, _bufferForward;  // Create a buffer to hold a small history of states
    private const int BUFFER_CAPACITY = 3;

    #region "GUI interactions"
    public GameObject GazeObject1, GazeObject2, GazeObject3, TrackObjectLine, TrackObjectArc;
    public Canvas BreakCanvas, CountdownCanvas;
    public TextMesh BreakMessage, CountdownMessage;
    private bool _continueClicked;
    #endregion

    private EyeParameter _eyeParameter = new EyeParameter();
    private GazeRayParameter _gaze = new GazeRayParameter();
    private static EyeData_v2 _eyeData = new EyeData_v2();
    private static bool _eyeCallbackRegistered = false;

    private bool _firstFrame;
    private TestType _testType;

    private const int SECONDS_PER_TRIAL = 10;
    private const int SHORT_BREAK = 5;
    private float _gameTime;

    public bool DoCalibrateAtStart;

    // Start is called before the first frame update
    void Start()
    {
        Invoke(nameof(SystemCheck), 0.5f);                // System check.
        if (DoCalibrateAtStart)
        {
            SRanipal_Eye_v2.LaunchEyeCalibration();
        }
        SRanipal_Eye_Framework.Instance.EnableEyeDataCallback = true;

        _player = Camera.main.transform.parent.parent;
        _playerRotationStart = _player.rotation;
        _playerLocalRotationStart = _player.localRotation;

        _modelLSTM = ModelLoader.Load(ModelAssetLSTM);
        _workerLSTM = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, _modelLSTM);
        _tensorLSTMHidden = new Tensor(1, 1, 9, 1);
        _tensorLSTMContext = new Tensor(1, 1, 9, 1);
        _tensorLSTMInput = new Tensor(1, 1, 9, 1);

        _modelMLP = ModelLoader.Load(ModelAssetMLP);
        _workerMLP = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, _modelMLP);
        _tensorMLPInput = new Tensor(1, 1, 27, 1);

        //first_output = true;
        _modelType = ModelType.None;
        _testType = TestType.None;
        DisableHeadTracking.Disable = false;

        System.Random rng = new System.Random();
        int orderingIndex = rng.Next() % 6;
        UnityEngine.Debug.Log("orderingIndex: " + orderingIndex); // TODO: Persist to .csv.
        _modelTypeOrdering = MODEL_TYPE_ORDERINGS[orderingIndex];

        // Create buffers for storing the history of gaze vectors.
        _bufferGazeL = new List<Vector3>();
        _bufferGazeR = new List<Vector3>();
        _bufferForward = new List<Vector3>();

        _firstFrame = true;

        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        TrackObjectLine.SetActive(false);
        TrackObjectArc.SetActive(false);
        _continueClicked = false;
    }

    /// <summary>
    /// Check if the system works properly.
    /// </summary>
    void SystemCheck()
    {
        if (SRanipal_Eye_API.GetEyeData_v2(ref _eyeData) == ViveSR.Error.WORK)
        {
            UnityEngine.Debug.Log("Device is working properly.");
        }

        if (SRanipal_Eye_API.GetEyeParameter(ref _eyeParameter) == ViveSR.Error.WORK)
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
        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && !_eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GetGazeData));
            _eyeCallbackRegistered = true;
        }
        else if (!SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && _eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GetGazeData));
            _eyeCallbackRegistered = false;
        }
    }

    void Release()
    {
        if (_eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GetGazeData));
            _eyeCallbackRegistered = false;
        }
    }

    /// <summary>
    /// Callback function to record the eye movement data.
    /// Note that SRanipal_Eye_v2 does not work in the function below. It only works under UnityEngine.
    /// </summary>
    private static void GetGazeData(ref EyeData_v2 eye_data)
    {
        EyeParameter eye_parameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);
        _eyeData = eye_data;

        Error error = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
        if (error == ViveSR.Error.WORK)
        {
            SetGazeVectors(_eyeData.verbose_data.left.gaze_direction_normalized, _eyeData.verbose_data.right.gaze_direction_normalized);
        }
    }

    private static void SetGazeVectors(Vector3 gazeL, Vector3 gazeR)
    {
        gazeL.x *= -1;
        gazeR.x *= -1;

        _vecGazeL = gazeL;
        _vecGazeR = gazeR;

        _bufferGazeL.Add(_vecGazeL);
        if (_bufferGazeL.Count > BUFFER_CAPACITY)
        {
            _bufferGazeL.RemoveAt(0);
        }

        _bufferGazeR.Add(_vecGazeR);
        if (_bufferGazeR.Count > BUFFER_CAPACITY)
        {
            _bufferGazeR.RemoveAt(0);
        }
    }

    private static void SetForwardVector(Vector3 forwardVec)
    {
        _vecForward = forwardVec;

        //_bufferForward.Add(_vecGazeR);
        _bufferForward.Add(_vecForward);
        if (_bufferForward.Count > BUFFER_CAPACITY)
        {
            _bufferForward.RemoveAt(0);
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
        
        SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out r.origin, out r.dir, _eyeData);
    }

    /// <summary>
    /// Changes the flag to indicate that one of the menu continue buttons has been clicked.
    /// </summary>
    public void ContinueClicked()
    {
        _continueClicked = true;
    }
    
    // Update is called once per frame
    void Update()
    {
        SetForwardVector(_player.forward);

        switch (_modelType)
        {
            case ModelType.BaselineQuadrant:
                QuadrantBaseline();
                break;
            case ModelType.BaselineVector:
                VectorBaseline();
                break;
            case ModelType.LSTM:
                ModelCallLSTM();
                break;
            case ModelType.MLP:
                ModelCallMLP();
                break;
        }

        RawGazeRays localGazeRays;
        GetGazeRays(out localGazeRays);
        RawGazeRays gazeRays = localGazeRays.Absolute(Camera.main.transform);

        Ray gaze = new Ray(gazeRays.origin, gazeRays.dir);
        RaycastHit hit;
        if (_testType == TestType.SmoothLinearTest)
        {
            bool didHit = Physics.Raycast(gaze, out hit) && hit.transform.gameObject == TrackObjectLine;
            TrackObjectLine.GetComponent<SmoothPursuitLinear>().GazeFocusChanged(didHit);
        }
        else if (_testType == TestType.SmoothArcTest)
        {
            bool didHit = Physics.Raycast(gaze, out hit) && hit.transform.gameObject == TrackObjectArc;
            TrackObjectArc.GetComponent<SmoothPursuitArc>().GazeFocusChanged(didHit);
        }
        else if (_testType == TestType.RapidMovementTest)
        {
            GameObject[] gazeObjects = { GazeObject1, GazeObject2, GazeObject3 };
            if (Physics.Raycast(gaze, out hit))
            {
                foreach (GameObject gazeObject in gazeObjects)
                {
                    gazeObject.GetComponent<HighlightAtGaze>().GazeFocusChanged(hit.transform.gameObject == gazeObject);
                }
            }
        }

        if (_firstFrame)
        {
            StartCoroutine(Sequence());
            _firstFrame = false;
        }
    }

    /// <summary>
    /// Rotate the player object by finding vector between current forward direction and eye gaze
    /// direction. Rotate in direction of this vector.
    /// </summary>
    private void VectorBaseline()
    {
        // Compute

        float angle_boundary = 5.0f;  //boundary of eye angle
        float rotate_speed = 4f;  //each rotate angle

        // eye angle in x direction > angle_boundry : rotate the 
        Vector3 gaze_direct_avg_world = _player.rotation * (_vecGazeL + _vecGazeR).normalized;

        var angle = Vector3.Angle(gaze_direct_avg_world, _vecForward);
        var global_angle = Vector3.Angle(gaze_direct_avg_world, new Vector3(0, 0, 1));
        if ((angle > angle_boundary || angle < -1 * angle_boundary) && (global_angle < 70f && global_angle > -70f))
        {
            _player.rotation = Quaternion.Slerp(_player.rotation, Quaternion.LookRotation(gaze_direct_avg_world), Time.deltaTime * rotate_speed);
        }

        //var obj_weight = 10 / _player.rotation.z;
        //if (_player.rotation.z == 0)
        //{
        //    obj_weight = 0;
        //}
    }

    private void QuadrantBaseline()
    {
        float angle_boundary = 5.0f;
        float rotate_speed = 0.5f;

        Vector3 gaze_direct_avg_world = _player.rotation * (_vecGazeL + _vecGazeR).normalized;

        Vector3 gaze_direct = (_vecGazeL + _vecGazeR).normalized;

        var angle = Vector3.Angle(gaze_direct_avg_world, _vecForward);
        var global_angle = Vector3.Angle(gaze_direct_avg_world, new Vector3(0, 0, 1));

        //UnityEngine.Debug.Log(global_angle);
        if ((angle > angle_boundary || angle < -1 * angle_boundary) && (global_angle < 70f && global_angle > -70f))
        {
            print(gaze_direct_avg_world);

            if (gaze_direct.x < gaze_direct.y)
            {

                if (gaze_direct.x > -1 * gaze_direct.y)
                {
                    _player.Rotate(-rotate_speed, 0f, 0f);
                    //player.rotation = Quaternion.Slerp(player.rotation, up, Time.deltaTime*rotate_speed);
                }
                else
                {
                    _player.Rotate(0, -rotate_speed, 0f, Space.World);
                    //player.rotation = Quaternion.Slerp(player.rotation, left, Time.deltaTime*rotate_speed);
                }
            }
            else
            {
                if (gaze_direct.x > -1 * gaze_direct.y)
                {
                    _player.Rotate(0f, rotate_speed, 0f, Space.World);
                }
                else
                {
                    _player.Rotate(rotate_speed, 0f, 0f);
                    //player.rotation = Quaternion.Slerp(player.rotation, down, Time.deltaTime*rotate_speed);
                }
            }
        }
    }

    private static void ModelCallLSTM()
    {
        for (int i = 0; i < 3; i++)
        {
            _tensorLSTMInput[0, 0, i, 0] = _vecGazeL[i];
            _tensorLSTMInput[0, 0, i+3, 0] = _vecGazeR[i];
            _tensorLSTMInput[0, 0, i+6, 0] = _vecForward[i];
        }
        
        var Inputs = new Dictionary<string, Tensor>() {
            {"input", _tensorLSTMInput},
            {"h0", _tensorLSTMHidden},
            {"c0", _tensorLSTMContext}
        };

        _workerLSTM.Execute(Inputs);
        Tensor output = _workerLSTM.PeekOutput("output");
        _tensorLSTMHidden = _workerLSTM.PeekOutput("hn");
        _tensorLSTMContext = _workerLSTM.PeekOutput("cn");
        var new_forward = new Vector3(output[0,0,0,0]-0.05f, output[0,0,0,1], output[0,0,0,2]).normalized;

        Quaternion rotation = Quaternion.LookRotation(new_forward);
        if (DisableHeadTracking.Disable)
        {
            _player.rotation = Quaternion.Slerp(_player.rotation, rotation, Time.deltaTime * 5.0f);
        }
    }

    private static void ModelCallMLP()
    {
        const int CONTEXT_SIZE = 3;
        const int DATA_PER_TIMESTEP = 9;

        if (_bufferGazeL.Count < CONTEXT_SIZE)
        {
            return;
        }

        // Build the input vector
        for (int c = 0; c < CONTEXT_SIZE; c++)
        {
            var ind = c * DATA_PER_TIMESTEP;

            Vector3 l_c = _bufferGazeL[c];
            Vector3 r_c = _bufferGazeR[c];
            Vector3 f_c = _bufferForward[c];

            _tensorMLPInput[0, 0, ind, 0] = l_c.x;
            _tensorMLPInput[0, 0, ind + 1, 0] = l_c.y;
            _tensorMLPInput[0, 0, ind + 2, 0] = l_c.z;
            _tensorMLPInput[0, 0, ind + 3, 0] = r_c.x;
            _tensorMLPInput[0, 0, ind + 4, 0] = r_c.y;
            _tensorMLPInput[0, 0, ind + 5, 0] = r_c.z;
            _tensorMLPInput[0, 0, ind + 6, 0] = f_c.x;
            _tensorMLPInput[0, 0, ind + 7, 0] = f_c.y;
            _tensorMLPInput[0, 0, ind + 8, 0] = f_c.z;
        }

        //_tensorMLPInput[0, 0, 0, 0] = 0.3640442f;
        //_tensorMLPInput[0, 0, 1, 0] = 0.2774048f;
        //_tensorMLPInput[0, 0, 2, 0] = 0.8890991f;
        //_tensorMLPInput[0, 0, 3, 0] = 0.3574524f;
        //_tensorMLPInput[0, 0, 4, 0] = 0.2452698f;
        //_tensorMLPInput[0, 0, 5, 0] = 0.9011383f;
        //_tensorMLPInput[0, 0, 6, 0] = 0.3591461f;
        //_tensorMLPInput[0, 0, 7, 0] = 0.2465515f;
        //_tensorMLPInput[0, 0, 8, 0] = 0.900116f;
        //_tensorMLPInput[0, 0, 9, 0] = 0.3615723f;
        //_tensorMLPInput[0, 0, 10, 0] = 0.2754974f;
        //_tensorMLPInput[0, 0, 11, 0] = 0.8907013f;
        //_tensorMLPInput[0, 0, 12, 0] = 0.3569946f;
        //_tensorMLPInput[0, 0, 13, 0] = 0.2446289f;
        //_tensorMLPInput[0, 0, 14, 0] = 0.9014893f;
        //_tensorMLPInput[0, 0, 15, 0] = 0.3574524f;
        //_tensorMLPInput[0, 0, 16, 0] = 0.2452698f;
        //_tensorMLPInput[0, 0, 17, 0] = 0.9011383f;
        //_tensorMLPInput[0, 0, 18, 0] = 0.3592377f;
        //_tensorMLPInput[0, 0, 19, 0] = 0.2737427f;
        //_tensorMLPInput[0, 0, 20, 0] = 0.8921814f;
        //_tensorMLPInput[0, 0, 21, 0] = 0.3565521f;
        //_tensorMLPInput[0, 0, 22, 0] = 0.2440643f;
        //_tensorMLPInput[0, 0, 23, 0] = 0.901825f;
        //_tensorMLPInput[0, 0, 24, 0] = 0.3565521f;
        //_tensorMLPInput[0, 0, 25, 0] = 0.2440643f;
        //_tensorMLPInput[0, 0, 26, 0] = 0.901825f;

        //string inputLog = "input:";
        //for (int i = 0; i < CONTEXT_SIZE * DATA_PER_TIMESTEP; i++)
        //{
        //    inputLog += " " + _tensorMLPInput[0, 0, i, 0];
        //}
        //UnityEngine.Debug.Log(inputLog);
        var Inputs = new Dictionary<string, Tensor>() {
            {"onnx::Gemm_0", _tensorMLPInput},
        };

        _workerMLP.Execute(Inputs);
        string outputLayerName = _modelMLP.outputs[0];
        Tensor output = _workerMLP.PeekOutput(outputLayerName);
        var forward = new Vector3(output[0, 0, 0, 0], output[0, 0, 0, 1], output[0, 0, 0, 2]);

        var new_forward = forward.normalized;

        Quaternion rotation = Quaternion.LookRotation(new_forward);
        if (DisableHeadTracking.Disable)
        {
            _player.rotation = Quaternion.Slerp(_player.rotation, rotation, Time.deltaTime * 75.0f);
        }
    }

    private void ResetHead()
    {
        //_player.rotation = Quaternion.LookRotation(new Vector3(0, 0, 1));
        _player.rotation = _playerRotationStart;
        _player.localRotation = _playerLocalRotationStart;
    }

    /// <summary>
    /// Saccade task sequence.
    /// </summary>
    private IEnumerator Sequence()
    {
        UnityEngine.Debug.Log("Sequence started");
        // DO NOT REMOVE THE PRIVACY STATEMENT, REQUIRED BY HTC 
        // Participants should see the paper version during the consent process https://docs.google.com/document/d/13ehQgG4bj30qM26owmaHe9gsbGhAz9uMMaSYZKIm2cA/edit?usp=sharing
        BreakMessage.text =
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
            "\n\nPress continue if you agree with the privacy statement and are ready to begin.";
        yield return StartCoroutine(DisplayBreakMenu());

        for (int i = 0; i < 4; i++)
        {
            if (i == 0)
            {
                BreakMessage.text =
                    "LINEAR PURSUIT\n" +
                    "\n" +
                    "Follow the floating cube. It will move around in straight lines.\n" +
                    "Look directly at the cube to change its color.\n" +
                    "\n" +
                    "When you are ready to begin the Linear Pursuit section of the test, press continue.";
            }
            else
            {
                BreakMessage.text =
                    "In this trial, you will not be able to move your head to look around.\n" +
                    " Our assistive technology will move the view based on your eye movements.\n" +
                    " We recommend keeping your head level and still. Move only your eyes.\n" +
                    "\n" +
                    "When you are ready to start Linear Pursuit\n" +
                    " using assistant '" + MODEL_ALIASES[i - 1] + "', press continue.";
            }
            yield return StartCoroutine(DisplayBreakMenu());
            yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
            _modelType = (i == 0) ? ModelType.None : _modelTypeOrdering[i - 1];
            yield return StartCoroutine(LinearPursuit());
            _modelType = ModelType.None;
        }

        BreakMessage.text =
            "Linear Pursuit Test Complete!";
        yield return StartCoroutine(DisplayBreakMenu());

        for (int i = 0; i < 4; i++)
        {
            if (i == 0)
            {
                BreakMessage.text =
                    "ARC PURSUIT\n" +
                    "\n" +
                    "Follow the floating cube. It will move around in curved paths.\n" +
                    "Look directly at the cube to change its color.\n" +
                    "\n" +
                    "You may move your head to look around.\n" +
                    "\n" +
                    "When you are ready to begin the Arc Pursuit section of the test, press continue.";
            }
            else
            {
                BreakMessage.text =
                    "In this trial, you will not be able to move your head to look around.\n" +
                    " Our assistive technology will move the view based on your eye movements.\n" +
                    " We recommend keeping your head level and still. Move only your eyes.\n" +
                    "\n" +
                    "When you are ready to start Arc Pursuit\n" +
                    " using assistant '" + MODEL_ALIASES[i - 1] + "', press continue.";
            }
            yield return StartCoroutine(DisplayBreakMenu());
            yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
            _modelType = (i == 0) ? ModelType.None : _modelTypeOrdering[i - 1];
            yield return StartCoroutine(ArcPursuit());
            _modelType = ModelType.None;
        }

        BreakMessage.text =
            "Arc Pursuit Test Complete!";
        yield return StartCoroutine(DisplayBreakMenu());

        for (int i = 0; i < 4; i++)
        {
            if (i == 0)
            {
                BreakMessage.text =
                    "RAPID MOVEMENT\n" +
                    "\n" +
                    "For the Rapid Movement section of the test, three cubes will spawn in\n" +
                    "various locations in front of you and will begin to move towards you.\n" +
                    "Look directly at the cubes to reset them before they reach you.\n" +
                    "\n" +
                    "You may move your head to look around.\n" +
                    "\n" +
                    "This test will last for " + SECONDS_PER_TRIAL + " seconds.\n" +
                    "Press continue when you are ready to begin.";
            }
            else
            {
                BreakMessage.text =
                    "In this trial, you will not be able to move your head to look around.\n" +
                    " Our assistive technology will move the view based on your eye movements.\n" +
                    " We recommend keeping your head level and still. Move only your eyes.\n" +
                    "\n" +
                    "When you are ready to start Rapid Movement\n" +
                    " using assistant '" + MODEL_ALIASES[i - 1] + "', press continue.";
            }
            yield return StartCoroutine(DisplayBreakMenu());
            yield return StartCoroutine(DisplayCountdown(SHORT_BREAK, ""));
            _modelType = (i == 0) ? ModelType.None : _modelTypeOrdering[i - 1];
            yield return StartCoroutine(RapidMovementTest());
            _modelType = ModelType.None;
        }

        BreakMessage.text =
            "All Tests Complete! Thank you!\n" +
            "\n" +
            "Press continue to try the assistive technology in a different environment.";
        yield return StartCoroutine(DisplayBreakMenu());

        BreakMessage.text = "Loading City...";
        BreakCanvas.enabled = true;
        BreakCanvas.gameObject.SetActive(true);
        AsyncOperation load = SceneManager.LoadSceneAsync("CityEnvironment", LoadSceneMode.Single);
        UnityEngine.EventSystems.EventSystem.current.enabled = false;
        while (!load.isDone)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Suspends the game until the continue button is pressed.
    /// This can be used to give the user a break between sections where the data gathered can be ignored.
    /// </summary>
    private IEnumerator DisplayBreakMenu()
    {
        _continueClicked = false;
        BreakCanvas.enabled = true;
        BreakCanvas.gameObject.SetActive(true);

        while (!_continueClicked)
        {
            yield return null;
        }

        BreakCanvas.enabled = false;
        BreakCanvas.gameObject.SetActive(false);
        _continueClicked = false;
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
            CountdownMessage.text = message + i.ToString();
            yield return new WaitForSeconds(1);
        }

        CountdownMessage.text = "";
        CountdownCanvas.gameObject.SetActive(false);
    }

    private IEnumerator LinearPursuit()
    {
        TrackObjectLine.SetActive(true);
        _testType = TestType.SmoothLinearTest;
        IEnumerator trial = TrialStart();
        while (trial.MoveNext())
        {
            yield return trial.Current;
        }
        TrackObjectLine.SetActive(false);
    }

    private IEnumerator ArcPursuit()
    {
        TrackObjectArc.SetActive(true);
        _testType = TestType.SmoothArcTest;
        IEnumerator trial = TrialStart();
        while (trial.MoveNext())
        {
            yield return trial.Current;
        }
        TrackObjectArc.SetActive(false);
    }

    private IEnumerator RapidMovementTest()
    {
        GazeObject1.SetActive(true);
        GazeObject2.SetActive(true);
        GazeObject3.SetActive(true);
        _testType = TestType.RapidMovementTest;
        IEnumerator trial = TrialStart();
        while (trial.MoveNext())
        {
            yield return trial.Current;
        }
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
    }

    private IEnumerator TrialStart()
    {
        DisableHeadTracking.Disable = _modelType != ModelType.None;
        ResetModel();
        Invoke(nameof(Measurement), 0f);
        _gameTime = Time.time;
        while (Time.time - _gameTime < SECONDS_PER_TRIAL)
        {
            yield return null;
        }
        _testType = TestType.None;
        DisableHeadTracking.Disable = false;
        //ResetHead();
        Invoke(nameof(ResetHead), 1.0f);
        Release();
    }

    private void ResetModel()
    {
        _tensorLSTMHidden.Dispose();
        _tensorLSTMContext.Dispose();
        _tensorLSTMInput.Dispose();
        _workerLSTM = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, _modelLSTM);
        _tensorLSTMHidden = new Tensor(1, 1, 9, 1);
        _tensorLSTMContext = new Tensor(1, 1, 9, 1);
        _tensorLSTMInput = new Tensor(1, 1, 9, 1);
        for (int i = 0; i < 9; i++)
        {
            _tensorLSTMHidden[0, 0, i, 0] = 0;
            _tensorLSTMContext[0, 0, i, 0] = 0;
        }

        _tensorMLPInput.Dispose();
        _workerMLP = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, _modelMLP);
        _tensorMLPInput = new Tensor(1, 1, 27, 1);
    }
}

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
    private const bool DEBUG = true;

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
        LinearPursuit,
        ArcPursuit,
        RapidMovement,
        RapidAvoid,
        None,
    }

    private const bool ENABLE_CITY_SCENE = false;

    private static ModelSim _instance; // Used with static eye tracker callback, `GazeCallbackStatic`.

    private const int TEST_COUNT = (ENABLE_CITY_SCENE) ? 5 : 4;
    private const int TRIAL_COUNT = 3;

    private int _playerID;
    private Transform _player;
    private Quaternion _playerRotationStart;

    private ModelType _modelType;
    private static readonly ModelType[][] MODEL_TYPE_ORDERINGS = new ModelType[][]
    {
        new ModelType[] { ModelType.BaselineVector, ModelType.LSTM, ModelType.MLP },
        new ModelType[] { ModelType.BaselineVector, ModelType.MLP, ModelType.LSTM },
        new ModelType[] { ModelType.LSTM, ModelType.BaselineVector, ModelType.MLP },
        new ModelType[] { ModelType.LSTM, ModelType.MLP, ModelType.BaselineVector },
        new ModelType[] { ModelType.MLP, ModelType.BaselineVector, ModelType.LSTM },
        new ModelType[] { ModelType.MLP, ModelType.LSTM, ModelType.BaselineVector }
    };
    public int SeedModelTypeOrdering;
    private List<int> _modelTypeOrderingIndices = new List<int>();

    public int ModelDownsamplingRate;
    private int _frameMod = 0; // Used with `DownsamplingRate` to perform inference only on each n-th frame.
    public bool ModelUseRelativePositions;

    private const int INSTANCE_SIZE = 9;

    public NNModel ModelAssetLSTM;
    private Model _modelLSTM;
    private Tensor _tensorLSTMInput, _tensorLSTMHidden, _tensorLSTMContext;
    private int _modelLSTMHiddenSize; // Initialized dynamically; used to allocate hidden and context tensors.
    private IWorker _workerLSTM; // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html

    private const int MLP_WINDOW_SIZE = 3;
    public NNModel ModelAssetMLP;
    private Model _modelMLP;
    private Tensor _tensorMLPInput;
    private IWorker _workerMLP;

    private Vector3 _vecGazeL, _vecGazeR, _vecForward;
    private Vector3[] _bufferGazeL, _bufferGazeR, _bufferForward;
    private int _bufferCapacity;  // Create a buffer to hold a small history of states.
    private int _bufferIndex = 0;
    private int _bufferSize = 0;

    public GameObject TrackObjectLine;
    public GameObject TrackObjectArc;
    public GameObject GazeObject1, GazeObject2, GazeObject3;
    public GameObject AvoidObject1, AvoidObject2, AvoidObject3;
    public Canvas BreakCanvas, CountdownCanvas;
    public TextMesh BreakMessage, CountdownMessage;
    private bool _continueClicked = false;

    //private EyeParameter _eyeParameter = new EyeParameter();
    private EyeData_v2 _eyeData = new EyeData_v2();
    private bool _eyeCallbackRegistered = false;

    private bool _firstFrame = true;
    private TestType _testType = TestType.None;

    private int _ticksEye = 0;
    private int _ticksUpdate = 0;
    private int _ticksSequence = 0;

    private const int SECONDS_TRIAL = 10;
    private const int SECONDS_COUNTDOWN = 5;

    public bool DoCalibrateAtStart;

    // Start is called before the first frame update
    void Start()
    {
        _instance = this;

        Invoke(nameof(EyeTrackerSystemCheck), 0.5f);
        if (DoCalibrateAtStart)
        {
            SRanipal_Eye_v2.LaunchEyeCalibration();
        }
        SRanipal_Eye_Framework.Instance.EnableEyeDataCallback = true;

        _player = Camera.main.transform.parent.parent;
        _playerRotationStart = _player.rotation;

        _modelType = ModelType.None;
        _testType = TestType.None;
        DisableHeadTracking.Disable = false;

        UnityEngine.Random.InitState(31);

        System.Random rng = new System.Random(SeedModelTypeOrdering);
        for (int i = 0; i < TEST_COUNT; i++)
        {
            if (DEBUG)
            {
                _modelTypeOrderingIndices.Add(0);
            }
            else
            {
                _modelTypeOrderingIndices.Add(rng.Next() % MODEL_TYPE_ORDERINGS.Length);
            }
        }
        UnityEngine.Debug.Log("orderingIndices: " + _modelTypeOrderingIndices); // TODO: Persist to .csv.

        _modelLSTM = ModelLoader.Load(ModelAssetLSTM, true);
        _modelLSTMHiddenSize = _modelLSTM.inputs[1].shape[6];

        _modelMLP = ModelLoader.Load(ModelAssetMLP);

        if (ModelUseRelativePositions)
        {
            // Need an additional data point to calculate difference between x_t and x_{t-downsampling_rate}.
            _bufferCapacity = (MLP_WINDOW_SIZE + 1) * ModelDownsamplingRate;
        }
        else
        {
            _bufferCapacity = MLP_WINDOW_SIZE * ModelDownsamplingRate;
        }
        _bufferGazeL = new Vector3[_bufferCapacity];
        _bufferGazeR = new Vector3[_bufferCapacity];
        _bufferForward = new Vector3[_bufferCapacity];

        TrackObjectLine.SetActive(false);
        TrackObjectArc.SetActive(false);
        GazeObject1.SetActive(false);
        GazeObject2.SetActive(false);
        GazeObject3.SetActive(false);
        AvoidObject1.SetActive(false);
        AvoidObject2.SetActive(false);
        AvoidObject3.SetActive(false);
    }

    /// <summary>
    /// Check if the system works properly.
    /// </summary>
    void EyeTrackerSystemCheck()
    {
        if (SRanipal_Eye_API.GetEyeData_v2(ref _eyeData) == ViveSR.Error.WORK)
        {
            UnityEngine.Debug.Log("Device is working properly.");
        }

        //if (SRanipal_Eye_API.GetEyeParameter(ref _eyeParameter) == ViveSR.Error.WORK)
        //{
        //    UnityEngine.Debug.Log("Eye parameters are measured.");
        //}

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

    void EyeTrackerMeasurement()
    {
        //EyeParameter eye_parameter = new EyeParameter();
        //SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);

        if (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING)
        {
            UnityEngine.Debug.Log("Not working");
            return;
        }

        UnityEngine.Debug.Log(SRanipal_Eye_Framework.Instance.EnableEyeDataCallback.ToString());
        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && !_eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GazeCallbackStatic));
            _eyeCallbackRegistered = true;
        }
        else if (!SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && _eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GazeCallbackStatic));
            _eyeCallbackRegistered = false;
        }
    }

    void EyeTrackerRelease()
    {
        if (_eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)GazeCallbackStatic));
            _eyeCallbackRegistered = false;
        }
    }

    /// <summary>
    /// Callback function to record the eye movement data.
    /// Note that SRanipal_Eye_v2 does not work in the function below. It only works under UnityEngine.
    /// 
    /// Sep 12 2024, Eric: I found out that this function has to be STATIC or the app crashes. Go figure.
    ///     Therefore, everything that this function calls/accesses must also be static.
    /// </summary>
    private static void GazeCallbackStatic(ref EyeData_v2 eye_data)
    {
        _instance.GazeCallback(eye_data);
    }

    /// <summary>
    /// Runs at 120 Hz.
    /// </summary>
    /// <param name="eye_data"></param>
    private void GazeCallback(EyeData_v2 eye_data)
    {
        //EyeParameter eye_parameter = new EyeParameter();
        //SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);
        _eyeData = eye_data;

        Error error = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
        if (error == ViveSR.Error.WORK)
        {
            //SetGazeVectors(_eyeData.verbose_data.left.gaze_direction_normalized, _eyeData.verbose_data.right.gaze_direction_normalized);
            Vector3 gazeL = _eyeData.verbose_data.left.gaze_direction_normalized;
            Vector3 gazeR = _eyeData.verbose_data.right.gaze_direction_normalized;
            gazeL.x *= -1;
            gazeR.x *= -1;
            _vecGazeL = gazeL;
            _vecGazeR = gazeR;
        }

        _ticksEye++;
    }

    //private static void SetGazeVectors(Vector3 gazeL, Vector3 gazeR)
    //{
    //    gazeL.x *= -1;
    //    gazeR.x *= -1;

    //    _vecGazeL = gazeL;
    //    _vecGazeR = gazeR;

    //    _bufferGazeIndex = (_bufferGazeIndex + 1) % _bufferCapacity;
    //    _bufferGazeL[_bufferGazeIndex] = _vecGazeL;
    //    _bufferGazeR[_bufferGazeIndex] = _vecGazeR;
    //    if (_bufferGazeSize < _bufferCapacity)
    //    {
    //        _bufferGazeSize++;
    //    }
    //}

    //private void SetForwardVector(Vector3 forwardVec)
    //{
    //    _vecForward = forwardVec;

    //    _bufferForwardIndex = (_bufferForwardIndex + 1) % _bufferCapacity;
    //    _bufferForward[_bufferForwardIndex] = _vecForward;
    //    if (_bufferForwardSize < _bufferCapacity)
    //    {
    //        _bufferForwardSize++;
    //    }
    //}

    //void GetGazeRay(out Vector3 origin, out Vector3 direction, Transform transform)
    //{
    //}

    /// <summary>
    /// Changes the flag to indicate that one of the menu continue buttons has been clicked.
    /// </summary>
    public void ContinueClicked()
    {
        _continueClicked = true;
    }
    
    // Update is called once per frame
    // Runs at 84.5 Hz.
    void Update()
    {
        _frameMod = (_frameMod + 1) % ModelDownsamplingRate;

        //SetForwardVector(_player.forward);
        if (_testType != TestType.None)
        {
            _vecForward = _player.forward;

            _bufferIndex = (_bufferIndex + 1) % _bufferCapacity;
            _bufferGazeL[_bufferIndex] = _vecGazeL;
            _bufferGazeR[_bufferIndex] = _vecGazeR;
            _bufferForward[_bufferIndex] = _vecForward;
            if (_bufferSize < _bufferCapacity)
            {
                _bufferSize++;
            }

            _ticksUpdate++;
        }

        switch (_modelType)
        {
            case ModelType.BaselineQuadrant:
                QuadrantBaseline();
                break;
            case ModelType.BaselineVector:
                VectorBaseline();
                break;
            // Respect model down-sampling rate below.
            case ModelType.LSTM:
                if (_frameMod == 0)
                {
                    ModelCallLSTM();
                }
                break;
            case ModelType.MLP:
                if (_frameMod == 0)
                {
                    ModelCallMLP();
                }
                break;
        }

        Vector3 origin, direction;
        //GetGazeRay(out origin, out direction, Camera.main.transform);
        SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out origin, out direction, _eyeData);
        origin = Camera.main.transform.TransformPoint(origin);
        direction = Camera.main.transform.TransformDirection(direction);
        Ray gaze = new Ray(origin, direction);
        RaycastHit hit;
        if (_testType == TestType.LinearPursuit)
        {
            bool didHit = Physics.Raycast(gaze, out hit);
            TrackObjectLine.GetComponent<SmoothPursuitLinear>().GazeFocusChanged(didHit && hit.transform.gameObject == TrackObjectLine);
        }
        else if (_testType == TestType.ArcPursuit)
        {
            bool didHit = Physics.Raycast(gaze, out hit);
            TrackObjectArc.GetComponent<SmoothPursuitArc>().GazeFocusChanged(didHit && hit.transform.gameObject == TrackObjectArc);
        }
        else if (_testType == TestType.RapidMovement)
        {
            bool didHit = Physics.Raycast(gaze, out hit);
            GameObject[] gazeObjects = { GazeObject1, GazeObject2, GazeObject3 };
            foreach (GameObject gazeObject in gazeObjects)
            {
                gazeObject.GetComponent<HighlightAtGaze>().GazeFocusChanged(didHit && hit.transform.gameObject == gazeObject);
            }
        }
        else if (_testType == TestType.RapidAvoid)
        {
            bool didHit = Physics.Raycast(gaze, out hit);
            GameObject[] gazeObjects = { GazeObject1, GazeObject2, GazeObject3 };
            GameObject[] avoidObjects = { AvoidObject1, AvoidObject2, AvoidObject3 };
            foreach (GameObject gazeObject in gazeObjects)
            {
                gazeObject.GetComponent<HighlightAtGaze>().GazeFocusChanged(didHit && hit.transform.gameObject == gazeObject);
            }
            foreach (GameObject avoidObject in avoidObjects)
            {
                avoidObject.GetComponent<HighlightAtGaze>().GazeFocusChanged(didHit && hit.transform.gameObject == avoidObject);
            }
        }

        if (_firstFrame)
        {
            StartCoroutine(Sequence());
            _firstFrame = false;
        }
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

    /// <summary>
    /// Rotate the player object by finding vector between current forward direction and eye gaze
    /// direction. Rotate in direction of this vector.
    /// </summary>
    private void VectorBaseline()
    {
        // Compute

        // Colin Rubow: "1.767 is the average velocity proportion for the vector based controller.
        // It means, every 1 deg further a target is, the head should move 1.767 deg/s faster."
        float vectorVelocityProportion = 1.767f;

        float angle_boundary = 5.0f;  //boundary of eye angle
        //float rotate_speed = 4f;  //each rotate angle

        // eye angle in x direction > angle_boundary : rotate the 
        Vector3 gaze_direct_avg_world = _player.rotation * (_vecGazeL + _vecGazeR).normalized;

        var angle = Vector3.Angle(gaze_direct_avg_world, _vecForward);
        var global_angle = Vector3.Angle(gaze_direct_avg_world, new Vector3(0, 0, 1));
        if ((angle > angle_boundary || angle < -1 * angle_boundary) && (global_angle < 70f && global_angle > -70f))
        {
            float rotate_speed = 0.25f * vectorVelocityProportion * angle;
           _player.rotation = Quaternion.Slerp(_player.rotation, Quaternion.LookRotation(gaze_direct_avg_world), Time.deltaTime * rotate_speed);
        }
    }

    private void DebugTensorAxis6(Tensor tensor, string label)
    {
        string s = label + ": [";
        for (int i = 0; i < tensor.shape[6]; i++)
        {
            if (i > 0)
            {
                s += ", ";
            }
            s += tensor[0, 0, i, 0].ToString("0.###");
        }
        s += "]";
        Debug.Log(s);
    }

    private void ModelCallLSTM()
    {
        if (ModelUseRelativePositions)
        {
            if (_bufferSize <= ModelDownsamplingRate)
            {
                return;
            }

            int indexPrev = (_bufferIndex - ModelDownsamplingRate + _bufferCapacity) % _bufferCapacity;
            for (int i = 0; i < 3; i++)
            {
                _tensorLSTMInput[0, 0, i, 0] = _vecGazeL[i] - _bufferGazeL[indexPrev][i];
                _tensorLSTMInput[0, 0, i + 3, 0] = _vecGazeR[i] - _bufferGazeR[indexPrev][i];
                _tensorLSTMInput[0, 0, i + 6, 0] = _vecForward[i] - _bufferForward[indexPrev][i];
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                _tensorLSTMInput[0, 0, i, 0] = _vecGazeL[i];
                _tensorLSTMInput[0, 0, i + 3, 0] = _vecGazeR[i];
                _tensorLSTMInput[0, 0, i + 6, 0] = _vecForward[i];
            }
        }

        var inputs = new Dictionary<string, Tensor>() {
            {"input", _tensorLSTMInput},
            {"h0", _tensorLSTMHidden},
            {"c0", _tensorLSTMContext}
        };
        DebugTensorAxis6(_tensorLSTMInput, "LSTM input");
        DebugTensorAxis6(_tensorLSTMHidden, "LSTM hidden");
        DebugTensorAxis6(_tensorLSTMContext, "LSTM context");

        _workerLSTM.Execute(inputs);
        Tensor output = _workerLSTM.PeekOutput("output");
        DebugTensorAxis6(output, "LSTM output");
        _tensorLSTMHidden?.Dispose();
        _tensorLSTMHidden = _workerLSTM.PeekOutput("hn");
        _tensorLSTMContext?.Dispose();
        _tensorLSTMContext = _workerLSTM.PeekOutput("cn");

        Quaternion rotation;
        if (ModelUseRelativePositions)
        {
            var delta = new Vector3(output[0, 0, 0, 0], output[0, 0, 1, 0], output[0, 0, 2, 0]);
            Debug.Log("delta: " + delta.ToString());
            Debug.Log("delta.normalized: " + delta.normalized.ToString());
            rotation = Quaternion.LookRotation(_vecForward + delta.normalized);
            Debug.Log("rotation: " + rotation.ToString());
        }
        else
        {
            //var new_forward = new Vector3(output[0, 0, 0, 0] -0.05f, output[0, 0, 0, 1], output[0, 0, 0, 2]).normalized; // LRXYZ
            //var new_forward = new Vector3(output[0, 0, 0, 0], output[0, 0, 0, 1], output[0, 0, 0, 2]).normalized;
            var new_forward = new Vector3(output[0, 0, 0, 0], output[0, 0, 1, 0], output[0, 0, 2, 0]).normalized; // LSTM_...
            rotation = Quaternion.LookRotation(new_forward);
        }

        if (DisableHeadTracking.Disable)
        {
            _player.rotation = Quaternion.Slerp(_player.rotation, rotation, Time.deltaTime * 10.0f);
        }
    }

    private void ModelCallMLP()
    {
        if (_bufferSize < _bufferCapacity)
        {
            return;
        }

        // Build the input vector
        for (int w = 0; w < MLP_WINDOW_SIZE; w++)
        {
            int index = (_bufferIndex + ((w - MLP_WINDOW_SIZE + 1) * ModelDownsamplingRate) + _bufferCapacity) % _bufferCapacity;
            int windowOffset = w * INSTANCE_SIZE;
            if (ModelUseRelativePositions)
            {
                int indexPrev = (index - ModelDownsamplingRate + _bufferCapacity) % _bufferCapacity;
                for (int i = 0; i < 3; i++)
                {
                    _tensorMLPInput[0, 0, windowOffset + i, 0] = _bufferGazeL[index][i] - _bufferGazeL[indexPrev][i];
                    _tensorMLPInput[0, 0, windowOffset + 3 + i, 0] = _bufferGazeR[index][i] - _bufferGazeR[indexPrev][i];
                    _tensorMLPInput[0, 0, windowOffset + 6 + i, 0] = _bufferForward[index][i] - _bufferForward[indexPrev][i];
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    _tensorMLPInput[0, 0, windowOffset + i, 0] = _bufferGazeL[index][i];
                    _tensorMLPInput[0, 0, windowOffset + 3 + i, 0] = _bufferGazeR[index][i];
                    _tensorMLPInput[0, 0, windowOffset + 6 + i, 0] = _bufferForward[index][i];
                }
            }
        }
        var Inputs = new Dictionary<string, Tensor>() {
            {_modelMLP.inputs[0].name, _tensorMLPInput},
        };
        DebugTensorAxis6(_tensorMLPInput, "MLP input");

        _workerMLP.Execute(Inputs);
        string outputLayerName = _modelMLP.outputs[0];
        Tensor output = _workerMLP.PeekOutput(outputLayerName);
        DebugTensorAxis6(output, "MLP output");

        Quaternion rotation;
        if (ModelUseRelativePositions)
        {
            var delta = new Vector3(output[0, 0, 0, 0], output[0, 0, 0, 1], output[0, 0, 0, 1]);
            rotation = Quaternion.LookRotation(_vecForward + delta.normalized);
        }
        else
        {
            var new_forward = new Vector3(output[0, 0, 0, 0], output[0, 0, 0, 1], output[0, 0, 0, 2]).normalized;
            rotation = Quaternion.LookRotation(new_forward);
        }

        if (DisableHeadTracking.Disable)
        {
            _player.rotation = Quaternion.Slerp(_player.rotation, rotation, Time.deltaTime * 50.0f);
        }
    }

    private void ResetHead()
    {
        //_player.rotation = Quaternion.LookRotation(new Vector3(0, 0, 1));
        _player.rotation = _playerRotationStart;
        DisableHeadTracking.ResetHead();
    }

    /// <summary>
    /// Saccade task sequence.
    /// </summary>
    private IEnumerator Sequence()
    {
        // DO NOT REMOVE THE PRIVACY STATEMENT, REQUIRED BY HTC 
        // Participants should see the paper version during the consent process https://docs.google.com/document/d/13ehQgG4bj30qM26owmaHe9gsbGhAz9uMMaSYZKIm2cA/edit?usp=sharing
        //BreakMessage.text =
        //    "Welcome to the virtual environment. The following is a version of the privacy statement you should have already seen during the consent process." +
        //    "\nIf you have not seen this do not continue until the staff provide you with a physical copy of this and have explained it and answered any questions to your satifaction." +
        //    "\n\n Privacy Statement: While using this virtual environment, data about your facial expressions will be saved." +
        //    "\n This includes head position and orientation, gaze origin, gaze direction, gaze sensitivity scale, validity of data, time stamps of the data, and details concerning items in the virtual environment." +
        //    "\nWe will not collect images of your eyes, and the data collected from this environment should not be able to identify you when used independently of our other records." +
        //    "\nWe will never sell this data to another party, and we will work to maintain its confidentiality to the best of our ability." +
        //    "\nWe will not share this information with individuals outside of our research team without your consent, and we will not use this data to discriminate against any party." +
        //    "\nThis data wil not be used to make decisions regarding eligibility or terms for any services, including loans. We will not use third party services to process this data without your consent." +
        //    "\nWe will use this data to learn how paitients experiencing limited neck mobility may regain a portion of autonomy by controlling an assistive neck brace. " +
        //    "\nBecause we are using this data for a healthcare purpose, we will comply with regulations such as HIPAA as it applies to any data collected." +
        //    "\nWe will follow other procedures to ensure all of your data is protected and not misused. If you are concerned that your data will be or has been misused, or are concerned about the data being saved, " +
        //    "\ndiscontinue participation in the study immediately and contact the University of Utah IRB. This privacy statement was last modified August 18, 2022." +
        //    "\n\nPress continue if you agree with the privacy statement and are ready to begin.";

        BreakMessage.text =
            "Welcome to the virtual environment.\n" +
            "\n" +
            "Using the trigger button of the controller, press continue.";
        yield return StartCoroutine(DisplayBreakMenu());

        TestType[] testTypes = new TestType[]
        {
            TestType.LinearPursuit,
            TestType.ArcPursuit,
            TestType.RapidMovement,
            TestType.RapidAvoid,
        };
        GameObject[][] testObjects = new GameObject[][]
        {
            new GameObject[] { TrackObjectLine },
            new GameObject[] { TrackObjectArc },
            new GameObject[] { GazeObject1, GazeObject2, GazeObject3 },
            new GameObject[] { GazeObject1, GazeObject2, GazeObject3, AvoidObject1, AvoidObject2, AvoidObject3 },
        };
        string[] testTitles = new string[]
        {
            "Linear Pursuit",
            "Arc Pursuit",
            "Rapid Movement",
            "Rapid Avoid",
        };
        string[] testExplanations = new string[]
        {
            "LINEAR PURSUIT\n" +
            "\n" +
            "Follow the floating cube. It will move around in straight lines.\n" +
            "Look directly at the cube to change its color.",

            "ARC PURSUIT\n" +
            "\n" +
            "Follow the floating cube. It will move around in curved paths.\n" +
            "Look directly at the cube to change its color.",

            "RAPID MOVEMENT\n" +
            "\n" +
            "Three cubes will spawn in front of you and will move towards you.\n" +
            "Look directly at the cubes to reset them before they reach you.",

            "RAPID AVOID\n" +
            "\n" +
            "Exactly like Rapid Movement, with the addition of three\n" +
            " yellow DISTRACTOR CUBES.\n" +
            "The yellow cubes CANNOT BE RESET.",
        };

        for (int i = 0; i < TEST_COUNT - 1; i++)
        {
            if (i > 0)
            {
                BreakMessage.text = "" + testTitles[i - 1] + " complete!";
                yield return StartCoroutine(DisplayBreakMenu());
            }

            string explanation = "" + testExplanations[i];
            if (i > 0)
            {
                explanation += "\n\nYou may move your head to look around.";
            }
            explanation += "\n\nWhen you are ready to start " + testTitles[i] + ", press continue.";
            BreakMessage.text = explanation;
            yield return StartCoroutine(DisplayBreakMenu());
            yield return StartCoroutine(DisplayCountdown(SECONDS_COUNTDOWN, ""));
            _modelType = ModelType.None;
            yield return StartCoroutine(TrialStart(testTypes[i], false, testObjects[i]));

            for (int j = 0; j < TRIAL_COUNT; j++)
            {
                BreakMessage.text =
                    "In this trial, you will NOT be able to move your head to look around.\n" +
                    " Our assistive technology will move the view based on your eye movements.\n" +
                    " We recommend keeping your head level and still. Move only your eyes.\n" +
                    "\n" +
                    "When you are ready to start " + testTitles[i] + "\n" +
                    " using only your eyes, press continue.";
                yield return StartCoroutine(DisplayBreakMenu());
                yield return StartCoroutine(DisplayCountdown(SECONDS_COUNTDOWN, ""));
                _modelType = MODEL_TYPE_ORDERINGS[_modelTypeOrderingIndices[i]][j];
                yield return StartCoroutine(TrialStart(testTypes[i], true, testObjects[i]));
                _modelType = ModelType.None;
            }
        }

        if (ENABLE_CITY_SCENE)
        {
            BreakMessage.text =
                "All tests Complete!\n" +
                "\n" +
                "Press continue to try the assistive technology in a different environment.";
            yield return StartCoroutine(DisplayBreakMenu());

            /* RAPID AVOID END - CITY SCENE START */

            AsyncOperation loadCity = SceneManager.LoadSceneAsync("CityScene", LoadSceneMode.Additive);
            while (!loadCity.isDone)
            {
                yield return null;
            }

            BreakMessage.text =
                "Now, feel free to look around this city environment.\n" +
                "\n" +
                "Press continue when you're ready to move on.";
            yield return StartCoroutine(DisplayBreakMenu());

            for (int j = 0; j < TRIAL_COUNT; j++)
            {
                BreakMessage.text =
                    "Using only your eyes, look at anything in this scene\n" +
                    " using our assistive technology.\n" +
                    "\n" +
                    "To begin looking with only your eyes, press continue.";
                yield return StartCoroutine(DisplayBreakMenu());
                _modelType = MODEL_TYPE_ORDERINGS[_modelTypeOrderingIndices[4]][j];
                // TODO: Spawn player randomly in one of a set of "good" starting positions.
                yield return StartCoroutine(TrialStart(TestType.None, true, new GameObject[] { }));
                // TODO: Return player to street light.
                //_player.position = new Vector3(-166.8f, 6.22f, -424.8f); // Sidewalk corner under street light.
                _modelType = ModelType.None;
            }
        }

        BreakMessage.text =
            "That concludes the study.\n" +
            "\n" +
            "Thank you!";
        yield return StartCoroutine(DisplayBreakMenu());
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

    private IEnumerator TrialStart(TestType testType, bool disableHead, GameObject[] gameObjects)
    {
        _ticksEye = 0;
        _ticksUpdate = 0;
        _ticksSequence = 0;
        foreach (GameObject gameObject in gameObjects)
        {
            gameObject.SetActive(true);
        }
        _testType = testType;
        DisableHeadTracking.Disable = disableHead;
        ResetModel();
        Invoke(nameof(EyeTrackerMeasurement), 0f);

        float gameTime = Time.time;
        while (Time.time - gameTime < SECONDS_TRIAL)
        {
            _ticksSequence++;
            yield return null;
        }

        Debug.Log("SECONDS_TRIAL: " + SECONDS_TRIAL);
        Debug.Log("_ticksEye: " + _ticksEye);
        Debug.Log("_ticksUpdate: " + _ticksUpdate);
        Debug.Log("_ticksSequence: " + _ticksSequence);
        foreach (GameObject gameObject in gameObjects)
        {
            gameObject.SetActive(false);
        }
        _testType = TestType.None;
        DisableHeadTracking.Disable = false;
        ResetHead();
        EyeTrackerRelease();
    }

    private void ResetModel()
    {
        _workerLSTM?.Dispose();
        _tensorLSTMInput?.Dispose();
        _tensorLSTMHidden?.Dispose();
        _tensorLSTMContext?.Dispose();
        _tensorLSTMHidden = null;
        _tensorLSTMContext = null;
        _workerLSTM = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, _modelLSTM);
        _tensorLSTMInput = new Tensor(1, 1, INSTANCE_SIZE, 1, "LSTMInput");
        _tensorLSTMHidden = new Tensor(1, 1, _modelLSTMHiddenSize, 1, "LSTMHidden");
        _tensorLSTMContext = new Tensor(1, 1, _modelLSTMHiddenSize, 1, "LSTMContext");
        for (int i = 0; i < _modelLSTMHiddenSize; i++)
        {
            _tensorLSTMHidden[0, 0, i, 0] = 0;
            _tensorLSTMContext[0, 0, i, 0] = 0;
        }

        _workerMLP?.Dispose();
        _tensorMLPInput?.Dispose();
        _workerMLP = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, _modelMLP);
        _tensorMLPInput = new Tensor(1, 1, MLP_WINDOW_SIZE * INSTANCE_SIZE, 1);
        
        _bufferSize = 0;
    }
}

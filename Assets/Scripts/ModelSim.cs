using System;
using System.IO;
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
    public bool DebugMode;

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

    private static ModelSim _instance; // Used with static eye tracker callback, `GazeCallbackStatic`.

    private const int TEST_COUNT = 4;
    private const int COMPARISON_COUNT = 3;

    private Transform _camera;
    private Transform _player;
    private Quaternion _playerRotationStart;

    private int _userID;
    private string _pathUsers = Path.Combine("Data", "Sim", "users_v3.csv");
    private string _pathUserFolder;
    private System.Random _rng;
    private List<Vector3> _historyGazeL, _historyGazeR, _historyForward;
    private List<Quaternion> _historyCamera;
    private List<List<Vector3>> _historyObjects;

    private ModelType _modelType;
    private static readonly ModelType[][] MODEL_TYPE_ORDERINGS = new ModelType[][]
    {
        new ModelType[] { ModelType.BaselineQuadrant, ModelType.BaselineVector, ModelType.LSTM, ModelType.MLP },
        new ModelType[] { ModelType.BaselineQuadrant, ModelType.BaselineVector, ModelType.MLP, ModelType.LSTM },
        new ModelType[] { ModelType.BaselineQuadrant, ModelType.LSTM, ModelType.BaselineVector, ModelType.MLP },
        new ModelType[] { ModelType.BaselineQuadrant, ModelType.LSTM, ModelType.MLP, ModelType.BaselineVector },
        new ModelType[] { ModelType.BaselineQuadrant, ModelType.MLP, ModelType.BaselineVector, ModelType.LSTM },
        new ModelType[] { ModelType.BaselineQuadrant, ModelType.MLP, ModelType.LSTM, ModelType.BaselineVector },

        new ModelType[] { ModelType.BaselineVector, ModelType.BaselineQuadrant, ModelType.LSTM, ModelType.MLP },
        new ModelType[] { ModelType.BaselineVector, ModelType.BaselineQuadrant, ModelType.MLP, ModelType.LSTM },
        new ModelType[] { ModelType.BaselineVector, ModelType.LSTM, ModelType.BaselineQuadrant, ModelType.MLP },
        new ModelType[] { ModelType.BaselineVector, ModelType.LSTM, ModelType.MLP, ModelType.BaselineQuadrant },
        new ModelType[] { ModelType.BaselineVector, ModelType.MLP, ModelType.BaselineQuadrant, ModelType.LSTM },
        new ModelType[] { ModelType.BaselineVector, ModelType.MLP, ModelType.LSTM, ModelType.BaselineQuadrant },

        new ModelType[] { ModelType.LSTM, ModelType.BaselineQuadrant, ModelType.BaselineVector, ModelType.MLP },
        new ModelType[] { ModelType.LSTM, ModelType.BaselineQuadrant, ModelType.MLP, ModelType.BaselineVector },
        new ModelType[] { ModelType.LSTM, ModelType.BaselineVector, ModelType.BaselineQuadrant, ModelType.MLP },
        new ModelType[] { ModelType.LSTM, ModelType.BaselineVector, ModelType.MLP, ModelType.BaselineQuadrant },
        new ModelType[] { ModelType.LSTM, ModelType.MLP, ModelType.BaselineQuadrant, ModelType.BaselineVector },
        new ModelType[] { ModelType.LSTM, ModelType.MLP, ModelType.BaselineVector, ModelType.BaselineQuadrant },

        new ModelType[] { ModelType.MLP, ModelType.BaselineQuadrant, ModelType.BaselineVector, ModelType.LSTM },
        new ModelType[] { ModelType.MLP, ModelType.BaselineQuadrant, ModelType.LSTM, ModelType.BaselineVector },
        new ModelType[] { ModelType.MLP, ModelType.BaselineVector, ModelType.BaselineQuadrant, ModelType.LSTM },
        new ModelType[] { ModelType.MLP, ModelType.BaselineVector, ModelType.LSTM, ModelType.BaselineQuadrant },
        new ModelType[] { ModelType.MLP, ModelType.LSTM, ModelType.BaselineQuadrant, ModelType.BaselineVector },
        new ModelType[] { ModelType.MLP, ModelType.LSTM, ModelType.BaselineVector, ModelType.BaselineQuadrant },
    };
    private readonly List<int> _modelTypeOrderingIndices = new List<int>();
    private ModelType _modelTypePreferred;

    private TestType _testType = TestType.None;

    double _vPitch = 0.3299;  //  0.1562;
    double _aPitch = 3.6013;  //  0.8983;
    double _bPitch = 1.1195;  //  2.7913;
    double _cPitch = -0.1271; // -0.1261;

    double _vYaw = 0.2113; // 0.1629;
    double _aYaw = 2.7898; // 0.5761;
    double _bYaw = 0.8050; // 2.4104;
    double _cYaw = 0.0015; // 0.0290;

    private int _inputLength = 9;

    public NNModel ModelAssetLSTM;
    private Model _modelLSTM;
    private Tensor _tensorLSTMInput, _tensorLSTMHidden, _tensorLSTMContext;
    private int _modelLSTMHiddenSize; // Initialized dynamically; used to allocate hidden and context tensors.
    private IWorker _workerLSTM; // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html
    private const int MODEL_LSTM_SKIP_UPDATE_ITERATIONS = 55;
    private int _modelLSTMSkipCounter = 0;

    public NNModel ModelAssetMLP;
    private Model _modelMLP;
    private Tensor _tensorMLPInput;
    private IWorker _workerMLP;

    private float _outputLastPitch; // Rectify blinking by repeating the last rotation when the eyes were open.
    private float _outputLastYaw;

    private Vector3 _vecGazeL, _vecGazeR, _vecForward;

    public GameObject TrackObjectLine;
    public GameObject TrackObjectArc;
    public GameObject GazeObject1, GazeObject2, GazeObject3;
    public GameObject AvoidObject1, AvoidObject2, AvoidObject3;
    public Canvas BreakCanvas, CountdownCanvas, PreferenceCanvas;
    public TextMesh BreakMessage, CountdownMessage;
    private bool _continueClicked = false;
    private bool _firstClicked = false;
    private bool _secondClicked = false;
    private bool _noPreferenceClicked = false;

    private readonly List<int> _scores = new List<int>();
    private readonly List<int> _scoresPossible = new List<int>();
    private int _score;
    private int _scorePossible;

    //private EyeParameter _eyeParameter = new EyeParameter();
    private EyeData_v2 _eyeData = new EyeData_v2();
    private bool _eyeCallbackRegistered = false;

    private bool _firstFrame = true;

    public int SecondsTrial;
    private const int SECONDS_COUNTDOWN = 5;

    public bool DoCalibrateAtStart;

    // Start is called before the first frame update
    void Start()
    {
        _instance = this;

        File.AppendAllText(_pathUsers, "\r\n"); // The file should never end with a newline.
        string[] linesUsers = File.ReadAllLines(_pathUsers);
        _userID = linesUsers.Length - 1; // Auto-increment.
        _rng = new System.Random(_userID);
        File.AppendAllText(_pathUsers, "" + _userID + "," + SecondsTrial);
        for (int i = 0; i < TEST_COUNT; i++)
        {
            //int seed = _rng.Next() % 65536;
            //_testSeeds.Add(seed);
            //lineUser += "," + seed.ToString();

            int index = DebugMode ? 0 : _rng.Next() % MODEL_TYPE_ORDERINGS.Length;
            _modelTypeOrderingIndices.Add(index);
            //for (int j = 0; j < TRIAL_COUNT; j++)
            //{
            //    lineUser += "," + MODEL_TYPE_ORDERINGS[index][j].ToString();
            //}
        }
        _pathUserFolder = Path.Combine("Data", "Sim", "User_v3_" + _userID);
        Directory.CreateDirectory(_pathUserFolder);

        Invoke(nameof(EyeTrackerSystemCheck), 0.5f);
        if (DoCalibrateAtStart)
        {
            bool calibrationError = SRanipal_Eye_v2.LaunchEyeCalibration();
            Debug.Log("Calibration at start: " + calibrationError);
        }
        SRanipal_Eye_Framework.Instance.EnableEyeDataCallback = true;

        _camera = Camera.main.transform;
        _player = Camera.main.transform.parent.parent;
        _playerRotationStart = _player.rotation;

        _modelType = ModelType.None;
        _testType = TestType.None;
        DisableHeadTracking.Disable = false;

        _modelLSTM = ModelLoader.Load(ModelAssetLSTM, true);
        _modelLSTMHiddenSize = _modelLSTM.inputs[1].shape[6];

        _modelMLP = ModelLoader.Load(ModelAssetMLP);

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
    private static void GazeCallbackStatic(ref EyeData_v2 eyeData)
    {
        _instance.GazeCallback(eyeData);
    }

    /// <summary>
    /// Runs at 120 Hz.
    /// </summary>
    /// <param name="eyeData"></param>
    private void GazeCallback(EyeData_v2 eyeData)
    {
        _eyeData = eyeData;

        Error error = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
        if (error == ViveSR.Error.WORK)
        {
            //SetGazeVectors(_eyeData.verbose_data.left.gaze_direction_normalized, _eyeData.verbose_data.right.gaze_direction_normalized);
            Vector3 gazeL = _eyeData.verbose_data.left.gaze_direction_normalized;
            Vector3 gazeR = _eyeData.verbose_data.right.gaze_direction_normalized;
            gazeL.x *= -1; // Change to left-handed to match Unity's coordinate system.
            gazeR.x *= -1;
            _vecGazeL = gazeL;
            _vecGazeR = gazeR;
        }
    }

    private bool EyeIsOpen(SingleEyeData singleEyeData)
    {
        //return singleEyeData.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY);
        return singleEyeData.gaze_direction_normalized.magnitude > 0.5f;
    }

    /// <summary>
    /// Changes the flag to indicate that one of the menu continue buttons has been clicked.
    /// </summary>
    public void ContinueClicked()
    {
        _continueClicked = true;
    }

    public void FirstClicked()
    {
        _firstClicked = true;
    }

    public void SecondClicked()
    {
        _secondClicked = true;
    }

    public void NoPreferenceClicked()
    {
        _noPreferenceClicked = true;
    }

    // Update is called once per frame.
    // Runs at 90Hz without inference; ~45Hz with LSTM; ~85Hz with MLP.
    void Update()
    {
        //SetForwardVector(_player.forward);
        if (_testType != TestType.None)
        {
            _vecForward = _player.forward;

            _historyGazeL.Add(_vecGazeL);
            _historyGazeR.Add(_vecGazeR);
            _historyForward.Add(_vecForward);
            _historyCamera.Add(_camera.localRotation);
        }

        // Check that both eyes are open.
        if (EyeIsOpen(_eyeData.verbose_data.left) && EyeIsOpen(_eyeData.verbose_data.right))
        {
            // Invoke model inference.
            switch (_modelType)
            {
                case ModelType.BaselineQuadrant:
                    QuadrantBaseline();
                    break;
                case ModelType.BaselineVector:
                    //VectorBaseline();
                    VectorParameterized();
                    break;
                case ModelType.LSTM:
                    ModelCallLSTM();
                    break;
                case ModelType.MLP:
                    ModelCallMLP();
                    break;
            }
        }
        else if (_modelType != ModelType.None && _modelType != ModelType.BaselineQuadrant)
        {
            // Repeat last.
            RotatePlayer(0.9f * _outputLastPitch, 0.9f * _outputLastYaw);
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
            _historyObjects.Add(new List<Vector3> { TrackObjectLine.transform.position });

            _scorePossible++;
            bool didHit = Physics.Raycast(gaze, out hit);
            didHit = didHit && hit.transform.gameObject == TrackObjectLine;
            TrackObjectLine.GetComponent<SmoothPursuitLinear>().GazeFocusChanged(didHit);
            if (didHit)
            {
                _score++;
            }
        }
        else if (_testType == TestType.ArcPursuit)
        {
            _historyObjects.Add(new List<Vector3> { TrackObjectArc.transform.position });

            _scorePossible++;
            bool didHit = Physics.Raycast(gaze, out hit);
            didHit = didHit && hit.transform.gameObject == TrackObjectArc;
            TrackObjectArc.GetComponent<SmoothPursuitArc>().GazeFocusChanged(didHit);
            if (didHit)
            {
                _score++;
            }
        }
        else if (_testType == TestType.RapidMovement)
        {
            GameObject[] gazeObjects = { GazeObject1, GazeObject2, GazeObject3 };
            List<Vector3> objects = new List<Vector3>();
            foreach (GameObject gazeObject in gazeObjects)
            {
                objects.Add(gazeObject.transform.position);
            }
            _historyObjects.Add(objects);

            bool didHit = Physics.Raycast(gaze, out hit);
            foreach (GameObject gazeObject in gazeObjects)
            {
                gazeObject.GetComponent<HighlightAtGaze>().GazeFocusChanged(didHit && hit.transform.gameObject == gazeObject);
            }
        }
        else if (_testType == TestType.RapidAvoid)
        {
            GameObject[] gazeObjects = { GazeObject1, GazeObject2, GazeObject3 };
            GameObject[] avoidObjects = { AvoidObject1, AvoidObject2, AvoidObject3 };
            List<Vector3> objects = new List<Vector3>();
            foreach (GameObject gazeObject in gazeObjects)
            {
                objects.Add(gazeObject.transform.position);
            }
            foreach (GameObject avoidObject in avoidObjects)
            {
                objects.Add(avoidObject.transform.position);
            }
            _historyObjects.Add(objects);

            bool didHit = Physics.Raycast(gaze, out hit);
            foreach (GameObject gazeObject in gazeObjects)
            {
                gazeObject.GetComponent<HighlightAtGaze>().GazeFocusChanged(didHit && hit.transform.gameObject == gazeObject);
            }
            foreach (GameObject avoidObject in avoidObjects)
            {
                avoidObject.GetComponent<AvoidObstacleTest>().GazeFocusChanged(didHit && hit.transform.gameObject == avoidObject);
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

    ///// <summary>
    ///// Rotate the player object by finding vector between current forward direction and eye gaze
    ///// direction. Rotate in direction of this vector.
    ///// </summary>
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

    private void VectorParameterized()
    {
        Vector3 v = (_vecGazeL + _vecGazeR).normalized;
        double thetaPitch = Math.Atan(v.y / v.z) - _cPitch;
        int signPitch = (thetaPitch < 0.0) ? -1 : 1;
        double thetaYaw = Math.Atan(v.x / v.z) - _cYaw;
        int signYaw = (thetaYaw < 0.0) ? -1 : 1;

        double incrementPitch = VectorAngleSoftDeadZone(Math.Abs(thetaPitch), _vPitch, _aPitch, _bPitch);
        double incrementYaw = VectorAngleSoftDeadZone(Math.Abs(thetaYaw), _vYaw, _aYaw, _bYaw);
        RotatePlayer((float)incrementPitch * signPitch, (float)incrementYaw * signYaw);
    }

    private double VectorAngleSoftDeadZone(double x, double v, double a, double b)
    {
        return v * x / (1 + ((float)Math.Pow((float)Math.E, a * (-x + b))));
    }

    private void DebugTensor(Tensor tensor, int axis, string label)
    {
        string s = label + ": [";
        for (int i = 0; i < tensor.shape[axis]; i++)
        {
            if (i > 0)
            {
                s += ", ";
            }
            float x;
            if (axis == 6)
            {
                x = tensor[0, 0, i, 0];
            }
            else if (axis == 7)
            {
                x = tensor[0, 0, 0, i];
            }
            else
            {
                throw new Exception("Invalid axis: " + axis);
            }
            s += x.ToString("0.#####");
        }
        s += "]";
        Debug.Log(s);
    }

    private void ModelCallLSTM()
    {
        for (int i = 0; i < 3; i++)
        {
            _tensorLSTMInput[0, 0, i, 0] = _vecGazeL[i];
            _tensorLSTMInput[0, 0, i + 3, 0] = _vecGazeR[i];
            _tensorLSTMInput[0, 0, i + 6, 0] = _vecForward[i];
        }

        var inputs = new Dictionary<string, Tensor>() {
            {"input", _tensorLSTMInput},
            {"h0", _tensorLSTMHidden},
            {"c0", _tensorLSTMContext}
        };
        DebugTensor(_tensorLSTMInput, 6, "LSTM input");
        DebugTensor(_tensorLSTMHidden, 6, "LSTM hidden");
        DebugTensor(_tensorLSTMContext, 6, "LSTM context");

        _workerLSTM.Execute(inputs);
        Tensor output = _workerLSTM.PeekOutput("output");
        //Debug.Log("LSTM shape: " + output.shape);
        DebugTensor(output, 7, "LSTM output");
        _tensorLSTMHidden?.Dispose();
        _tensorLSTMHidden = _workerLSTM.PeekOutput("hn");
        _tensorLSTMContext?.Dispose();
        _tensorLSTMContext = _workerLSTM.PeekOutput("cn");

        // Wait a few iterations to let the hidden states settle down to something reasonable.
        if (_modelLSTMSkipCounter > 0)
        {
            _modelLSTMSkipCounter--;
        }
        else
        {
            // Clip.
            float incrementPitch = Math.Max(-0.01f, Math.Min(0.01f, output[0, 0, 0, 0]));
            float incrementYaw = Math.Max(-0.01f, Math.Min(0.01f, output[0, 0, 0, 1]));
            RotatePlayer(incrementPitch, incrementYaw);
        }
    }

    private void ModelCallMLP()
    {
        // Build the input vector
        for (int i = 0; i < 3; i++)
        {
            _tensorMLPInput[0, 0, i, 0] = _vecGazeL[i];
            _tensorMLPInput[0, 0, i + 3, 0] = _vecGazeR[i];
            _tensorMLPInput[0, 0, i + 6, 0] = _vecForward[i];
        }
        var Inputs = new Dictionary<string, Tensor>() {
            {_modelMLP.inputs[0].name, _tensorMLPInput},
        };
        //DebugTensor(_tensorMLPInput, 7, "MLP input");

        _workerMLP.Execute(Inputs);
        string outputLayerName = _modelMLP.outputs[0];
        Tensor output = _workerMLP.PeekOutput(outputLayerName);
        DebugTensor(output, 7, "MLP output");

        RotatePlayer(output[0, 0, 0, 0], output[0, 0, 0, 1]);
    }

    void RotatePlayer(float incrementPitch, float incrementYaw)
    {
        _outputLastPitch = incrementPitch;
        _outputLastYaw = incrementYaw;

        Debug.Log("incrementPitch: " + incrementPitch + "; incrementYaw: " + incrementYaw);
        double x = _player.forward.x;
        double y = _player.forward.y;
        double z = _player.forward.z;
        Debug.Log("forward: " + x + ", " + y + ", " + z);

        double pitch = Math.Max(-Math.PI / 2, Math.Min(Math.PI / 2, Math.Atan(y / z) + incrementPitch));
        y = Math.Tan(pitch) * z;

        double yaw = Math.Max(-Math.PI / 2, Math.Min(Math.PI / 2, Math.Atan(x / z) + incrementYaw));
        x = Math.Tan(yaw) * z;

        Vector3 forward = new Vector3((float)x, (float)y, (float)Math.Abs(z));
        _player.rotation = Quaternion.LookRotation(forward.normalized);
    }

    private void ResetModel()
    {
        _workerLSTM?.Dispose();
        _tensorLSTMInput?.Dispose();
        _tensorLSTMHidden?.Dispose();
        _tensorLSTMContext?.Dispose();
        _tensorLSTMHidden = null;
        _tensorLSTMContext = null;
        _workerLSTM = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, _modelLSTM);
        _tensorLSTMInput = new Tensor(1, 1, _inputLength, 1, "LSTMInput");
        _tensorLSTMHidden = new Tensor(1, 1, _modelLSTMHiddenSize, 1, "LSTMHidden");
        _tensorLSTMContext = new Tensor(1, 1, _modelLSTMHiddenSize, 1, "LSTMContext");
        for (int i = 0; i < _modelLSTMHiddenSize; i++)
        {
            _tensorLSTMHidden[0, 0, i, 0] = 0;
            _tensorLSTMContext[0, 0, i, 0] = 0;
        }
        _modelLSTMSkipCounter = MODEL_LSTM_SKIP_UPDATE_ITERATIONS;

        _workerMLP?.Dispose();
        _tensorMLPInput?.Dispose();
        _workerMLP = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, _modelMLP);
        _tensorMLPInput = new Tensor(1, 1, _inputLength, 1);
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
            "Follow the floating blue cube. It will move around in straight lines.\n" +
            "Try to look directly at the cube to change its color to green.",

            "ARC PURSUIT\n" +
            "\n" +
            "Follow the floating blue cube. It will move around in curved paths.\n" +
            "Try to look directly at the cube to change its color to green.",

            "RAPID MOVEMENT\n" +
            "\n" +
            "Blue cubes will spawn from different directions and will move towards you.\n" +
            "Try to look directly at the cubes to make them disappear before they reach you.",

            "RAPID AVOID\n" +
            "\n" +
            "Exactly like Rapid Movement, with the addition of three\n" +
            " yellow DISTRACTOR CUBES.\n" +
            "Ignore the yellow cubes. They can't be destroyed.\n" +
            "Your goal is to still make the blue cubes disappear.",
        };

        for (int i = 0; i < TEST_COUNT; i++)
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
            yield return StartCoroutine(DisplayCountdown(SECONDS_COUNTDOWN, "PRACTICE... "));
            yield return StartCoroutine(TrialStart(i, -1, 0, testTypes[i], ModelType.None, -1, false, testObjects[i]));

            for (int j = 0; j < COMPARISON_COUNT; j++)
            {
                // In each of the following trials, 
                BreakMessage.text =
                    "" + testTitles[i].ToUpper() + "   " + (j + 1) + " / " + COMPARISON_COUNT + "\n" +
                    "\n" +
                    "In this trial, you will NOT be able to move your head to look around.\n" +
                    " A controller will move the view based on your eye movements.\n" +
                    " We recommend keeping your head level and still. Move only your eyes.\n" +
                    "\n" +
                    "You will use two different controllers, one after the other.\n" +
                    "Afterwards, you will select which of the two you preferred.\n" +
                    "\n" +
                    "When you are ready to start " + testTitles[i] + "\n" +
                    " using only your eyes, press continue.";
                yield return StartCoroutine(DisplayBreakMenu());

                ModelType modelTypeFirst;
                ModelType modelTypeSecond;
                if (j == 0)
                {
                    modelTypeFirst = MODEL_TYPE_ORDERINGS[_modelTypeOrderingIndices[i]][0];
                }
                else
                {
                    modelTypeFirst = _modelTypePreferred;
                }
                modelTypeSecond = MODEL_TYPE_ORDERINGS[_modelTypeOrderingIndices[i]][j + 1];
                // Randomize order of pair.
                if (_rng.Next() % 2 == 0)
                {
                    ModelType t = modelTypeFirst;
                    modelTypeFirst = modelTypeSecond;
                    modelTypeSecond = t;
                }
                // Seed the trial.
                int seed = MakeSeed();
                // Persist user values for this trial.
                File.AppendAllText(_pathUsers, "," + seed.ToString() + "," + modelTypeFirst.ToString() + "," + modelTypeSecond.ToString());
                // Start pair of trials.
                yield return StartCoroutine(DisplayCountdown(SECONDS_COUNTDOWN, "FIRST CONTROLLER... "));
                yield return StartCoroutine(TrialStart(i, j, 0, testTypes[i], modelTypeFirst, seed, true, testObjects[i]));
                yield return StartCoroutine(DisplayCountdown(SECONDS_COUNTDOWN, "SECOND CONTROLLER... "));
                yield return StartCoroutine(TrialStart(i, j, 1, testTypes[i], modelTypeSecond, seed, true, testObjects[i]));
                // Persist user preference.
                yield return StartCoroutine(DisplayPreferenceMenu(modelTypeFirst, modelTypeSecond));
            }
        }

        // Persist scores.
        string pathScores = Path.Combine("Data", "Sim", "scores_v3.csv");
        string lineScore = "" + _userID;
        for (int i = 0; i < _scores.Count; i++)
        {
            lineScore += "," + _scores[i].ToString();
            lineScore += "," + _scoresPossible[i].ToString();
        }
        lineScore += "\r\n";
        File.AppendAllText(pathScores, lineScore);

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

    private int MakeSeed()
    {
        return _rng.Next() % 65536;
    }

    private IEnumerator TrialStart(int testIndex, int trialIndex, int pairIndex, TestType testType, ModelType modelType, int seed, bool disableHead, GameObject[] gameObjects)
    {
        HighlightAtGaze.Score = 0;
        HighlightAtGaze.ScorePossible = 0;
        _score = 0;
        _scorePossible = 0;

        _historyGazeL = new List<Vector3>();
        _historyGazeR = new List<Vector3>();
        _historyForward = new List<Vector3>();
        _historyCamera = new List<Quaternion>();
        _historyObjects = new List<List<Vector3>>();

        // Start trial.
        if (seed != -1)
        {
            UnityEngine.Random.InitState(seed); // Make trials equivalent within the same task.
        }
        foreach (GameObject gameObject in gameObjects)
        {
            gameObject.SetActive(true);
        }
        _testType = testType;
        _modelType = modelType;
        DisableHeadTracking.Disable = disableHead;
        ResetModel();
        Invoke(nameof(EyeTrackerMeasurement), 0f);

        // Run for some number of seconds.
        float gameTime = Time.time;
        while (Time.time - gameTime < SecondsTrial)
        {
            yield return null;
        }

        if (_testType == TestType.RapidMovement || _testType == TestType.RapidAvoid)
        {
            _score = HighlightAtGaze.Score;
            _scorePossible = HighlightAtGaze.ScorePossible;
        }
        _scores.Add(_score);
        _scoresPossible.Add(_scorePossible);

        // Write user data.
        string fname = "Trial" + testIndex + "_";
        if (trialIndex == -1)
        {
            fname += "p";
        }
        else
        {
            fname += trialIndex + "_" + pairIndex;
        }
        fname += ".csv";
        string pathData = Path.Combine(_pathUserFolder, fname);
        File.WriteAllText(pathData, "eye_l_x,eye_l_y,eye_l_z,eye_r_x,eye_r_y,eye_r_z,head_x,head_y,head_z,camera_w,camera_x,camera_y,camera_z\r\n");
        for (int i = 0; i < _historyGazeL.Count; i++)
        {
            Vector3 eyeL = _historyGazeL[i];
            Vector3 eyeR = _historyGazeR[i];
            Vector3 head = _historyForward[i];
            Quaternion cameraQ = _historyCamera[i];
            string s = "" + eyeL.x + "," + eyeL.y + "," + eyeL.z +
                "," + eyeR.x + "," + eyeR.y + "," + eyeR.z +
                "," + head.x + "," + head.y + "," + head.z +
                "," + cameraQ.w + "," + cameraQ.x + "," + cameraQ.y + "," + cameraQ.z + "\r\n";
            File.AppendAllText(pathData, s);
        }

        // Write object data.
        if (_historyObjects.Count > 0)
        {
            string pathObjectData = Path.Combine(_pathUserFolder, "Objects" + testIndex + "_" + trialIndex + ".csv");
            string header = "";
            for (int j = 0; j < _historyObjects[0].Count; j++)
            {
                if (j > 0)
                {
                    header += ",";
                }
                header += "obj" + j + "_x,obj" + j + "_y,obj" + j + "_z";
            }
            header += "\r\n";
            File.WriteAllText(pathObjectData, header);
            for (int i = 0; i < _historyObjects.Count; i++)
            {
                List<Vector3> objects = _historyObjects[i];
                string s = "";
                for (int j = 0; j < objects.Count; j++)
                {
                    Vector3 obj = objects[j];
                    if (j > 0)
                    {
                        s += ",";
                    }
                    s += "" + obj.x + "," + obj.y + "," + obj.z;
                }
                s += "\r\n";
                File.AppendAllText(pathObjectData, s);
            }
        }

        // End trial.
        foreach (GameObject gameObject in gameObjects)
        {
            gameObject.SetActive(false);
        }
        _modelType = ModelType.None;
        _testType = TestType.None;
        _outputLastPitch = 0.0f;
        _outputLastYaw = 0.0f;
        DisableHeadTracking.Disable = false;
        ResetHead();
        EyeTrackerRelease();
    }

    private IEnumerator DisplayPreferenceMenu(ModelType modelTypeFirst, ModelType modelTypeSecond)
    {
        PreferenceCanvas.enabled = true;
        PreferenceCanvas.gameObject.SetActive(true);

        while (!_firstClicked && !_secondClicked && !_noPreferenceClicked)
        {
            yield return null;
        }
        if (_firstClicked || _secondClicked)
        {
            _modelTypePreferred = (_firstClicked) ? modelTypeFirst : modelTypeSecond;
            File.AppendAllText(_pathUsers, "," + _modelTypePreferred.ToString());
        }
        else
        {
            _modelTypePreferred = (_rng.Next() % 2 == 0) ? modelTypeFirst : modelTypeSecond;
            File.AppendAllText(_pathUsers, ",NoPreference");
        }
        _firstClicked = false;
        _secondClicked = false;
        _noPreferenceClicked = false;

        PreferenceCanvas.gameObject.SetActive(false);
        PreferenceCanvas.enabled = false;
    }
}

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
    public NNModel modelAsset;
    public bool useModel;

    private static Model m_RuntimeModel;
    private static Transform player;

    
    private static long MeasureTime;
    private static Vector3 gaze_direct_L, gaze_direct_R, forward;
    private static Tensor input, hn, cn;

    // Start is called before the first frame update
    void Start()
    {
        player = Camera.main.transform.parent.parent;
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        hn = new Tensor(1, 1, 9, 1);
        cn = new Tensor(1, 1, 9, 1);
        input = new Tensor(1, 1, 9, 1);
    }

    // Update is called once per frame
    void Update()
    {
        GetData();

        if (useModel)
        {
            ModelCall();
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

        var worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
        worker.Execute(Inputs);
        var output = worker.PeekOutput("output");
        hn = worker.PeekOutput("hn");
        cn = worker.PeekOutput("cn");

        var new_forward = new Vector3(output[0,0,0,0], output[0,0,0,1], output[0,0,0,2]);
        new_forward.Normalize();
        UnityEngine.Debug.Log(new_forward.ToString());

        Quaternion rotation = Quaternion.LookRotation(new_forward);
        player.rotation = rotation;

        worker.Dispose();
    }

    /// <summary>
    /// Callback function to record the eye movement data.
    /// Note that SRanipal_Eye_v2 does not work in the function below. It only works under UnityEngine.
    /// </summary>
    private static void GetData()
    {
        
        EyeData_v2 eyeData = new EyeData_v2();
        Error error = SRanipal_Eye_API.GetEyeData_v2(ref eyeData);
        if (error == ViveSR.Error.WORK)
        {
            gaze_direct_L = eyeData.verbose_data.left.gaze_direction_normalized;
            gaze_direct_R = eyeData.verbose_data.right.gaze_direction_normalized;

            gaze_direct_L.x = gaze_direct_L.x * -1;
            gaze_direct_R.x = gaze_direct_R.x * -1;


        }
        
        forward = player.forward;
    }
}

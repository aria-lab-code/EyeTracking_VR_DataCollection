using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableHeadTracking : MonoBehaviour
{

    public GameObject simulation;
    private ModelSim modelSim;

    void Start()
    {
        modelSim = simulation.GetComponent<ModelSim>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        Transform camera = Camera.main.transform;
        Transform camParent = camera.parent;
        Transform player = camParent.parent;

        bool testing = modelSim.testing;
        if (testing)
        {
            camParent.localRotation = Quaternion.Inverse(camera.localRotation);
        }
        
    }
}

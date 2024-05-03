using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableHeadTracking : MonoBehaviour
{


    void Start()
    {

    }

    // Update is called once per frame
    void LateUpdate()
    {
        Transform camera = Camera.main.transform;
        Transform camParent = camera.parent;
        Transform player = camParent.parent;

        bool testing = ModelSim.testing;
        if (testing)
        {
            camParent.localRotation = Quaternion.Inverse(camera.localRotation);
        }
        
    }
}

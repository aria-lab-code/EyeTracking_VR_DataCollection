using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableHeadTracking : MonoBehaviour
{
    public static bool Disable = false;

    void Start()
    {

    }

    // Update is called once per frame
    void LateUpdate()
    {
        Transform camera = Camera.main.transform;
        Transform camParent = camera.parent;

        if (Disable)
        {
            camParent.localRotation = Quaternion.Inverse(camera.localRotation);
        }
    }
}

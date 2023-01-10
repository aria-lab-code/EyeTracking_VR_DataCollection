using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableHeadTracking : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        Camera.main.transform.parent.rotation = Quaternion.Inverse(Camera.main.transform.localRotation);
    }
}

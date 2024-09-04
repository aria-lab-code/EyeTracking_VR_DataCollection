using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableHeadTracking : MonoBehaviour
{
    public static bool Disable = false;

    private static Transform _camera;
    private static Transform _camParent;
    private static Quaternion _camParentLocalRotationStart;

    void Start()
    {
        _camera = Camera.main.transform;
        _camParent = _camera.parent;
        _camParentLocalRotationStart = _camParent.localRotation;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (Disable)
        {
            _camParent.localRotation = Quaternion.Inverse(_camera.localRotation);
        }
    }

    public static void ResetHead()
    {
        _camParent.localRotation = _camParentLocalRotationStart;
    }
}

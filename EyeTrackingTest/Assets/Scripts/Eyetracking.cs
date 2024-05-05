using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[RequireComponent(typeof(LineRenderer))]
public class Eyetracking : MonoBehaviour
{

    [SerializeField]
    private float rayDistance = 1.0f;

    [SerializeField]
    private float rayWidth = 0.01f;

    [SerializeField]
    private LayerMask layersToInclude;

    [SerializeField]
    private Color rayColor = Color.yellow;

    private LineRenderer lineRenderer;
    private List<EyeInteractable> eyeInteractable = new List<EyeInteractable>();
    // Start is called before the first frame update
    void Start()
    {
        //Set ray
        lineRenderer = GetComponent<LineRenderer>();
        SetupRay();
    }
    void SetupRay()
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth;
        lineRenderer.startColor = rayColor;
        lineRenderer.endColor = rayColor;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, new Vector3(transform.position.x, transform.position.y, transform.position.z + rayDistance));
    }

    private void FixedUpdate()
    {
        RaycastHit hit;
        Vector3 rayCastDirection = transform.TransformDirection(Vector3.forward) * rayDistance;
        //if (Physics.Raycast(transform.position, rayCastDirection, out hit, Mathf.Infinity, layersToInclude))
        //{
        // }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}

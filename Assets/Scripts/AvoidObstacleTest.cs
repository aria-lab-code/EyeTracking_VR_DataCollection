using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvoidObstacleTest : MonoBehaviour
{
    private static readonly int _baseColor = Shader.PropertyToID("_BaseColor");
    public Color highlightColor = Color.red;
    public float animationTime = 0.1f;


    private Renderer _renderer;
    private Color _originalColor = Color.yellow;
    private Color _targetColor;
    public float speed = 2;
    private float x;
    private float y;
    private float z;
    private Vector3 initPos;
    private float focusTime;
    private bool focused;
    private float startTime;
    private bool init = true;
    public Transform camera;

    //The method of the "IGazeFocusable" interface, which will be called when this object receives or loses focus
    public void GazeFocusChanged(bool hasFocus)
    {

        //If this object received focus, fade the object's color to highlight color
        if (hasFocus)
        {
            _targetColor = highlightColor;
        }
        //If this object lost focus, fade the object's color to it's original color
        else
        {
            _targetColor = _originalColor;
        }
        checkFocus(hasFocus);
    }


    private void checkFocus(bool newFocus)
    {
        if (newFocus != focused)
        {
            focused = newFocus;
            if (focused)
            {
                focusTime = Time.time;
            }
        }
    }

    void OnEnable()
    {
        startTime = Time.time;
        focusTime = Time.time;

        focused = false;
        _renderer = GetComponent<Renderer>();

        if (init)
        {
            _originalColor = _renderer.material.color;
        }
        _targetColor = _originalColor;
        _renderer.material.color = _targetColor;
        System.Random random = new System.Random();

        float xMax = 20 * Mathf.Sqrt(3);
        x = UnityEngine.Random.Range(-xMax, xMax);

        y = UnityEngine.Random.Range(-xMax, xMax);

        initPos = new Vector3(x, y, 20);
        transform.position = initPos;

        transform.rotation = Quaternion.LookRotation(-1 * initPos);
        init = false;

    }

    private void Update()
    {
        //transform.Translate(Vector3.forward * speed * Time.deltaTime);
        //This lerp will fade the color of the object
        transform.position += -3 * initPos / 20 * Time.deltaTime;
        if (_renderer.material.HasProperty(_baseColor)) // new rendering pipeline (lightweight, hd, universal...)
        {
            _renderer.material.SetColor(_baseColor, Color.Lerp(_renderer.material.GetColor(_baseColor), _targetColor, Time.deltaTime * (1 / animationTime)));
        }
        else // old standard rendering pipline
        {
            _renderer.material.color = Color.Lerp(_renderer.material.color, _targetColor, Time.deltaTime * (1 / animationTime));
        }
        if (Time.time - startTime > 7|| getDistance() < 3)
        {
            OnEnable();

        }
    }
    private float getDistance()
    {
        float dist = Vector3.Distance(_renderer.transform.position, camera.position);
        return dist;
    }

}

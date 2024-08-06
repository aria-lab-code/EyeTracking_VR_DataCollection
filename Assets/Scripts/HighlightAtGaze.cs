// Copyright © 2018 – Property of Tobii AB (publ) - All Rights Reserved

using System;
using UnityEngine;

    //Monobehaviour which implements the "IGazeFocusable" interface, meaning it will be called on when the object receives focus
public class HighlightAtGaze : MonoBehaviour
{
    private static readonly int _baseColor = Shader.PropertyToID("_BaseColor");
    public Color highlightColor = Color.green;
    public float animationTime = 0.1f;

    public GameObject[] _otherObject;
    private Renderer _renderer;
    private Color _originalColor = Color.blue;
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

    //The method of the "IGazeFocusable" interface, which will be called when this object receives or loses focus
    public void GazeFocusChanged(bool hasFocus)
    {
        // If this object received focus, fade the object's color to highlight color
        if (hasFocus)
        {
            _targetColor = highlightColor;
        }
        // If this object lost focus, fade the object's color to it's original color
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
        if (focused)
        {
            GazeCollection2.score++; // TODO: That's kinda janky, but w/e.
            GazeCollectionWithModel.score++;
        }
        focused = false;
        _renderer = GetComponent<Renderer>();
        GazeCollection2.total_score++;
        GazeCollectionWithModel.total_score++;

        if (init)
        {
            _originalColor = _renderer.material.color;
        }
        _targetColor = _originalColor;
        _renderer.material.color = _targetColor;
        System.Random random = new System.Random();

        float xMax = 20 * Mathf.Sqrt(3);
        bool generated = false;
        while (!generated)
        {
            x = UnityEngine.Random.Range(-xMax, xMax);
            y = UnityEngine.Random.Range(-xMax, xMax);
            generated = true;
            foreach (GameObject cube in _otherObject)
            {
                if (Mathf.Pow(cube.transform.position.x - x, 2f) + Mathf.Pow(cube.transform.position.y - y, 2) < 2)
                {
                    generated = false;
                    break;
                }
            }
        }
        initPos = new Vector3(x, y, 20);
        transform.position = initPos;

        transform.rotation = Quaternion.LookRotation(-1 * initPos);
        init = false;
    }

    private void Update()
    {
        //transform.Translate(Vector3.forward * speed * Time.deltaTime);
        // This lerp will fade the color of the object
        transform.position += -3 * initPos / 20 * Time.deltaTime;
        if (_renderer.material.HasProperty(_baseColor)) // new rendering pipeline (lightweight, hd, universal...)
        {
            _renderer.material.SetColor(_baseColor, Color.Lerp(_renderer.material.GetColor(_baseColor), _targetColor, Time.deltaTime * (1 / animationTime)));
        }
        else // old standard rendering pipline
        {
            _renderer.material.color = Color.Lerp(_renderer.material.color, _targetColor, Time.deltaTime * (1 / animationTime));
        }
        if (Time.time - focusTime > 0.3 && focused || Time.time - startTime > 7)
        {
            OnEnable();
        }
    }
}


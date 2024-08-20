using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothPursuitLinear : MonoBehaviour
{
    private float x;
    private float y;
    private float max;
    private bool atNextPos;
    private Vector3 nextPos;

    private float speed;

    private Renderer _renderer;
    private Color _originalColor = Color.blue;
    private Color _targetColor;

    public Color highlightColor = Color.green;
    private bool init = true;

    private bool focused;
    // Start is called before the first frame update
    private GazeCollection2 mainScript;

    void OnEnable()
    {
        speed = 5;
        max = 15;//10 * Mathf.Sqrt(3);

        transform.position = new Vector3(0, 5, 10);
        
        x = UnityEngine.Random.Range(-max, max);
        y = UnityEngine.Random.Range(-max, max);

        UnityEngine.Debug.Log($"{x},{y}");
        nextPos = new Vector3(x, y, 10);

        atNextPos = false;
        init = false;
    }

    void ColorUpdate()
    {
        _renderer = GetComponent<Renderer>();
        if (init)
        {
            _originalColor = _renderer.material.color;
        }
        _renderer.material.color = _targetColor;
        init = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (atNextPos)
        {
            x = UnityEngine.Random.Range(-max, max);
            y = UnityEngine.Random.Range(-max, max);
            nextPos = new Vector3(x, y, 10);
            atNextPos = false;

        }

        var step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, nextPos, step);

        if (Vector3.Distance(transform.position, nextPos) < .001f)
        {
            atNextPos = true;
        }
        
        ColorUpdate();
    }

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
        }
    }
}

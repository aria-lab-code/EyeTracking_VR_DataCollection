// // Copyright © 2018 – Property of Tobii AB (publ) - All Rights Reserved

// using Tobii.G2OM;
// using UnityEngine;

// namespace Tobii.XR.Examples.GettingStarted
// {
//     //Monobehaviour which implements the "IGazeFocusable" interface, meaning it will be called on when the object receives focus
//     public class HighlightAtGaze : MonoBehaviour, IGazeFocusable
//     {
//         // private static readonly int _baseColor = Shader.PropertyToID("_BaseColor");
//         // public Color highlightColor = Color.red;
//         // public float animationTime = 0.1f;

//         // private Renderer _renderer;
//         // private Color _originalColor;
//         // private Color _targetColor;
//         // public float speed = 2;
//         // private float x;
//         // private float y;
//         // private float z;
//         // private Vector3 initPos;
//         // private float focusTime;
//         // private bool focused;
//         // private float startTime;
//         // private static int countDestroyed = 0;
//         // private bool init = true;
//         // private float gameTime;

//         // //The method of the "IGazeFocusable" interface, which will be called when this object receives or loses focus
//         // public void GazeFocusChanged(bool hasFocus)
//         // {
//         //     //If this object received focus, fade the object's color to highlight color
//         //     if (hasFocus)
//         //     {
//         //         _targetColor = highlightColor;
//         //         focusTime = Time.time;
//         //         focused = true;
//         //     }
//         //     //If this object lost focus, fade the object's color to it's original color
//         //     else
//         //     {
//         //         _targetColor = _originalColor;
//         //         focused = false;
//         //     }
//         // }

//         // private void Start()
//         // {
//         //     startTime = Time.time;
//         //     focusTime = Time.time;
//         //     focused=false;
//         //     _renderer = GetComponent<Renderer>();
//         //     if (init) {
//         //         _originalColor = _renderer.material.color;
//         //         gameTime = Time.time;
//         //     }
//         //     _targetColor = _originalColor;
//         //     _renderer.material.color = _targetColor;
//         //     System.Random random = new System.Random();

//         //     float xMax = 20 * Mathf.Sqrt(3);
//         //     x = Random.Range(-xMax, xMax);

//         //     y = Random.Range(-xMax, xMax);

//         //     initPos = new Vector3(x, y, 20);
//         //     transform.position = initPos;

//         //     transform.rotation = Quaternion.LookRotation(-1 * initPos);
//         //     init = false;

//         // }

//         // private void Update()
//         // {
//         //     //transform.Translate(Vector3.forward * speed * Time.deltaTime);
//         //     //This lerp will fade the color of the object
//         //     transform.position += -3 * initPos / 20 * Time.deltaTime;
//         //     if (_renderer.material.HasProperty(_baseColor)) // new rendering pipeline (lightweight, hd, universal...)
//         //     {
//         //         _renderer.material.SetColor(_baseColor, Color.Lerp(_renderer.material.GetColor(_baseColor), _targetColor, Time.deltaTime * (1 / animationTime)));
//         //     }
//         //     else // old standard rendering pipline
//         //     {
//         //         _renderer.material.color = Color.Lerp(_renderer.material.color, _targetColor, Time.deltaTime * (1 / animationTime));
//         //     }
//         //     if (Time.time - focusTime > 0.3 && focused) {
//         //         Start();
//         //         countDestroyed += 1;
//         //     }
//         //     if (Time.time - startTime > 7) {
//         //         Start();
//         //     }
//         //     if (Time.time - gameTime > 60) {
//         //         UnityEditor.EditorApplication.isPlaying = false;
//         //     }
//         // }
//     }
// }

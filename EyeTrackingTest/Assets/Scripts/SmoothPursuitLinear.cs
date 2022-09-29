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

    // Start is called before the first frame update
    void OnEnable()
    {
        speed = 5;
        max = 10 * Mathf.Sqrt(3);
        
        transform.position = new Vector3(0, 5, 10);

        x = Random.Range(-max, max);
        y = Random.Range(-max, max);

        nextPos = new Vector3(x,y,10);

        atNextPos = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (atNextPos)
        {
            x = Random.Range(-max, max);
            y = Random.Range(-max, max);
            nextPos = new Vector3(x,y,10);
            atNextPos = false;
        }

        var step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, nextPos, step);

        if (Vector3.Distance(transform.position, nextPos) < .001f)
        {
            atNextPos = true;
        }
    }
}

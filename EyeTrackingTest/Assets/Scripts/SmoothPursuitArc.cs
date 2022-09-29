using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothPursuitArc : MonoBehaviour
{

    private float x;
    private float y;
    private float max;
    private Vector3 initPos;
    private bool atNextPos;
    private Vector3 nextPos;
    private Vector3 intersection1;
    private Vector3 intersection2;
    private float r;
    private float angle;
    private float dir;
    private Vector3 center;
    private System.Random ran;

    private float speed;

    // Start is called before the first frame update
    void OnEnable()
    {
        ran = new System.Random();
        speed = 1f;
        transform.position = new Vector3(0, 5, 10);
        max = 10 * Mathf.Sqrt(3);
        atNextPos = false;

        GetCenter();
        GetNextPosition();
        GetDir();
        GetAngle();
    }

    void GetDir()
    {
        int rand = ran.Next(0, 2);
        if (rand == 0)
        {
            dir = 1;
        }
        else
        {
            dir = -1;
        }
    }

    void GetCenter()
    {
        float minValX = Mathf.Max(-max, transform.position.x - 7);
        float maxValX = Mathf.Min(max, transform.position.x + 7);
        float minValY = Mathf.Max(-max, transform.position.y - 7);
        float maxValY = Mathf.Min(max, transform.position.y + 7);
        x = Random.Range(minValX, maxValX);
        y = Random.Range(minValY, maxValY);

        center = new Vector3(x, y, 10);
        r = Vector3.Distance(transform.position, center);
    }

    void GetNextPosition()
    {
        float minVal = Mathf.Max(-max, center.x - r);
        float maxVal = Mathf.Min(max, center.x + r);
        x = Random.Range(minVal, maxVal);
        int rand = ran.Next(0, 2);
        var val = 1;
        if (rand == 0)
        {
            val = -1;
        }
        y = val * Mathf.Sqrt((r*r) - ((x - center.x)*(x - center.x))) + center.y;
        
        
        nextPos = new Vector3(x, y, 10);
    }

    void GetAngle()
    {
        angle = Mathf.Atan2(transform.position.y - center.y, transform.position.x - center.x);
    }

    // Update is called once per frame
    void Update()
    {
        if (atNextPos)
        {
            GetCenter();
            GetNextPosition();
            GetDir();
            GetAngle();
            atNextPos = false;
            UnityEngine.Debug.Log(transform.position.ToString() + nextPos.ToString() + center.ToString());
        }

        angle += speed * Time.deltaTime * dir;
        var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * r;
        transform.position = center + offset;
        if (Vector3.Distance(transform.position, nextPos) < .1f)
        {
            atNextPos = true;
        }
    }
}

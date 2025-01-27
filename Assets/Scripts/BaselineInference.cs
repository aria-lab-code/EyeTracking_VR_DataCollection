using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BaselineInference : MonoBehaviour
{
    private static string PATH_USERS = Path.Combine("..", "vr-exoskeleton", "data", "Users_90hz");
    private static string[] USER_IDS = new string[] { "10", "17", "1", "18", "16" }; // rng = np.default_rng(seed=23); idx = rng.permutation(20)[-5:];
    private static string[] TASKS = new string[] { "LinearSmoothPursuit", "ArcSmoothPursuit", "RapidVisualSearch", "RapidVisualSearchAvoidance" };
    private static int N_TRIALS = 3;

    private Transform _player;
    private Quaternion _playerRotationStart;

    private bool _firstFrame = true;

    public bool ErrorModeMSE;

    // Start is called before the first frame update
    void Start()
    {
        _player = Camera.main.transform.parent.parent;
        _playerRotationStart = _player.rotation;

        DisableHeadTracking.Disable = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (_firstFrame)
        {
            StartCoroutine(Inference());
            _firstFrame = false;
        }
    }

    private IEnumerator Inference()
    {
        float trialLossSum = 0.0f;
        int trialLossCount = 0;

        foreach (string userId in USER_IDS)
        {
            string pathUser = Path.Combine(PATH_USERS, "User" + userId);
            foreach (string task in TASKS)
            {
                for (int trial = 0; trial < N_TRIALS; trial++)
                {
                    _player.rotation = _playerRotationStart;
                    DisableHeadTracking.ResetHead();
                    yield return null;

                    float lossSum = 0.0f;
                    int lossCount = 0;

                    // Open data file.
                    string path = Path.Combine(pathUser, "User" + userId + "_" + task + "_" + trial + ".csv");
                    StreamReader reader = File.OpenText(path);
                    reader.ReadLine(); // Skip header (first row).
                    string line = reader.ReadLine();
                    var instanceNext = new List<Vector3>();
                    string[] items = line.Split(",");
                    for (int i = 1; i < 10; i += 3) // Skip time stamp (first column).
                    {
                        instanceNext.Add(new Vector3(float.Parse(items[i]), float.Parse(items[i + 1]), float.Parse(items[i + 2])));
                    }

                    while (true)
                    {
                        line = reader.ReadLine();
                        if (line == null || line == "")
                        {
                            break;
                        }

                        var instance = instanceNext;
                        _player.forward = instance[2];
                        yield return null;

                        instanceNext = new List<Vector3>();
                        items = line.Split(",");
                        for (int i = 1; i < 10; i += 3)
                        {
                            instanceNext.Add(new Vector3(float.Parse(items[i]), float.Parse(items[i + 1]), float.Parse(items[i + 2])));
                        }

                        Vector3 gaze = (instance[0] + instance[1]).normalized;
                        Quaternion? pred = VectorBaseline(_player, gaze, _player.forward);
                        if (pred != null)
                        {
                            _player.rotation = (Quaternion)pred;
                            yield return null;
                        }

                        if (ErrorModeMSE)
                        {
                            var error = _player.forward - instanceNext[2];
                            lossSum += (error.x * error.x + error.y * error.y + error.z * error.z) / 3;
                        }
                        else
                        {
                            lossSum += Vector3.Angle(_player.forward, instanceNext[2]);
                        }
                        lossCount++;
                    }

                    float trialLoss = lossSum / lossCount;
                    Debug.LogWarning("User: " + userId + "; task: " + task + "; trial: " + trial + "; loss: " + trialLoss);
                    trialLossSum += trialLoss;
                    trialLossCount++;
                }
            }
        }

        float mse = trialLossSum / trialLossCount;
        Debug.LogWarning("MSE: " + mse);
    }

    /// <summary>
    /// Rotate the player object by finding vector between current forward direction and eye gaze
    /// direction. Rotate in direction of this vector.
    /// </summary>
    public static Quaternion? VectorBaseline(Transform player, Vector3 vecGaze, Vector3 vecForward)
    {
        // Compute

        // Colin Rubow: "1.767 is the average velocity proportion for the vector based controller.
        // It means, every 1 deg further a target is, the head should move 1.767 deg/s faster."
        float vectorVelocityProportion = 1.767f;

        float angle_boundary = 5.0f;  //boundary of eye angle
        //float rotate_speed = 4f;  //each rotate angle

        // eye angle in x direction > angle_boundary : rotate the 
        Vector3 gaze_direct_avg_world = player.rotation * vecGaze;

        var angle = Vector3.Angle(gaze_direct_avg_world, vecForward);
        var global_angle = Vector3.Angle(gaze_direct_avg_world, new Vector3(0, 0, 1));
        if ((angle > angle_boundary || angle < -1 * angle_boundary) && (global_angle < 70f && global_angle > -70f))
        {
            float rotate_speed = 0.25f * vectorVelocityProportion * angle;
            return Quaternion.Slerp(player.rotation, Quaternion.LookRotation(gaze_direct_avg_world), Time.deltaTime * rotate_speed);
        }

        return null;
    }
}

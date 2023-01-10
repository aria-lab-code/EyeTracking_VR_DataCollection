using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

public class ModelSim : MonoBehaviour
{
    public NNModel modelAsset;
    private Model m_RuntimeModel;


    private IWorker worker;
    
    // Start is called before the first frame update
    void Start()
    {
        m_RuntimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Compute, m_RuntimeModel);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

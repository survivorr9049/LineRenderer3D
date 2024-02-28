using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lag : MonoBehaviour
{
    public int lag;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < lag; i++){
            Debug.Log("test");
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tree : MonoBehaviour
{
    // Start is called before the first frame update\
    [SerializeField] LineRenderer3D lineRenderer;
    void Start()
    {
        lineRenderer.resolution = 8;
        Vector3[] array = GenerateTreeArray();
        //set to array
        lineRenderer.SetPoints(array, 0.4f);
        //manually call mesh generation since we don't modify it every frame
        lineRenderer.BeginGenerationAutoComplete();
    }
    Vector3[] GenerateTreeArray(){
        Vector3[] array = new Vector3[512];
        Vector3 direction = Vector3.forward;
        Vector3 position = Vector3.zero;
        Vector3 lastDirection = Vector3.forward;
        for(int i = 0; i < 512; i++){
            int random = Random.Range(0, 6);
            if(random == 0){
                direction = Vector3.up;
            }else if (random == 1){
                direction = Vector3.right;
            }else if (random == 2){
                direction = Vector3.forward;
            }else if (random == 3){
                direction = Vector3.left;
            }else if (random == 4){
                direction = Vector3.down;
            }else if (random == 5){
                direction = Vector3.back;
            }
            if (Vector3.Dot(lastDirection, direction) < 0) direction = -direction;
            position += direction;
            array[i] = position;
            lastDirection = direction;
        }
        return array;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}

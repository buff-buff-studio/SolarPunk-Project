using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class VideoCamera : MonoBehaviour
{
    public Vector3 positionA;
    public Vector3 positionB;
    public Transform camera;

    public float time = 0;
    public bool animating = true;
    
    public AnimationCurve curve;

    public void Update()
    {
        //get if a is pressed in new input system
        if(Keyboard.current.aKey.wasPressedThisFrame)
            animating = !animating;
        
        if(animating)
            time += Time.deltaTime;
        
        camera.position = Vector3.LerpUnclamped(positionA, positionB, curve.Evaluate(time));
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScreenScript : MonoBehaviour {

    public Canvas MainCanvas;
    public Canvas CommandCanvas;

    // This is the first script that is loaded when enabling the app. 
    void Start () {
        MainCanvas.enabled = true;
        CommandCanvas.enabled = false;
    }
	
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Screen : MonoBehaviour {

    public static Canvas MainCanvas;
    public static Canvas CommandCanvas;

    // Use this for initialization
    void Start () {
        MainCanvas = GameObject.Find("Menu").GetComponent<Canvas>();
        CommandCanvas = GameObject.Find("CommandMenu").GetComponent<Canvas>();
        MainCanvas.enabled = true;
        CommandCanvas.enabled = false;
    }
	
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour {

    private Dictionary<string, SelectPeripheral> _peripheralList;
    private bool _scanning = false;

    public Transform deviceContents;
    public BluetoothLE BLE;
    public GameObject PeripheralButton;
    public Text ButtonScanText;

    // Use this for initialization
    void Start () {
        
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    void RemovePeripherals()
    {
        for (int i = 0; i < deviceContents.childCount; ++i)
        {
            Destroy(deviceContents.GetChild(i).gameObject);
        }

        if (_peripheralList != null) _peripheralList.Clear();
    }

    void AddPeripheral(string name, string index)
    {
        if (_peripheralList == null) _peripheralList = new Dictionary<string, SelectPeripheral>();

        if (!_peripheralList.ContainsKey(index))
        {
            var peripheralObject = Instantiate(PeripheralButton);
            var peripheralButton = peripheralObject.GetComponent<SelectPeripheral>();
            peripheralButton.TextName.text = name;
            peripheralButton.TextAddress.text = index;
            peripheralButton.Connect = BLE;
            peripheralObject.transform.SetParent(deviceContents);
            peripheralObject.transform.localScale = new Vector3(1f, 1f, 1f);
            _peripheralList[index] = peripheralButton;
        }
    }
}

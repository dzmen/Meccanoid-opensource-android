using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class MainMenuScript : MonoBehaviour {

    //private Dictionary<string, SelectPeripheral> _peripheralList;
    private bool _scanning = false;

    public BluetoothLE BLE;

    public Transform deviceList;
    public GameObject deviceButton;

    public Text ButtonScanText;
    public Canvas MainCanvas;
    public Canvas CommandCanvas;

    public List<string> deviceFoundList;
    private List<string> deviceFoundListCheck;

    // Use this for initialization
    void Start () {
        this.BLE.deviceFoundCallback = new Action<List<string>>(this.deviceFoundCallback);
        this.BLE.connectCallback = new Action<string>(this.connectCallback);
    }
	 
	// Update is called once per frame
	void Update () {

    }

    public void OnScan()
    {
        if (_scanning)
        {
            BLE.StopScan();
            ButtonScanText.text = "Scan";
            _scanning = false;
        }
        else
        {
            RemoveDeviceButtons();
            BLE.Scan();
            Debug.Log("BluetoothLE Scan Start");
            ButtonScanText.text = "Stop Scan";
            _scanning = true;
        }
    }

    private void deviceFoundCallback(List<string> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            this.AddDeviceButton(list[i], "", i);
        }
        Debug.Log("BLEManager deviceFoundCallback called");
    }

    private void connectCallback(string name)
    {
        //When connected, enable command screen
        MainCanvas.enabled = false;
        CommandCanvas.enabled = true;
    }

    void RemoveDeviceButtons()
    {
        //Remove all device buttons
        for (int i = 0; i < deviceList.childCount; ++i)
        {
            Destroy(deviceList.GetChild(i).gameObject);
        }
        //Empty devicelist check
        if (deviceFoundListCheck != null) deviceFoundListCheck.Clear();
    }

    void AddDeviceButton(string name,string uuid, int index)
    {

        if (deviceFoundListCheck != null) deviceFoundListCheck = new List<string>();

        //Check if button already exist
        //if (!deviceFoundListCheck.Contains(name))
        //{
            GameObject createButton = Instantiate(deviceButton) as GameObject;
            deviceButtonScript buttonScript = createButton.GetComponent<deviceButtonScript>();
            Button buttonCtrl = createButton.GetComponent<Button>();
            buttonScript.nameLabel.text = name;
            buttonScript.addressLabel.text = uuid;
            buttonScript.deviceID = index;

            buttonCtrl.onClick.AddListener(() => onDeviceClick(index));
            createButton.transform.SetParent(deviceList);
    
            deviceFoundListCheck.Add(uuid);
        //}

    }

    void onDeviceClick(int i)
    {
        if (_scanning)
        {
            OnScan();
        }
        BLE.Connect(i);
    }
}

using System;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuScript : MonoBehaviour {

    //private Dictionary<string, SelectPeripheral> _peripheralList;
    private bool _scanning = false;

    public BluetoothLE BLE;

    public Transform deviceList;
    public GameObject deviceButton;

    public Text ButtonScanText;

    // Use this for initialization
    void Start () {
        for (int i = 0; i < 15; i++)
        {
            this.AddDeviceButton("test"+i,"testadress"+i,i);
        }
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

    void RemoveDeviceButtons()
    {
        for (int i = 0; i < deviceList.childCount; ++i)
        {
            Destroy(deviceList.GetChild(i).gameObject);
        }

        //if (_peripheralList != null) _peripheralList.Clear();
    }

    void AddDeviceButton(string name,string uuid, int index)
    {
        GameObject createButton = Instantiate(deviceButton) as GameObject;
        deviceButtonScript buttonScript = createButton.GetComponent<deviceButtonScript>();
        buttonScript.nameLabel.text = name;
        buttonScript.addressLabel.text = uuid;
        buttonScript.deviceID = index;

        createButton.transform.SetParent(deviceList);
    }
}

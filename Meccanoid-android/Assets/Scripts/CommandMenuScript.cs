using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CommandMenuScript : MonoBehaviour {

    public BluetoothLE BLE;
    public InputField Iname;

    public Canvas MainCanvas;
    public Canvas CommandCanvas;

    private int preset = 1;

	// Use this for initialization
	void Start () {

	}

    public void onGetName() {
        Iname.text = BLE.meccaName;
    }

    public void onChangePreset(int value)
    {
        this.preset = value+1;
    }

    public void onPlayPreset()
    {
        byte bpreset = byte.Parse(this.preset.ToString());
        BLE.playPreset(bpreset, true);
    }

    /*
    TODO: Make a nice explain page

    Below you see BLE commands and how to use them. Lot of testing en trying.

    ==========================================================
    COMMAND: BLE.playPreset(preset number, boolean if you want to play instant of later(not sure how to play later));

    Available preset(still working on the list):
    1: Welcome
    2: High-five
    3: Telling a joke
    4: Follow a person
    5: Doing excercise
    6: Doing a dance
    7: Doing kung fu
    8: ????
    9: Wisper (Is only enabled while bluetooth is connected)
    10: Test hardware and give battery status
    11: Set custom name with your own voice
    12: Reset custom name to default name
    13: Record sound (I dont know for what :/????)
    14: Move backwards
    15: ?????
    16: ?????
    17: ?????
    18: Gives the local time
    19: ?????
    ==========================================================

    */

    public void onDisconnect()
    {
        BLE.Disconnect();
        MainCanvas.enabled = true;
        CommandCanvas.enabled = false;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class SelectPeripheral : MonoBehaviour
{ 
    public BluetoothLE Connect;
    public Text TextName;
    public Text TextAddress;

    public void OnPeripheralSelected()
    {
        if (TextName.text.ToLower().Contains("meccanoid"))
        {
            Connect.Connect(int.Parse(TextAddress.ToString()));
            Screen.MainCanvas.enabled = false;
            Screen.CommandCanvas.enabled = true;
        }
    }
}

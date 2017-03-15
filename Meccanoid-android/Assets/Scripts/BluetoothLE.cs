using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class BluetoothLE : MonoBehaviour
{
    public const int kServoModulesMax = 4;

    public const int kServoSlotsMax = 4;

    private const int framesPerSecond = 20;

    public bool debugOutput;

    public bool jDebug;

    private bool ack;

    private byte maxSpeed = 180;

    private float updateInterval = 0.05f;

    private float lastInterval;

    private int frames;

    private int interleave;

    public float[] initial = new float[8];

    public bool initialSet;

    private static BluetoothDeviceScript bluetoothDeviceScript;

    public byte[] actualPos = new byte[] { 127, 127, 127, 127, 127, 127, 127, 127 };

    public byte[] targetPos = new byte[] { 127, 127, 127, 127, 127, 127, 127, 127 };

    public byte[] status = new byte[16];

    public byte[] config = new byte[32];

    public byte[] mapping = new byte[16];

    public bool config0flag;

    public bool config1flag;

    public bool config2flag;

    public bool config3flag;

    public bool timeDateflag;

    public bool nameflag;

    private float[] batteryLevels = new float[] { 10f, 25f, 50f, 75f, 100f };

    private static bool connectState;

    private static bool connectDataState;

    private static bool connecting;

    private static bool writeState;

    private static bool pendingWriteState;

    private static bool motorState;

    private static bool updateState;

    private string serviceUUID = "FFF0";

    private string characteristicUUID = "FFF2";

    private string subscribeUUID = "FFF1";

    public string timeDate = string.Empty;

    public string meccaName = string.Empty;

    private static bool created;

    private static bool scanning;

    private bool[] motors = new bool[4];

    public Action<List<string>> deviceFoundCallback;

    public Action<string> connectCallback;

    public Action<string> connectDataCallback;

    public Action<string> disconnectCallback;

    public int connectedDevice = -1;

    public byte robotType;

    private byte prevButtons;

    public bool meccaBlueButton;

    public bool meccaRedButton;

    public bool meccaGreenButton;

    public bool meccaYellowButton;

    public Action<bool, bool, bool, bool> OnMeccaBrainButtonsChanged;

    private List<string> names = new List<string>();

    private List<string> uuids = new List<string>();

    public Action<byte[]> resultsCallback;

    public Action getServoPosCallback;

    public Action<Color> getLEDColorCallback;

    public Action<byte, float, string> getLIMInfoCallback;

    private List<byte[]> commandQueue = new List<byte[]>();

    public int motorOutputSpeed = 255;

    public bool waitingForSequenceFeedback;

    private bool m_checkingForConfigError;

    private bool m_checkingForStatus;

    public static BluetoothLE instance;

    private bool m_skipTimeoutDisconnect;

    private bool m_appPaused;

    public bool robotEverSet;

    public static int currentRobot
    {
        get
        {
            return BluetoothLE.instance.robotType;
        }
    }

    public byte MaxSpeed
    {
        get
        {
            return this.maxSpeed;
        }
        set
        {
            this.maxSpeed = value;
        }
    }

    static BluetoothLE()
    {
        BluetoothLE.motorState = true;
    }

    public BluetoothLE()
    {
    }

    public byte adjust(byte actual, byte target)
    {
        if (Mathf.Abs((int)(actual - target)) <= this.maxSpeed)
        {
            return target;
        }
        return (byte)((actual >= target ? actual - this.maxSpeed : actual + this.maxSpeed));
    }

    private void Awake()
    {
        if (this.debugOutput)
        {
            Debug.Log("BluetoothLE.Awake called");
        }
        if (BluetoothLE.created)
        {
            UnityEngine.Object.Destroy(base.gameObject);
        }
        else
        {
            UnityEngine.Object.DontDestroyOnLoad(this);
            BluetoothLE.bluetoothDeviceScript = BluetoothLEHardwareInterface.Initialize(true, false, new Action(this.Initialized), new Action<string>(this.Error));
            if (null != BluetoothLE.bluetoothDeviceScript)
            {
                UnityEngine.Object.DontDestroyOnLoad(BluetoothLE.bluetoothDeviceScript);
                BluetoothLE.bluetoothDeviceScript.DisconnectedPeripheralAction = new Action<string>(this.disconnectedPeripheralAction);
            }
            BluetoothLE.created = true;
            if (this.debugOutput)
            {
                Debug.Log("BluetoothLE.Awake initialized");
            }
            BluetoothLE.instance = this;
        }
        Array.Clear(this.status, 0, (int)this.status.Length);
        Array.Clear(this.config, 0, (int)this.config.Length);
        Array.Clear(this.mapping, 0, (int)this.mapping.Length);
    }

    public void calculateChecksum(ref byte[] data)
    {
        int num = 0;
        for (int i = 0; i < (int)data.Length - 2; i++)
        {
            num = num + data[i];
        }
        data[(int)data.Length - 2] = (byte)((num & 65280) >> 8);
        data[(int)data.Length - 1] = (byte)(num & 255);
    }

    public void changeLIMName(byte index, string name)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] num = new byte[15];
        for (int i = 0; i < Math.Min(name.Length, (int)num.Length); i++)
        {
            num[i] = Convert.ToByte(name[i]);
        }
        byte[] numArray = new byte[] { 20, index, 0, num[0], num[1], num[2], num[3], num[4], num[5], num[6], num[7], num[8], num[9], num[10], num[11], num[12], num[13], num[14], 0, 0 };
        this.calculateChecksum(ref numArray);
        this.commandQueue.Add(numArray);
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("renameLIM(", index, ") called"));
        }
    }

    public void Characteristic(string a, string b, string c)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new string[] { "*** BLE Characteristic: ", a, ", ", b, ", ", c }));
        }
        if (this.isMatch(b, this.serviceUUID) && this.isMatch(c, this.subscribeUUID))
        {
            this.Subscribe();
        }
    }

    public void CheckForConfigError()
    {
        if (this.m_checkingForStatus)
        {
            if (this.jDebug)
            {
                Debug.Log("checking for config error already");
            }
            return;
        }
        if (this.jDebug)
        {
            Debug.Log("new config check");
        }
        this.m_checkingForStatus = true;
        this.m_checkingForConfigError = true;
        this.SendCommand(2);
        this.SendCommand(3);
    }

    public void Clear()
    {
        Debug.Log("BLE Clear before scan");
        this.names.Clear();
        this.uuids.Clear();
    }

    public void ClearConnectedDevice()
    {
        this.connectedDevice = -1;
    }

    private void Completed()
    {
        if (this.connectCallback != null)
        {
            this.connectCallback(this.uuids[this.connectedDevice]);
        }
        this.setTimeDate();
        this.getServoPos();
        this.requestStatus();
        this.getName();
        this.setWriteState(false, true);
    }

    public void Connect(int index)
    {
        this.StopScan();
        this.connectedDevice = index;
        BluetoothLE.connectDataState = false;
        BluetoothLE.connectState = false;
        BluetoothLE.connecting = true;
        if (this.jDebug)
        {
            Debug.Log(string.Concat("connecting to ", index));
        }
        if (this.jDebug)
        {
            Debug.Log(this.uuids[this.connectedDevice]);
        }
        BluetoothLEHardwareInterface.ConnectToPeripheral(this.uuids[this.connectedDevice], new Action<string>(this.Connected), new Action<string, string>(this.Service), new Action<string, string, string>(this.Characteristic), null);
        base.Invoke("timeOut", 30f);
    }

    public void Connected(string msg)
    {
        if (this.debugOutput || this.jDebug)
        {
            Debug.Log(string.Concat("*** BLE Connected: ", msg));
        }
    }

    public void deleteLIM(byte index, string name = "")
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 23;
        numArray[1] = index;
        byte[] numArray1 = numArray;
        char[] charArray = name.ToCharArray();
        for (int i = 0; i < (int)charArray.Length; i++)
        {
            numArray1[i + 3] = (byte)charArray[i];
        }
        this.calculateChecksum(ref numArray1);
        this.commandQueue.Add(numArray1);
        if (this.jDebug || this.debugOutput)
        {
            Debug.Log(string.Concat("deleteLIM(", index, ") called"));
        }
    }

    public void Disconnect()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE Disconnect");
        }
            try
            {
                BluetoothLEHardwareInterface.DisconnectPeripheral(this.uuids[this.connectedDevice], this.disconnectCallback);
            }
            catch (Exception exception1)
            {
                Exception exception = exception1;
                Debug.LogError("DisconnectPeripheral failed 2");
                Debug.LogException(exception);
                if (exception.Message.Contains("out of range"))
                {
                    Debug.Log("Out of Range disconnect, break out");
                    base.CancelInvoke("timeOut");
                    this.m_skipTimeoutDisconnect = true;
                    return;
                }
            }
            base.CancelInvoke("SendPIN");
            base.CancelInvoke("timeOut");
            BluetoothLE.connecting = false;
            BluetoothLE.connectDataState = false;
            BluetoothLE.connectState = false;
            this.connectedDevice = -1;
            this.setWriteState(false);

            if (this.waitingForSequenceFeedback)
            {
                this.waitingForSequenceFeedback = false;
                return;
            }
            base.Invoke("disconnectCB", 2f);
        
    }

    private void disconnectCB()
    {
        base.CancelInvoke("disconnectCB");
        if (this.disconnectCallback != null)
        {
            this.disconnectCallback(null);
        }
    }

    public void disconnectedPeripheralAction(string uuid)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** BLE disconnectedPeripheralAction: ", uuid));
        }
        base.CancelInvoke("SendPIN");
 
        BluetoothLE.connecting = false;
        BluetoothLE.connectDataState = false;
        BluetoothLE.connectState = false;
        this.connectedDevice = -1;
        this.setWriteState(false);
        if (this.disconnectCallback != null)
        {
            this.disconnectCallback(uuid);
        }
        if (this.waitingForSequenceFeedback)
        {
            //TODO: TutorialManager.instance.ProgressToNext("BluetoothReject");
        }
    }

    public void DisconnectForTutorial()
    {
        try
        {
            BluetoothLEHardwareInterface.DisconnectPeripheral(this.uuids[this.connectedDevice], null);
        }
        catch (Exception exception1)
        {
            Exception exception = exception1;
            Debug.LogError("DisconnectPeripheral failed 3");
            Debug.LogException(exception);
            if (exception.Message.Contains("out of range"))
            {
                Debug.Log("Out of Range disconnect, break out");
                base.CancelInvoke("timeOut");
                this.m_skipTimeoutDisconnect = true;
                return;
            }
        }
        base.CancelInvoke("SendPIN");
        base.CancelInvoke("timeOut");
        BluetoothLE.connecting = false;
        BluetoothLE.connectDataState = false;
        BluetoothLE.connectState = false;
        this.connectedDevice = -1;
        this.setWriteState(false);
    }

    public void Error(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** BLE error: ", msg));
        }
        if (msg.IndexOf("not enabled", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            //TODO error popup
        }
    }

    private void executeQueue()
    {
        if (this.commandQueue.Count > 0)
        {
            this.SendCommand(this.commandQueue[0], false);
            this.commandQueue.RemoveAt(0);
        }
    }

    public void Found(string a, string b)
    {
        Debug.Log(string.Concat("BLE Peripheral Found: ", a, ", ", b));
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** Peripheral Found: ", a, ", ", b));
        }
        if (!b.ToLower().Contains("meccanoid"))
        {
            return;
        }
        this.uuids.Add(a);
        this.names.Add(b);
        if (this.deviceFoundCallback != null)
        {
            this.deviceFoundCallback(this.names);
        }
    }

    public int getBatteryLevel()
    {
        return (!this.config0flag ? -1 : (int)this.status[11]);
    }

    public Color getColor(int one, int two)
    {
        int num = one & 7;
        int num1 = one >> 3 & 7;
        int num2 = two & 7;
        return new Color((float)num / 7f, (float)num1 / 7f, (float)num2 / 7f);
    }

    public bool getConnectDataState()
    {
        return BluetoothLE.connectDataState;
    }

    public bool getConnecting()
    {
        return BluetoothLE.connecting;
    }

    public bool getConnectState()
    {
        return BluetoothLE.connectState;
    }

    public void getLIMInfo(byte index)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 19;
        numArray[1] = index;
        byte[] numArray1 = numArray;
        this.calculateChecksum(ref numArray1);
        this.commandQueue.Add(numArray1);
        if (this.debugOutput || this.jDebug)
        {
            Debug.Log(string.Concat("getLIMInfo(", index, ") called"));
        }
    }

    public bool[] getMotors()
    {
        return this.motors;
    }

    public bool getMotorState()
    {
        return BluetoothLE.motorState;
    }

    public void getName()
    {
        this.nameflag = false;
        this.SendCommand(7);
    }

    public int getScannedDevices()
    {
        return (null == BluetoothLE.bluetoothDeviceScript || BluetoothLE.bluetoothDeviceScript.DiscoveredDeviceList == null ? 0 : BluetoothLE.bluetoothDeviceScript.DiscoveredDeviceList.Count);
    }

    public void getServoPos()
    {
        this.getServoPos(true);
    }

    private void getServoPos(bool queue)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 9;
        byte[] numArray1 = numArray;
        this.calculateChecksum(ref numArray1);
        if (queue)
        {
            this.commandQueue.Add(numArray1);
        }
        else
        {
            BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, numArray1, 20, this.ack, new Action<string>(this.Result));
        }
    }

    public void getTimeDate()
    {
        this.timeDateflag = false;
        this.SendCommand(29);
    }

    public bool getUpdateState()
    {
        return BluetoothLE.updateState;
    }

    public bool getWriteState()
    {
        return BluetoothLE.writeState;
    }

    public void Initialized()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE initialized");
        }
    }

    public bool IsDeviceConnected()
    {
        return this.connectedDevice != -1;
    }

    public bool IsDrone()
    {
        return (this.robotType == 0 ? true : this.robotType == 3);
    }

    public bool isInitialized()
    {
        return (null != BluetoothLE.bluetoothDeviceScript ? BluetoothLE.bluetoothDeviceScript.isInitialized() : false);
    }

    public bool isMatch(string a, string b)
    {
        int num;
        int num1;
        if (a == null || b == null)
        {
            return false;
        }
        string[] strArrays = a.Split(new char[] { '-' });
        string[] strArrays1 = b.Split(new char[] { '-' });
        if ((int)strArrays.Length == 0 || (int)strArrays1.Length == 0)
        {
            return false;
        }
        if (!int.TryParse(strArrays[0], NumberStyles.HexNumber, null, out num) || !int.TryParse(strArrays1[0], NumberStyles.HexNumber, null, out num1))
        {
            return false;
        }
        return num == num1;
    }

    public void Notification(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** Notification: ", msg));
        }
    }

    private void OnApplicationPause(bool paused)
    {
        BluetoothLEHardwareInterface.PauseMessages(paused);
        this.m_appPaused = paused;
    }

    private void OnApplicationQuit()
    {
        if (!Application.isEditor)
        {
            Debug.Log(string.Concat("Application ending after ", Time.time, " seconds"));
            this.Disconnect();
        }
    }

    private void OnDestroy()
    {
        BluetoothLEHardwareInterface.DeInitialize(null);

        BluetoothLE.connecting = false;
        BluetoothLE.connectDataState = false;
        BluetoothLE.connectState = false;
        if (this.debugOutput)
        {
            Debug.Log("BluetoothLE.OnDestroy called");
        }
    }

    public void playLIM(byte index)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 21;
        numArray[1] = index;
        byte[] numArray1 = numArray;
        this.calculateChecksum(ref numArray1);
        this.commandQueue.Add(numArray1);
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("playLIM(", index, ") called"));
        }
    }

    public void playPreset(byte index, bool immediatePlay = false)
    {
        this.playPreset(index, 0, immediatePlay);
    }

    public void playPreset(byte index, byte sub, bool immediatePlay = false)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 25;
        numArray[1] = index;
        numArray[2] = sub;
        byte[] numArray1 = numArray;
        this.calculateChecksum(ref numArray1);
        this.commandQueue.Add(numArray1);
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("playPreset(", index, ") called"));
        }
    }

    public void recordLIM(byte index)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 22;
        numArray[1] = index;
        byte[] numArray1 = numArray;
        this.calculateChecksum(ref numArray1);
        this.commandQueue.Add(numArray1);
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("recordLIM(", index, ") called"));
        }
    }

    public void requestStatus()
    {
        if (this.m_checkingForStatus)
        {
            Debug.Log("checking for status already");
        }
        bool flag = false;
        this.config3flag = false;
        bool flag1 = flag;
        flag = flag1;
        this.config2flag = flag1;
        bool flag2 = flag;
        flag = flag2;
        this.config1flag = flag2;
        this.config0flag = flag;
        this.m_checkingForStatus = true;
        this.SendCommand(1);
        this.SendCommand(2);
        this.SendCommand(3);
        this.SendCommand(32);
    }

    public void ResetQueue()
    {
        Debug.Log(string.Concat("clear queue: ", this.commandQueue.Count));
        this.commandQueue.Clear();
    }

    public void resetScreen()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE resetScreen");
        }
        base.CancelInvoke("SendPIN");

        BluetoothLE.connecting = false;
        BluetoothLE.connectDataState = false;
        BluetoothLE.connectState = false;
        this.connectedDevice = -1;
        this.setWriteState(false);
        this.robotType = 0;
    }

    public void Result(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** Result: ", msg));
        }
    }

    public void Scan()
    {
        if (!BluetoothLE.scanning)
        {
            this.Clear();
            BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, new Action<string, string>(this.Found), null, false);
            BluetoothLE.scanning = true;
            if (this.debugOutput)
            {
                Debug.Log("BluetoothLE Scan called");
            }
        }
    }

    public void SendAwake()
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        this.SendCommand(25, 29);
    }

    private void SendCommand(byte command, bool queue)
    {
        this.SendCommand(command, 0, queue);
    }

    public void SendCommand(byte command)
    {
        this.SendCommand(command, 0);
    }

    private void SendCommand(byte command, byte data, bool queue)
    {
        this.SendCommand(new byte[] { command, data, data, data, data, data, data, data, data, data, data, data, data, data, data, data, data, data, 0, 0 }, queue);
    }

    public void SendCommand(byte command, byte data)
    {
        this.SendCommand(new byte[] { command, data, data, data, data, data, data, data, data, data, data, data, data, data, data, data, data, data, 0, 0 });
    }

    public void SendCommand(byte[] ch1)
    {
        this.SendCommand(ch1, true);
    }

    private void SendCommand(byte[] ch1, bool queue)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        this.calculateChecksum(ref ch1);
        if (this.debugOutput)
        {
            string empty = string.Empty;
            byte[] numArray = ch1;
            for (int i = 0; i < (int)numArray.Length; i++)
            {
                byte num = numArray[i];
                empty = string.Concat(empty, string.Format("{0:X2}", num));
            }
        }
        if (queue)
        {
            this.commandQueue.Add(ch1);
        }
        else
        {
            BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, ch1, 20, this.ack, new Action<string>(this.Result));
            if (ch1[0] == 11)
            {
                BluetoothLE.writeState = (ch1[1] == 0 ? true : ch1[1] == 2);
            }
        }
    }

    public void SendPIN()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE SendPIN");
        }
        byte[] numArray = new byte[20];
        numArray[0] = 26;
        numArray[1] = 1;
        numArray[3] = 1;
        byte[] numArray1 = numArray;
        this.calculateChecksum(ref numArray1);
        BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, numArray1, 20, this.ack, new Action<string>(this.Result));
    }

    public void Service(string a, string b)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** BLE Service: ", a, ", ", b));
        }
    }

    public void setBatteryLevel()
    {
        if (!Application.isEditor)
        {
            return;
        }
        this.status[11] = (byte)Mathf.CeilToInt((float)UnityEngine.Random.Range(1, 6));
        this.config0flag = true;
    }

    public void setMapping()
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 31;
        byte[] numArray1 = numArray;
        for (int i = 0; i < 16; i++)
        {
            numArray1[i + 1] = this.mapping[i];
        }
        this.SendCommand(numArray1);
    }

    public void setMotors(bool[] input)
    {
        if ((int)input.Length == 4)
        {
            this.motors = input;
        }
    }

    public void setMotorState(bool state)
    {
        BluetoothLE.motorState = state;
    }

    private void setMotorValues()
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 13;
        numArray[3] = (byte)this.motorOutputSpeed;
        numArray[4] = (byte)this.motorOutputSpeed;
        numArray[5] = 255;
        numArray[6] = 255;
        byte[] numArray1 = numArray;
        if (this.motors[0])
        {
            numArray1[1] = 1;
        }
        else if (!this.motors[1])
        {
            numArray1[3] = 0;
        }
        else
        {
            numArray1[1] = 2;
        }
        if (this.motors[2])
        {
            numArray1[2] = 1;
        }
        else if (!this.motors[3])
        {
            numArray1[4] = 0;
        }
        else
        {
            numArray1[2] = 2;
        }
        this.calculateChecksum(ref numArray1);
        BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, numArray1, 20, this.ack, new Action<string>(this.Result));
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "setMotorValues: ", numArray1[1], " (", numArray1[3], ") - ", numArray1[2], " (", numArray1[4], ")" }));
        }
    }

    public void setName(string name)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[20];
        numArray[0] = 6;
        byte[] numArray1 = numArray;
        for (int i = 0; i < name.Length && i < 16 && name[i] >= ' ' && name[i] <= '~'; i++)
        {
            numArray1[i + 1] = (byte)name[i];
        }
        this.SendCommand(numArray1);
        this.meccaName = name;
        this.nameflag = true;
    }

    public void setResult(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** setResult: ", msg));
        }
    }

    public void setRobotType(MODEL type)
    {
        this.robotType = (byte)type;
    }

    public void setServoMode(byte mode)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] numArray = new byte[] { 11, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, mode, 0, 0, 0 };
        this.calculateChecksum(ref numArray);
        this.commandQueue.Add(numArray);
    }

    public void setServoPos()
    {
        this.setServoPos(true);
    }

    private void setServoPos(bool queue)
    {
        byte[] numArray = new byte[] { 8, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 };
        for (int i = 0; i < 8; i++)
        {
            byte[] numArray1 = this.actualPos;
            byte num = this.adjust(this.actualPos[i], this.targetPos[i]);
            byte num1 = num;
            numArray1[i] = num;
            numArray[i + 1] = num1;
        }
        this.calculateChecksum(ref numArray);
        if (BluetoothLE.connectState && BluetoothLE.writeState)
        {
            if (queue)
            {
                this.commandQueue.Add(numArray);
            }
            else
            {
                BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, numArray, 20, this.ack, new Action<string>(this.setResult));
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (int j = 1; j <= 8; j++)
            {
                stringBuilder.Append(string.Concat(numArray[j].ToString("X2"), " "));
            }
            if (this.debugOutput)
            {
                Debug.Log(string.Concat(new object[] { "*** setServoPos: ", stringBuilder.ToString(), " (", Time.realtimeSinceStartup, ")" }));
            }
        }
    }

    private void setTimeDate()
    {
        object obj;
        DateTime now = DateTime.Now;
        if (this.debugOutput)
        {
            Debug.Log(now.TimeOfDay);
        }
        int hour = now.Hour;
        bool flag = hour >= 12;
        if (flag)
        {
            hour = hour - 12;
        }
        if (hour == 0)
        {
            hour = 12;
        }
        byte[] minute = new byte[20];
        minute[0] = 27;
        minute[1] = (byte)(hour / 10);
        minute[2] = (byte)(hour % 10);
        minute[3] = (byte)(now.Minute / 10);
        minute[4] = (byte)(now.Minute % 10);
        if (!flag)
        {
            obj = null;
        }
        else
        {
            obj = 1;
        }
        minute[5] = (byte)obj;
        minute[7] = (byte)now.Month;
        minute[8] = (byte)now.Day;
        minute[9] = (byte)(now.Year / 1000 % 10);
        minute[10] = (byte)(now.Year / 100 % 10);
        minute[11] = (byte)(now.Year / 10 % 10);
        minute[12] = (byte)(now.Year % 10);
        this.SendCommand(minute);
    }

    public void setUpdateState(bool state)
    {
        BluetoothLE.updateState = state;
    }

    public void setWriteState(bool state)
    {
        this.setWriteState(state, false);
    }

    public void setWriteState(bool state, bool force)
    {
        if (!force && BluetoothLE.pendingWriteState == state)
        {
            return;
        }
        if (!BluetoothLE.pendingWriteState)
        {
            this.getServoPos();
        }
        BluetoothLE.pendingWriteState = state;
        this.setServoMode((byte)((!state ? 4 : 2)));
        if (!BluetoothLE.pendingWriteState)
        {
            this.getServoPos();
        }
    }

    private void Start()
    {
        this.lastInterval = Time.realtimeSinceStartup;
        this.frames = 0;
    }

    public void StopScan()
    {
        if (BluetoothLE.scanning)
        {
            BluetoothLEHardwareInterface.StopScan();
            BluetoothLE.scanning = false;
            if (this.debugOutput)
            {
                Debug.Log("BluetoothLE StopScan called");
            }
        }
    }

    public void SubResult(string msg, byte[] data)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if ((int)data.Length > 0)
        {
            stringBuilder.Append(string.Concat(msg, ": (", data[0].ToString("X"), ") "));
        }
        for (int i = 1; i <= 8 && i < (int)data.Length; i++)
        {
            stringBuilder.Append(string.Concat(data[i].ToString("X"), " "));
        }
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** SubResult: ", stringBuilder.ToString()));
        }
        byte num = data[0];
        switch (num)
        {
            case 1:
                {
                    for (int j = 0; j < 16; j++)
                    {
                        this.status[j] = data[j + 1];
                    }
                    if (this.debugOutput)
                    {
                        Debug.Log(string.Concat(new object[] { "STATUS DATA: ", this.status[0], ",", this.status[1], ",", this.status[2], ",", this.status[3], ",", this.status[4], ",", this.status[5], ",", this.status[6], ",", this.status[7], ",", this.status[8], ",", this.status[9], ",", this.status[10], ",", this.status[11], ",", this.status[12], ",", this.status[13], ",", this.status[14], ",", this.status[15] }));
                    }
                    if (this.status[15] == 5 || this.status[15] == 6)
                    {
                        if (this.jDebug)
                        {
                            Debug.Log("ERROR FOUND");
                        }
                        this.CheckForConfigError();
                    }
                    this.config0flag = true;
                    this.updateRobotType();
                    if (this.status[15] == 4)
                    {
                        if (this.jDebug)
                        {
                            Debug.Log("SWITCH FOUND ");
                        }
                        /*if (!TutorialManager.ACTIVE)
                        {
                            this.robotType = this.status[0];
                            Debug.Log(string.Concat("new robot type: ", this.robotType));
                            ScanManager scanManager = Object.FindObjectOfType<ScanManager>();
                            if (scanManager != null)
                            {
                                scanManager.UpdateRobotInfo();
                                scanManager.UpdateRobotPageInfo();
                            }
                            GameObject gameObject = GameObject.Find("DroneWarningDialog");
                            if (gameObject != null && gameObject.get_activeSelf())
                            {
                                gameObject.GetComponent<ConnectToDroneMeccDialog>().ConnectToMecc();
                            }
                            GameObject gameObject1 = GameObject.Find("AppVersionDialog");
                            if (gameObject1 != null && gameObject1.get_activeSelf() && scanManager != null)
                            {
                                if (this.jDebug)
                                {
                                    Debug.Log("app version active");
                                }
                                scanManager.ReprocessFirmwareCheck();
                            }
                            this.requestStatus();
                        }*/
                    }
                    if (this.status[15] != 0)
                    {
                        if (this.jDebug)
                        {
                            Debug.Log(string.Concat("ERROR FOUND : ", this.status[15]));
                        }
                        if (this.jDebug)
                        {
                            Debug.Log(string.Concat("behavior running : "));
                        }
                    }
                    this.updateMeccaButtons();
                    break;
                }
            case 2:
                {
                    for (int k = 0; k < 16; k++)
                    {
                        this.config[k] = data[k + 1];
                    }
                    this.config1flag = true;
                    if (this.debugOutput)
                    {
                        Debug.LogWarning(string.Concat(new object[] { "Config1: ", data[1], ",", data[2], ",", data[3], ",", data[4], " | ", data[5], ",", data[6], ",", data[7], ",", data[8], " | ", data[9], ",", data[10], ",", data[11], ",", data[12], " | ", data[13], ",", data[14], ",", data[15], ",", data[16] }));
                    }
                    if (this.m_checkingForConfigError)
                    {
                        this.m_checkingForConfigError = false;
                        //Error
                    }
                    this.m_checkingForStatus = false;
                    break;
                }
            case 3:
                {
                    for (int l = 0; l < 16; l++)
                    {
                        this.config[l + 16] = data[l + 1];
                    }
                    this.config2flag = true;
                    if (this.debugOutput)
                    {
                        Debug.LogWarning(string.Concat(new object[] { "Config2: ", data[1], ",", data[2], ",", data[3], ",", data[4], " | ", data[5], ",", data[6], ",", data[7], ",", data[8], " | ", data[9], ",", data[10], ",", data[11], ",", data[12], " | ", data[13], ",", data[14], ",", data[15], ",", data[16] }));
                    }
                    break;
                }
            case 7:
                {
                    this.meccaName = string.Empty;
                    for (int m = 1; m <= 16 && data[m] >= 32 && data[1] <= 126; m++)
                    {
                        BluetoothLE bluetoothLE = this;
                        bluetoothLE.meccaName = string.Concat(bluetoothLE.meccaName, (char)data[m]);
                    }
                    this.nameflag = true;
                    break;
                }
            case 9:
                {
                    for (int n = 0; n < 8; n++)
                    {
                        this.actualPos[n] = data[n + 1];
                    }
                    if (this.getServoPosCallback != null)
                    {
                        this.getServoPosCallback();
                    }
                    break;
                }
            default:
                {
                    switch (num)
                    {
                        case 26:
                            {
                                base.CancelInvoke("timeOut");
                                base.CancelInvoke("SendPIN");
                                BluetoothLE.connecting = false;
                                if (data[1] != 0 || data[2] != 1 || data[3] != 255)
                                {
                                    if (this.debugOutput)
                                    {
                                        Debug.Log(string.Concat(new object[] { "MB_SendPinNumber returned: ", data[1], ", ", data[2], ", ", data[3], ", ", data[4] }));
                                    }
                                    BluetoothLE.connectState = true;
                                    this.Completed();
                                }
                                else
                                {
                                    BluetoothLE.connectState = false;
                                    this.Disconnect();
                                }
                                break;
                            }
                        case 29:
                            {
                                this.timeDate = string.Format("{0}{1}:{2}{3}{4} onMB_GetServoMapping {5}/{6}/{7}{8}{9}{10}", new object[] { (data[1] != 0 ? data[1].ToString() : string.Empty), data[2].ToString(), data[3].ToString(), data[4].ToString(), (data[5] != 0 ? "pm" : "am"), data[7].ToString(), data[8].ToString(), data[9].ToString(), data[10].ToString(), data[11].ToString(), data[12].ToString() });
                                this.timeDateflag = true;
                                break;
                            }
                        default:
                            {
                                if (num == 15)
                                {
                                    if (this.getLEDColorCallback != null)
                                    {
                                        Color color = this.getColor((int)data[3], (int)data[4]);
                                        this.getLEDColorCallback(color);
                                    }
                                }
                                else if (num != 19)
                                {
                                    if (num == 32)
                                    {
                                        for (int o = 0; o < 16; o++)
                                        {
                                            this.mapping[o] = data[o + 1];
                                        }
                                        this.config3flag = true;
                                        if (this.debugOutput)
                                        {
                                            Debug.LogWarning(string.Concat(new object[] { "Mapping: ", data[1], ",", data[2], ",", data[3], ",", data[4], " | ", data[5], ",", data[6], ",", data[7], ",", data[8], " | ", data[9], ",", data[10], ",", data[11], ",", data[12], " | ", data[13], ",", data[14], ",", data[15], ",", data[16] }));
                                        }
                                    }
                                }
                                else if (this.getLIMInfoCallback != null)
                                {
                                    byte num1 = data[1];
                                    if (this.jDebug)
                                    {
                                        Debug.Log(string.Concat("GET LIM INFO: ", num1));
                                    }
                                    byte num2 = data[2];
                                    char[] chrArray = new char[16];
                                    for (int p = 0; p < 15; p++)
                                    {
                                        chrArray[p] = (char)data[p + 3];
                                    }
                                    chrArray[15] = '\0';
                                    if (this.jDebug)
                                    {
                                        Debug.Log(string.Concat("LIM NAME: ", new string(chrArray)));
                                    }
                                    float single = (float)Convert.ToInt32(num2);
                                    if (this.jDebug)
                                    {
                                        Debug.Log(string.Concat(new object[] { "LIM CONVERTED DURATION: ", single, " : ", num2 }));
                                    }
                                    this.getLIMInfoCallback(num1, single, new string(chrArray));
                                }
                                break;
                            }
                    }
                    break;
                }
        }
        if (this.resultsCallback != null)
        {
            this.resultsCallback(data);
        }
    }

    public void Subscribe()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE Subscribe");
        }
        BluetoothLEHardwareInterface.SubscribeCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.subscribeUUID, new Action<string>(this.Subscribed), new Action<string, byte[]>(this.SubResult));
    }

    public void Subscribed(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("*** Subscribed: ", msg));
        }
        this.SendPIN();
    }

    public void timeOut()
    {
        if (this.debugOutput || this.jDebug)
        {
            Debug.Log("*** BLE timeOut");
        }
        base.CancelInvoke("timeOut");
        this.Disconnect();
        if (this.m_skipTimeoutDisconnect)
        {
            this.m_skipTimeoutDisconnect = false;
            return;
        }
        if (this.disconnectCallback != null)
        {
            this.disconnectCallback(null);
        }
    }

    private void Update()
    {
        BluetoothLE bluetoothLE = this;
        bluetoothLE.frames = bluetoothLE.frames + 1;
        float _realtimeSinceStartup = Time.realtimeSinceStartup;
        if (BluetoothLE.connectState && _realtimeSinceStartup > this.lastInterval + this.updateInterval)
        {
            if (this.interleave % 10 == 1)
            {
                this.SendCommand(1, false);
            }
            else if (BluetoothLE.updateState && this.interleave % 2 == 0)
            {
                if (!BluetoothLE.writeState)
                {
                    this.getServoPos(false);
                }
                else
                {
                    this.setServoPos(false);
                }
            }
            else if (!BluetoothLE.motorState || this.interleave % 10 != 5)
            {
                this.executeQueue();
            }
            else
            {
                this.setMotorValues();
            }
            BluetoothLE bluetoothLE1 = this;
            int num = bluetoothLE1.interleave + 1;
            int num1 = num;
            bluetoothLE1.interleave = num;
            if (num1 >= 20)
            {
                this.interleave = 0;
            }
            this.frames = 0;
            this.lastInterval = _realtimeSinceStartup;
        }
        if (this.connectDataCallback != null && !BluetoothLE.connecting && !BluetoothLE.connectDataState && 0 <= this.connectedDevice && this.connectedDevice < this.uuids.Count && this.config0flag && this.config1flag && this.config2flag && this.config3flag && this.nameflag)
        {
            BluetoothLE.connectDataState = true;
            this.connectDataCallback(this.uuids[this.connectedDevice]);
        }
    }

    private void updateMeccaButtons()
    {
        this.meccaBlueButton = (this.status[7] & 1) != 0;
        this.meccaRedButton = (this.status[7] & 2) != 0;
        this.meccaGreenButton = (this.status[7] & 4) != 0;
        this.meccaYellowButton = (this.status[7] & 8) != 0;
        byte num = (byte)(this.status[7] & 15);
        if (num != this.prevButtons && this.OnMeccaBrainButtonsChanged != null)
        {
            byte num1 = (byte)(num ^ this.prevButtons);
            this.OnMeccaBrainButtonsChanged((num1 & 1) != 0, (num1 & 2) != 0, (num1 & 4) != 0, (num1 & 8) != 0);
        }
        this.prevButtons = num;
    }

    private void updateRobotType()
    {
        if (this.config0flag && this.status[0] != this.robotType)
        {
            this.robotType = this.status[0];
            this.robotEverSet = true;
            //TODO update robot info
        }
        this.robotType = this.status[0];
    }

    public enum MB_Commands : byte
    {
        MB_Status = 1,
        MB_GetConfig1 = 2,
        MB_GetConfig2 = 3,
        MB_SetConfig1 = 4,
        MB_Setconfig2 = 5,
        MB_SetName = 6,
        MB_GetName = 7,
        MB_SetServoPos = 8,
        MB_GetServoPos = 9,
        MB_GetServoStatus = 10,
        MB_SetServoStatus = 11,
        MB_SetServoLED = 12,
        MB_SetMotorValues = 13,
        MB_GetMotorValues = 14,
        MB_GetRGBLEDColor1 = 15,
        MB_GetRGBLEDColor2 = 16,
        MB_SetRGBLEDColor1 = 17,
        MB_SetRGBLEDColor2 = 18,
        MB_GetLIMInfo = 19,
        MB_ChangeLIMName = 20,
        MB_PlayLIM = 21,
        MB_RecordLIM = 22,
        MB_DeleteLIM = 23,
        MB_GetPresetInfo = 24,
        MB_PlayPreset = 25,
        MB_SendPinNumber = 26,
        MB_SetTimeAndDate = 27,
        MB_SetPCBLED = 28,
        MB_GetTimeAndDate = 29,
        MB_GetServoLED = 30,
        MB_SetServoMapping = 31,
        MB_GetServoMapping = 32
    }

    public struct MotorIndex
    {
        public const int leftFront = 0;

        public const int leftRear = 1;

        public const int rightFront = 2;

        public const int rightRear = 3;
    }

    public struct RobotType
    {
        public const byte drone = 0;

        public const byte g15ks = 1;

        public const byte g15 = 2;

        public const byte g16Drone = 3;

        public const byte g16ks = 4;

        public const byte g16 = 5;
    }

    public enum ServoMode
    {
        SendOld,
        RecieveOld,
        Send,
        Unknown,
        Recieve
    }

    public enum MODEL
    {
        OTHER,
        TALL,
        SHORT,
        G16DRONE,
        TALLG16,
        SHORTG16
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class BluetoothLE : MonoBehaviour
{

    public enum MODEL
    {
        OTHER,
        TALL,
        SHORT,
        G16DRONE,
        TALLG16,
        SHORTG16
    }
    public enum MB_Commands : byte
    {
        MB_Status = 1,
        MB_GetConfig1,
        MB_GetConfig2,
        MB_SetConfig1,
        MB_Setconfig2,
        MB_SetName,
        MB_GetName,
        MB_SetServoPos,
        MB_GetServoPos,
        MB_GetServoStatus,
        MB_SetServoStatus,
        MB_SetServoLED,
        MB_SetMotorValues,
        MB_GetMotorValues,
        MB_GetRGBLEDColor1,
        MB_GetRGBLEDColor2,
        MB_SetRGBLEDColor1,
        MB_SetRGBLEDColor2,
        MB_GetLIMInfo,
        MB_ChangeLIMName,
        MB_PlayLIM,
        MB_RecordLIM,
        MB_DeleteLIM,
        MB_GetPresetInfo,
        MB_PlayPreset,
        MB_SendPinNumber,
        MB_SetTimeAndDate,
        MB_SetPCBLED,
        MB_GetTimeAndDate,
        MB_GetServoLED,
        MB_SetServoMapping,
        MB_GetServoMapping
    }

    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct MotorIndex
    {
        public const int leftFront = 0;

        public const int leftRear = 1;

        public const int rightFront = 2;

        public const int rightRear = 3;
    }

    [StructLayout(LayoutKind.Sequential, Size = 1)]
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

    public byte[] actualPos = new byte[]
    {
        127,
        127,
        127,
        127,
        127,
        127,
        127,
        127
    };

    public byte[] targetPos = new byte[]
    {
        127,
        127,
        127,
        127,
        127,
        127,
        127,
        127
    };

    public byte[] status = new byte[16];

    public byte[] config = new byte[32];

    public byte[] mapping = new byte[16];

    public bool config0flag;

    public bool config1flag;

    public bool config2flag;

    public bool config3flag;

    public bool timeDateflag;

    public bool nameflag;

    private float[] batteryLevels = new float[]
    {
        10f,
        25f,
        50f,
        75f,
        100f
    };

    private static bool connectState;

    private static bool connectDataState;

    private static bool connecting;

    private static bool writeState;

    private static bool pendingWriteState;

    private static bool motorState = true;

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

    public static int currentRobot
    {
        get
        {
            return (int)BluetoothLE.instance.robotType;
        }
    }

    private void Awake()
    {
        if (this.debugOutput)
        {
            Debug.Log("BluetoothLE.Awake called");
        }
        if (!BluetoothLE.created)
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
        else
        {
            UnityEngine.Object.Destroy(base.gameObject);
        }
        Array.Clear(this.status, 0, this.status.Length);
        Array.Clear(this.config, 0, this.config.Length);
        Array.Clear(this.mapping, 0, this.mapping.Length);
    }


    private void Start()
    {
        this.lastInterval = Time.realtimeSinceStartup;
        this.frames = 0;
    }

    public bool isInitialized()
    {
        return !(null == BluetoothLE.bluetoothDeviceScript) && BluetoothLE.bluetoothDeviceScript.isInitialized();
    }

    private void executeQueue()
    {
        if (this.commandQueue.Count > 0)
        {
            this.SendCommand(this.commandQueue[0], false);
            this.commandQueue.RemoveAt(0);
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
            Debug.Log("Application ending after " + Time.time + " seconds");
            this.Disconnect();
        }
    }

    private void OnDestroy()
    {
        BluetoothLEHardwareInterface.DeInitialize(null);
        BluetoothLE.connectState = (BluetoothLE.connectDataState = (BluetoothLE.connecting = false));
        if (this.debugOutput)
        {
            Debug.Log("BluetoothLE.OnDestroy called");
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

    public int getBatteryLevel()
    {
        return (!this.config0flag) ? -1 : ((int)this.status[11]);
    }

    public bool getConnectState()
    {
        return BluetoothLE.connectState;
    }

    public bool getConnectDataState()
    {
        return BluetoothLE.connectDataState;
    }

    public bool getConnecting()
    {
        return BluetoothLE.connecting;
    }

    public int getScannedDevices()
    {
        return (!(null == BluetoothLE.bluetoothDeviceScript) && BluetoothLE.bluetoothDeviceScript.DiscoveredDeviceList != null) ? BluetoothLE.bluetoothDeviceScript.DiscoveredDeviceList.Count : 0;
    }

    public bool getWriteState()
    {
        return BluetoothLE.writeState;
    }

    public void setUpdateState(bool state)
    {
        BluetoothLE.updateState = state;
    }

    public bool getUpdateState()
    {
        return BluetoothLE.updateState;
    }

    public bool getMotorState()
    {
        return BluetoothLE.motorState;
    }

    public void setMotorState(bool state)
    {
        BluetoothLE.motorState = state;
    }

    public bool[] getMotors()
    {
        return this.motors;
    }

    public void setMotors(bool[] input)
    {
        if (input.Length == 4)
        {
            this.motors = input;
        }
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
        this.setServoMode((byte)((!state) ? 4 : 2));
        if (!BluetoothLE.pendingWriteState)
        {
            this.getServoPos();
        }
    }

    public void Initialized()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE initialized");
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
        else
        {
            this.StopScan();
        }
    }

    public void Clear()
    {
        Debug.Log("BLE Clear before scan");
        this.names.Clear();
        this.uuids.Clear();
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

    public void Found(string a, string b)
    {
        Debug.Log("BLE Peripheral Found: " + a + ", " + b);
        if (this.debugOutput)
        {
            Debug.Log("*** Peripheral Found: " + a + ", " + b);
        }
        //if (!b.ToLower().Contains("meccanoid"))
        //{
        //    return;
        //}
        this.uuids.Add(a);
        this.names.Add(b);

        if (this.deviceFoundCallback != null)
        {
            this.deviceFoundCallback(this.names);
        }
    }

    public void Connect(int index)
    {
        this.StopScan();
        this.connectedDevice = index;
        BluetoothLE.connectState = (BluetoothLE.connectDataState = false);
        BluetoothLE.connecting = true;
        if (this.jDebug)
        {
            Debug.Log("connecting to " + index);
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
            Debug.Log("*** BLE Connected: " + msg);
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
        catch (Exception ex2)
        {
            Debug.LogError("DisconnectPeripheral failed 2");
            Debug.LogException(ex2);
            if (ex2.Message.Contains("out of range"))
            {
                Debug.Log("Out of Range disconnect, break out");
                base.CancelInvoke("timeOut");
                this.m_skipTimeoutDisconnect = true;
                return;
            }
        }
        base.CancelInvoke("SendPIN");
        base.CancelInvoke("timeOut");
        BluetoothLE.connectState = (BluetoothLE.connectDataState = (BluetoothLE.connecting = false));
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
            Debug.Log("*** BLE disconnectedPeripheralAction: " + uuid);
        }
        base.CancelInvoke("SendPIN");
        BluetoothLE.connectState = (BluetoothLE.connectDataState = (BluetoothLE.connecting = false));
        this.connectedDevice = -1;
        this.setWriteState(false);
        if (this.disconnectCallback != null)
        {
            this.disconnectCallback(uuid);
        }
    }

    public void resetScreen()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE resetScreen");
        }
        base.CancelInvoke("SendPIN");
        BluetoothLE.connectState = (BluetoothLE.connectDataState = (BluetoothLE.connecting = false));
        this.connectedDevice = -1;
        this.setWriteState(false);
        this.robotType = 0;
    }

    public void Subscribe()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE Subscribe");
        }
        BluetoothLEHardwareInterface.SubscribeCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.subscribeUUID, new Action<string>(this.Subscribed), new Action<string, byte[]>(this.SubResult));
    }

    public void SendPIN()
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE SendPIN");
        }
        byte[] expr_1C = new byte[20];
        expr_1C[0] = 26;
        expr_1C[1] = 1;
        expr_1C[3] = 1;
        byte[] data = expr_1C;
        this.calculateChecksum(ref data);
        BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, data, 20, this.ack, new Action<string>(this.Result));
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

    public void Notification(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log("*** Notification: " + msg);
        }
    }

    public void Subscribed(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log("*** Subscribed: " + msg);
        }
        this.SendPIN();
    }

    public void requestStatus()
    {
        if (this.m_checkingForStatus)
        {
            Debug.Log("checking for status already");
        }
        this.config0flag = (this.config1flag = (this.config2flag = (this.config3flag = false)));
        this.m_checkingForStatus = true;
        this.SendCommand(1);
        this.SendCommand(2);
        this.SendCommand(3);
        this.SendCommand(32);
    }

    private void setTimeDate()
    {
        DateTime now = DateTime.Now;
        if (this.debugOutput)
        {
            Debug.Log(now.TimeOfDay);
        }
        int num = now.Hour;
        bool flag = num >= 12;
        if (flag)
        {
            num -= 12;
        }
        if (num == 0)
        {
            num = 12;
        }
        byte[] expr_4E = new byte[20];
        expr_4E[0] = 27;
        expr_4E[1] = (byte)(num / 10);
        expr_4E[2] = (byte)(num % 10);
        expr_4E[3] = (byte)(now.Minute / 10);
        expr_4E[4] = (byte)(now.Minute % 10);
        expr_4E[5] = (byte)((!flag) ? 0 : 1);
        expr_4E[7] = (byte)now.Month;
        expr_4E[8] = (byte)now.Day;
        expr_4E[9] = (byte)(now.Year / 1000 % 10);
        expr_4E[10] = (byte)(now.Year / 100 % 10);
        expr_4E[11] = (byte)(now.Year / 10 % 10);
        expr_4E[12] = (byte)(now.Year % 10);
        byte[] ch = expr_4E;
        this.SendCommand(ch);
    }

    public void getTimeDate()
    {
        this.timeDateflag = false;
        this.SendCommand(29);
    }

    public void getName()
    {
        this.nameflag = false;
        this.SendCommand(7);
    }

    public void setName(string name)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 6;
        byte[] array = expr_12;
        int num = 0;
        while (num < name.Length && num < 16 && name[num] >= ' ' && name[num] <= '~')
        {
            array[num + 1] = (byte)name[num];
            num++;
        }
        this.SendCommand(array);
        this.meccaName = name;
        this.nameflag = true;
    }

    public void setMapping()
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 31;
        byte[] array = expr_12;
        for (int i = 0; i < 16; i++)
        {
            array[i + 1] = this.mapping[i];
        }
        this.SendCommand(array);
    }

    public void SendAwake()
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        this.SendCommand(25, 29);
    }
    public void SubResult(string msg, byte[] data)
    {
        StringBuilder stringBuilder = new StringBuilder();
        if (data.Length > 0)
        {
            stringBuilder.Append(msg + ": (" + data[0].ToString("X") + ") ");
        }
        int num = 1;
        while (num <= 8 && num < data.Length)
        {
            stringBuilder.Append(data[num].ToString("X") + " ");
            num++;
        }
        if (this.debugOutput)
        {
            Debug.Log("*** SubResult: " + stringBuilder.ToString());
        }
        byte b = data[0];
        switch (b)
        {
            case 1:
                for (int i = 0; i < 16; i++)
                {
                    this.status[i] = data[i + 1];
                }
                if (this.debugOutput)
                {
                    Debug.Log(string.Concat(new object[]
                    {
                    "STATUS DATA: ",
                    this.status[0],
                    ",",
                    this.status[1],
                    ",",
                    this.status[2],
                    ",",
                    this.status[3],
                    ",",
                    this.status[4],
                    ",",
                    this.status[5],
                    ",",
                    this.status[6],
                    ",",
                    this.status[7],
                    ",",
                    this.status[8],
                    ",",
                    this.status[9],
                    ",",
                    this.status[10],
                    ",",
                    this.status[11],
                    ",",
                    this.status[12],
                    ",",
                    this.status[13],
                    ",",
                    this.status[14],
                    ",",
                    this.status[15]
                    }));
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
                if (this.status[15] == 4)
                {
                    if (this.jDebug)
                    {
                        Debug.Log("SWITCH FOUND ");
                    }
                }
                if (this.status[15] != 0)
                {
                    if (this.jDebug)
                    {
                        Debug.Log("ERROR FOUND : " + this.status[15]);
                    }
                    if (this.jDebug)
                    {
                        Debug.Log("behavior running : Not implemented yet");
                    }
                }
                this.updateMeccaButtons();
                goto IL_D02;
            case 2:
                for (int j = 0; j < 16; j++)
                {
                    this.config[j] = data[j + 1];
                }
                this.config1flag = true;
                if (this.debugOutput)
                {
                    Debug.LogWarning(string.Concat(new object[]
                    {
                    "Config1: ",
                    data[1],
                    ",",
                    data[2],
                    ",",
                    data[3],
                    ",",
                    data[4],
                    " | ",
                    data[5],
                    ",",
                    data[6],
                    ",",
                    data[7],
                    ",",
                    data[8],
                    " | ",
                    data[9],
                    ",",
                    data[10],
                    ",",
                    data[11],
                    ",",
                    data[12],
                    " | ",
                    data[13],
                    ",",
                    data[14],
                    ",",
                    data[15],
                    ",",
                    data[16]
                    }));
                }
                this.m_checkingForStatus = false;
                goto IL_D02;
            case 3:
                for (int k = 0; k < 16; k++)
                {
                    this.config[k + 16] = data[k + 1];
                }
                this.config2flag = true;
                if (this.debugOutput)
                {
                    Debug.LogWarning(string.Concat(new object[]
                    {
                    "Config2: ",
                    data[1],
                    ",",
                    data[2],
                    ",",
                    data[3],
                    ",",
                    data[4],
                    " | ",
                    data[5],
                    ",",
                    data[6],
                    ",",
                    data[7],
                    ",",
                    data[8],
                    " | ",
                    data[9],
                    ",",
                    data[10],
                    ",",
                    data[11],
                    ",",
                    data[12],
                    " | ",
                    data[13],
                    ",",
                    data[14],
                    ",",
                    data[15],
                    ",",
                    data[16]
                    }));
                }
                goto IL_D02;
            /*case 4:
            case 5:
            case 6:
            case 8:
            IL_C6:
                switch (b)
                {
                    case 26:
                        base.CancelInvoke("timeOut");
                        base.CancelInvoke("SendPIN");
                        BluetoothLE.connecting = false;
                        if (data[1] == 0 && data[2] == 1 && data[3] == 255)
                        {
                            BluetoothLE.connectState = false;
                            this.Disconnect();
                        }
                        else
                        {
                            if (this.debugOutput)
                            {
                                Debug.Log(string.Concat(new object[]
                                {
                            "MB_SendPinNumber returned: ",
                            data[1],
                            ", ",
                            data[2],
                            ", ",
                            data[3],
                            ", ",
                            data[4]
                                }));
                            }
                            BluetoothLE.connectState = true;
                            this.Completed();
                        }
                        goto IL_D02;
                    case 27:
                    case 28:
                    IL_E0:
                        if (b == 15)
                        {
                            if (this.getLEDColorCallback != null)
                            {
                                Color color = this.getColor((int)data[3], (int)data[4]);
                                this.getLEDColorCallback(color);
                            }
                            goto IL_D02;
                        }
                        if (b == 19)
                        {
                            if (this.getLIMInfoCallback != null)
                            {
                                byte b2 = data[1];
                                if (this.jDebug)
                                {
                                    Debug.Log("GET LIM INFO: " + b2);
                                }
                                byte b3 = data[2];
                                char[] array = new char[16];
                                for (int l = 0; l < 15; l++)
                                {
                                    array[l] = (char)data[l + 3];
                                }
                                array[15] = '\0';
                                if (this.jDebug)
                                {
                                    Debug.Log("LIM NAME: " + new string(array));
                                }
                                float num2 = (float)Convert.ToInt32(b3);
                                if (this.jDebug)
                                {
                                    Debug.Log(string.Concat(new object[]
                                    {
                                "LIM CONVERTED DURATION: ",
                                num2,
                                " : ",
                                b3
                                    }));
                                }
                                this.getLIMInfoCallback(b2, num2, new string(array));
                            }
                            goto IL_D02;
                        }
                        if (b != 32)
                        {
                            goto IL_D02;
                        }
                        for (int m = 0; m < 16; m++)
                        {
                            this.mapping[m] = data[m + 1];
                        }
                        this.config3flag = true;
                        if (this.debugOutput)
                        {
                            Debug.LogWarning(string.Concat(new object[]
                            {
                        "Mapping: ",
                        data[1],
                        ",",
                        data[2],
                        ",",
                        data[3],
                        ",",
                        data[4],
                        " | ",
                        data[5],
                        ",",
                        data[6],
                        ",",
                        data[7],
                        ",",
                        data[8],
                        " | ",
                        data[9],
                        ",",
                        data[10],
                        ",",
                        data[11],
                        ",",
                        data[12],
                        " | ",
                        data[13],
                        ",",
                        data[14],
                        ",",
                        data[15],
                        ",",
                        data[16]
                            }));
                        }
                        goto IL_D02;
                    case 29:
                        this.timeDate = string.Format("{0}{1}:{2}{3}{4} onMB_GetServoMapping {5}/{6}/{7}{8}{9}{10}", new object[]
                        {
                    (data[1] != 0) ? data[1].ToString() : string.Empty,
                    data[2].ToString(),
                    data[3].ToString(),
                    data[4].ToString(),
                    (data[5] != 0) ? "pm" : "am",
                    data[7].ToString(),
                    data[8].ToString(),
                    data[9].ToString(),
                    data[10].ToString(),
                    data[11].ToString(),
                    data[12].ToString()
                        });
                        this.timeDateflag = true;
                        goto IL_D02;
                }
                goto IL_E0;*/
            case 7:
                {
                    this.meccaName = string.Empty;
                    int num3 = 1;
                    while (num3 <= 16 && data[num3] >= 32 && data[1] <= 126)
                    {
                        this.meccaName += (char)data[num3];
                        num3++;
                    }
                    this.nameflag = true;
                    goto IL_D02;
                }
            case 9:
                for (int n = 0; n < 8; n++)
                {
                    this.actualPos[n] = data[n + 1];
                }
                if (this.getServoPosCallback != null)
                {
                    this.getServoPosCallback();
                }
                goto IL_D02;
        }
        //goto IL_C6;
    IL_D02:
        if (this.resultsCallback != null)
        {
            this.resultsCallback(data);
        }
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

    public void setRobotType(MODEL type)
    {
        this.robotType = (byte)type;
    }

    private void updateMeccaButtons()
    {
        this.meccaBlueButton = ((this.status[7] & 1) != 0);
        this.meccaRedButton = ((this.status[7] & 2) != 0);
        this.meccaGreenButton = ((this.status[7] & 4) != 0);
        this.meccaYellowButton = ((this.status[7] & 8) != 0);
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
        byte[] expr_07 = new byte[20];
        expr_07[0] = command;
        expr_07[1] = data;
        expr_07[2] = data;
        expr_07[3] = data;
        expr_07[4] = data;
        expr_07[5] = data;
        expr_07[6] = data;
        expr_07[7] = data;
        expr_07[8] = data;
        expr_07[9] = data;
        expr_07[10] = data;
        expr_07[11] = data;
        expr_07[12] = data;
        expr_07[13] = data;
        expr_07[14] = data;
        expr_07[15] = data;
        expr_07[16] = data;
        expr_07[17] = data;
        byte[] ch = expr_07;
        this.SendCommand(ch, queue);
    }

    public void SendCommand(byte command, byte data)
    {
        byte[] expr_07 = new byte[20];
        expr_07[0] = command;
        expr_07[1] = data;
        expr_07[2] = data;
        expr_07[3] = data;
        expr_07[4] = data;
        expr_07[5] = data;
        expr_07[6] = data;
        expr_07[7] = data;
        expr_07[8] = data;
        expr_07[9] = data;
        expr_07[10] = data;
        expr_07[11] = data;
        expr_07[12] = data;
        expr_07[13] = data;
        expr_07[14] = data;
        expr_07[15] = data;
        expr_07[16] = data;
        expr_07[17] = data;
        byte[] ch = expr_07;
        this.SendCommand(ch);
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
            string str = string.Empty;
            byte[] array = ch1;
            for (int i = 0; i < array.Length; i++)
            {
                byte b = array[i];
                str += string.Format("{0:X2}", b);
            }
        }
        if (!queue)
        {
            BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, ch1, 20, this.ack, new Action<string>(this.Result));
            if (ch1[0] == 11)
            {
                BluetoothLE.writeState = (ch1[1] == 0 || ch1[1] == 2);
            }
        }
        else
        {
            this.commandQueue.Add(ch1);
        }
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
        byte[] expr_12 = new byte[20];
        expr_12[0] = 9;
        byte[] array = expr_12;
        this.calculateChecksum(ref array);
        if (!queue)
        {
            BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, array, 20, this.ack, new Action<string>(this.Result));
        }
        else
        {
            this.commandQueue.Add(array);
        }
    }

    public void setServoPos()
    {
        this.setServoPos(true);
    }

    private void setServoPos(bool queue)
    {
        byte[] array = new byte[]
        {
            8,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0
        };
        for (int i = 0; i < 8; i++)
        {
            array[i + 1] = (this.actualPos[i] = this.adjust(this.actualPos[i], this.targetPos[i]));
        }
        this.calculateChecksum(ref array);
        if (BluetoothLE.connectState && BluetoothLE.writeState)
        {
            if (!queue)
            {
                BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, array, 20, this.ack, new Action<string>(this.setResult));
            }
            else
            {
                this.commandQueue.Add(array);
            }
            StringBuilder stringBuilder = new StringBuilder();
            for (int j = 1; j <= 8; j++)
            {
                stringBuilder.Append(array[j].ToString("X2") + " ");
            }
            if (this.debugOutput)
            {
                Debug.Log(string.Concat(new object[]
                {
                    "*** setServoPos: ",
                    stringBuilder.ToString(),
                    " (",
                    Time.realtimeSinceStartup,
                    ")"
                }));
            }
        }
    }

    public void setServoMode(byte mode)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 11;
        expr_12[1] = mode;
        expr_12[2] = mode;
        expr_12[3] = mode;
        expr_12[4] = mode;
        expr_12[5] = mode;
        expr_12[6] = mode;
        expr_12[7] = mode;
        expr_12[8] = mode;
        expr_12[9] = mode;
        expr_12[10] = mode;
        expr_12[11] = mode;
        expr_12[12] = mode;
        expr_12[13] = mode;
        expr_12[14] = mode;
        expr_12[15] = mode;
        expr_12[16] = mode;
        byte[] item = expr_12;
        this.calculateChecksum(ref item);
        this.commandQueue.Add(item);
    }

    private void setMotorValues()
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 13;
        expr_12[3] = (byte)this.motorOutputSpeed;
        expr_12[4] = (byte)this.motorOutputSpeed;
        expr_12[5] = 255;
        expr_12[6] = 255;
        byte[] array = expr_12;
        if (this.motors[0])
        {
            array[1] = 1;
        }
        else if (this.motors[1])
        {
            array[1] = 2;
        }
        else
        {
            array[3] = 0;
        }
        if (this.motors[2])
        {
            array[2] = 1;
        }
        else if (this.motors[3])
        {
            array[2] = 2;
        }
        else
        {
            array[4] = 0;
        }
        this.calculateChecksum(ref array);
        BluetoothLEHardwareInterface.WriteCharacteristic(this.uuids[this.connectedDevice], this.serviceUUID, this.characteristicUUID, array, 20, this.ack, new Action<string>(this.Result));
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[]
            {
                "setMotorValues: ",
                array[1],
                " (",
                array[3],
                ") - ",
                array[2],
                " (",
                array[4],
                ")"
            }));
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
        byte[] expr_12 = new byte[20];
        expr_12[0] = 25;
        expr_12[1] = index;
        expr_12[2] = sub;
        byte[] item = expr_12;
        this.calculateChecksum(ref item);
        this.commandQueue.Add(item);
        if (this.debugOutput)
        {
            Debug.Log("playPreset(" + index + ") called");
        }
    }

    public void recordLIM(byte index)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 22;
        expr_12[1] = index;
        byte[] item = expr_12;
        this.calculateChecksum(ref item);
        this.commandQueue.Add(item);
        if (this.debugOutput)
        {
            Debug.Log("recordLIM(" + index + ") called");
        }
    }

    public void playLIM(byte index)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 21;
        expr_12[1] = index;
        byte[] item = expr_12;
        this.calculateChecksum(ref item);
        this.commandQueue.Add(item);
        if (this.debugOutput)
        {
            Debug.Log("playLIM(" + index + ") called");
        }
    }

    public void getLIMInfo(byte index)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 19;
        expr_12[1] = index;
        byte[] item = expr_12;
        this.calculateChecksum(ref item);
        this.commandQueue.Add(item);
        if (this.debugOutput || this.jDebug)
        {
            Debug.Log("getLIMInfo(" + index + ") called");
        }
    }

    public void deleteLIM(byte index, string name = "")
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] expr_12 = new byte[20];
        expr_12[0] = 23;
        expr_12[1] = index;
        byte[] array = expr_12;
        char[] array2 = name.ToCharArray();
        for (int i = 0; i < array2.Length; i++)
        {
            array[i + 3] = (byte)array2[i];
        }
        this.calculateChecksum(ref array);
        this.commandQueue.Add(array);
        if (this.jDebug || this.debugOutput)
        {
            Debug.Log("deleteLIM(" + index + ") called");
        }
    }

    public void changeLIMName(byte index, string name)
    {
        if (!BluetoothLE.connectState)
        {
            return;
        }
        byte[] array = new byte[15];
        for (int i = 0; i < Math.Min(name.Length, array.Length); i++)
        {
            array[i] = Convert.ToByte(name[i]);
        }
        byte[] expr_48 = new byte[20];
        expr_48[0] = 20;
        expr_48[1] = index;
        expr_48[3] = array[0];
        expr_48[4] = array[1];
        expr_48[5] = array[2];
        expr_48[6] = array[3];
        expr_48[7] = array[4];
        expr_48[8] = array[5];
        expr_48[9] = array[6];
        expr_48[10] = array[7];
        expr_48[11] = array[8];
        expr_48[12] = array[9];
        expr_48[13] = array[10];
        expr_48[14] = array[11];
        expr_48[15] = array[12];
        expr_48[16] = array[13];
        expr_48[17] = array[14];
        byte[] item = expr_48;
        this.calculateChecksum(ref item);
        this.commandQueue.Add(item);
        if (this.debugOutput)
        {
            Debug.Log("renameLIM(" + index + ") called");
        }
    }

    public void Result(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log("*** Result: " + msg);
        }
    }

    public void setResult(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log("*** setResult: " + msg);
        }
    }

    public void Service(string a, string b)
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE Service: " + a + ", " + b);
        }
    }

    public void Characteristic(string a, string b, string c)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new string[]
            {
                "*** BLE Characteristic: ",
                a,
                ", ",
                b,
                ", ",
                c
            }));
        }
        if (this.isMatch(b, this.serviceUUID) && this.isMatch(c, this.subscribeUUID))
        {
            this.Subscribe();
        }
    }

    public bool isMatch(string a, string b)
    {
        if (a == null || b == null)
        {
            return false;
        }
        string[] array = a.Split(new char[]
        {
            '-'
        });
        string[] array2 = b.Split(new char[]
        {
            '-'
        });
        int num;
        int num2;
        return array.Length != 0 && array2.Length != 0 && int.TryParse(array[0], NumberStyles.HexNumber, null, out num) && int.TryParse(array2[0], NumberStyles.HexNumber, null, out num2) && num == num2;
    }

    public void Error(string msg)
    {
        if (this.debugOutput)
        {
            Debug.Log("*** BLE error: " + msg);
        }
    }

    public Color getColor(int one, int two)
    {
        int num = one & 7;
        int num2 = one >> 3 & 7;
        int num3 = two & 7;
        return new Color((float)num / 7f, (float)num2 / 7f, (float)num3 / 7f);
    }

    public byte adjust(byte actual, byte target)
    {
        if (Mathf.Abs((int)(actual - target)) <= (int)this.maxSpeed)
        {
            return target;
        }
        return (actual >= target) ? (byte)(actual - this.maxSpeed) : (byte)(actual + this.maxSpeed);
    }

    public void calculateChecksum(ref byte[] data)
    {
        int num = 0;
        for (int i = 0; i < data.Length - 2; i++)
        {
            num += (int)data[i];
        }
        data[data.Length - 2] = (byte)((num & 65280) >> 8);
        data[data.Length - 1] = (byte)(num & 255);
    }

    public bool IsDeviceConnected()
    {
        return this.connectedDevice != -1;
    }

    public void ClearConnectedDevice()
    {
        this.connectedDevice = -1;
    }

    public void ResetQueue()
    {
        Debug.Log("clear queue: " + this.commandQueue.Count);
        this.commandQueue.Clear();
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

    public bool IsDrone()
    {
        return this.robotType == 0 || this.robotType == 3;
    }
}

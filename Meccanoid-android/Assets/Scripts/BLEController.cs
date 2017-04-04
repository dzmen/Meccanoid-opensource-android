using System;
using UnityEngine;

public class BLEController : MonoBehaviour
{
    private const int framesPerSecond = 10;

    public BluetoothLE BLE;

    //public EyeColor eyeColor;

    public bool debugOutput;

    private float updateInterval = 0.1f;

    private float lastInterval;

    private float commandTimer;

    private bool[] motors;

    private bool available = true;

    private static int[] servoIndex;

    private byte[] servo_ch1 = new byte[] { 12, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 0, 0, 0 };

    private byte[] button_ch1;

    static BLEController()
    {
        BLEController.servoIndex = new int[] { 2, 1, 0, -1, 6, 7, -1, -1, 3, 4, 5, -1, -1, -1, -1, -1 };
    }

    public BLEController()
    {
        this.updateInterval = 0.1f;
        this.available = true;
        this.servo_ch1 = new byte[] { 12, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 0, 0, 0 };
        byte[] numArray = new byte[20];
        numArray[0] = 28;
        this.button_ch1 = numArray;
    }

    public bool Available()
    {
        return this.available;
    }

    private int colorFloatToInt(float color)
    {
        return (int)Mathf.Clamp(color * 8f, 0f, 7f);
    }

    public Color getColor()
    {
        return new Color(0f, 0f, 0f);
    }

    public int getServoIndex(int module, int slot)
    {
        if (null == this.BLE || !this.BLE.getConnectState() || !this.BLE.IsDrone())
        {
            return BLEController.servoIndex[Mathf.Clamp(module * 4 + slot, 0, 15)];
        }
        byte num = (byte)((module & 15) << 4 | slot & 15);
        for (int i = 0; i < 16; i++)
        {
            if (num == this.BLE.mapping[i])
            {
                return i;
            }
        }
        return -1;
    }

    public void getSound(float time, Action callback)
    {
        if (this.debugOutput)
        {
            Debug.Log("ScriptControl.getSound called");
        }
    }

    public bool getTouch()
    {
        return false;
    }

    private void OnDisable()
    {
        this.setMotors(false);
    }

    private void OnEnable()
    {
        this.motors = new bool[4];
    }

    public void resetLEDs()
    {
        for (int i = 1; i <= 16; i++)
        {
            this.servo_ch1[i] = 4;
        }
        if (null != this.BLE)
        {
            this.BLE.SendCommand(this.servo_ch1);
        }
    }

    public void ServosReset()
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                this.setServo(i, j, 0f);
            }
        }
    }

    public void setColor(int source, Color color)
    {
        this.setColor(source, color, 0);
    }

    public void setColor(int source, Color color, int time)
    {
        if (this.debugOutput)
        {
            Debug.Log("ScriptControl.setColor called");
        }
        int num = source & 7;
        int num1 = this.colorFloatToInt(color.g) << 3 | this.colorFloatToInt(color.r);
        int num2 = (time & 7) << 3 | this.colorFloatToInt(color.b);
        byte[] numArray = new byte[20];
        numArray[0] = 17;
        numArray[1 + num * 2] = (byte)num1;
        numArray[2 + num * 2] = (byte)num2;
        if (null != this.BLE)
        {
            this.BLE.SendCommand(numArray);
        }
        //if (null != this.eyeColor)
        //{
        //    this.eyeColor.setColor(num, color, time);
        //}
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("int1: ", Convert.ToString(num1, 2).PadLeft(8, '0'), " - int2: ", Convert.ToString(num2, 2).PadLeft(8, '0')));
        }
    }

    private void setMotors()
    {
        this.setMotors(true);
    }

    private void setMotors(bool existing)
    {
        if (!existing)
        {
            this.motors = new bool[4];
        }
        if (null != this.BLE)
        {
            this.BLE.setMotors(this.motors);
        }
    }

    public void setMove(bool forward, float distance, float speed)
    {
        bool flag;
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "ScriptControl.setMove called: ", forward, " - distance: ", distance, " - speed: ", speed }));
        }
        if (!forward)
        {
            this.motors[3] = true;
            this.motors[1] = true;
        }
        else
        {
            this.motors[2] = true;
            this.motors[0] = true;
        }
        this.commandTimer = Time.realtimeSinceStartup + distance;
        this.available = false;
        this.setMotors();
    }

    public void setMoveLeft(bool front, bool rear)
    {
        if (this.motors[0] == front && this.motors[1] == rear)
        {
            return;
        }
        this.motors[0] = front;
        this.motors[1] = rear;
        this.setMotors();
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "setMoveLeft: ", front, ", ", rear }));
        }
    }

    public void setMoveRight(bool front, bool rear)
    {
        if (this.motors[2] == front && this.motors[3] == rear)
        {
            return;
        }
        this.motors[2] = front;
        this.motors[3] = rear;
        this.setMotors();
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "setMoveRight: ", front, ", ", rear }));
        }
    }

    public void setPCBLEDs(bool led0_Blue, bool led1_Red, bool led2_Green, bool led3_Yellow)
    {
        object obj;
        object obj1;
        object obj2;
        object obj3;
        byte[] numArray = (byte[])this.button_ch1.Clone();
        byte[] buttonCh1 = this.button_ch1;
        if (!led0_Blue)
        {
            obj = null;
        }
        else
        {
            obj = 1;
        }
        buttonCh1[1] = (byte)obj;
        byte[] buttonCh11 = this.button_ch1;
        if (!led1_Red)
        {
            obj1 = null;
        }
        else
        {
            obj1 = 1;
        }
        buttonCh11[2] = (byte)obj1;
        byte[] numArray1 = this.button_ch1;
        if (!led2_Green)
        {
            obj2 = null;
        }
        else
        {
            obj2 = 1;
        }
        numArray1[3] = (byte)obj2;
        byte[] buttonCh12 = this.button_ch1;
        if (!led3_Yellow)
        {
            obj3 = null;
        }
        else
        {
            obj3 = 1;
        }
        buttonCh12[4] = (byte)obj3;
        if (numArray[1] == this.button_ch1[1] && numArray[2] == this.button_ch1[2] && numArray[3] == this.button_ch1[3] && numArray[4] == this.button_ch1[4])
        {
            return;
        }
        if (null != this.BLE)
        {
            this.BLE.SendCommand(this.button_ch1);
        }
        //if (null != this.eyeColor)
        //{
        //    this.eyeColor.setButtons(led1_Red, led2_Green, led0_Blue, led3_Yellow);
        //}
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "setPCBLEDs called (", led0_Blue, ", ", led1_Red, ", ", led2_Green, ", ", led3_Yellow, ")" }));
        }
    }

    public void setPCBLEDs(int slot, bool on)
    {
        object obj;
        byte[] numArray = (byte[])this.button_ch1.Clone();
        slot = Mathf.Clamp(slot, 0, 3) + 1;
        byte[] buttonCh1 = this.button_ch1;
        int num = slot;
        if (!on)
        {
            obj = null;
        }
        else
        {
            obj = 1;
        }
        buttonCh1[num] = (byte)obj;
        bool flag = this.button_ch1[1] != 0;
        bool buttonCh11 = this.button_ch1[2] != 0;
        bool flag1 = this.button_ch1[3] != 0;
        bool buttonCh12 = this.button_ch1[4] != 0;
        if (numArray[1] == this.button_ch1[1] && numArray[2] == this.button_ch1[2] && numArray[3] == this.button_ch1[3] && numArray[4] == this.button_ch1[4])
        {
            return;
        }
        if (null != this.BLE)
        {
            this.BLE.SendCommand(this.button_ch1);
        }
        //if (null != this.eyeColor)
        //{
        //    this.eyeColor.setButtons(flag, buttonCh11, flag1, buttonCh12);
        //}
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "setPCBLEDs called (", slot, ", ", on, ")" }));
        }
    }

    public void setPose(byte[] servos)
    {
        this.setPose(servos, 0f);
    }

    public void setPose(byte[] servos, float time)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "ScriptControl.setPose called: ", (int)servos.Length, " - time: ", time }));
        }
    }

    public void setRagdoll(bool enable)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("ScriptControl.setRagdoll called:", enable));
        }
    }

    public void setRotate(bool clockwise, float angle, float speed)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "ScriptControl.setRotate called: ", clockwise, " - angle: ", angle, " - speed: ", speed }));
        }
        if (!clockwise)
        {
            this.motors[2] = true;
        }
        else
        {
            this.motors[0] = true;
        }
        this.commandTimer = Time.realtimeSinceStartup + angle;
        this.available = false;
        this.setMotors();
    }

    public void setServo(int module, int slot, float angle)
    {
        bool flag = (this.BLE.robotType == 1 ? true : this.BLE.robotType == 4);
        int servoIndex = this.getServoIndex(module, slot);
        if (servoIndex < 0)
        {
            Debug.LogWarning(string.Concat(new object[] { "rejected setServo: ", module, ", ", slot }));
            return;
        }
        angle = -angle;
        angle = (90f + angle) / 180f;
        angle = Mathf.Clamp(angle, 0f, 1f);
        byte num = (byte)(24f + (float)((byte)(angle * 208f)));
        this.BLE.settargetPos(servoIndex, num);
    }

    public void setServoColor(int source, int color)
    {
        color = Mathf.Clamp(color, 0, 7);
        for (int i = 0; i < 8; i++)
        {
            if ((source & 1 << (i & 31)) != 0)
            {
                if (this.servo_ch1[i + 1] == (byte)color)
                {
                    return;
                }
                this.servo_ch1[i + 1] = (byte)color;
            }
        }
        if (null != this.BLE)
        {
            this.BLE.SendCommand(this.servo_ch1);
        }
        //if (null != this.eyeColor)
        //{
        //    this.eyeColor.setServos(source, color);
        //}
        if (this.debugOutput)
        {
            Debug.Log(string.Concat(new object[] { "setServoColor(", source, ", ", color, ")" }));
        }
    }

    public void setServoColor(int source, Color color)
    {
        this.setServoColor(source, Mathf.RoundToInt(color.r) + Mathf.RoundToInt(color.g) * 2 + Mathf.RoundToInt(color.b) * 4);
    }

    public void setServoColor(int module, int slot, Color color)
    {
        if (module < 0)
        {
            return;
        }
        int servoIndex = this.getServoIndex(module, slot);
        if (servoIndex >= 0)
        {
            this.setServoColor(1 << (servoIndex & 31), color);
            return;
        }
        Debug.LogWarning(string.Concat(new object[] { "rejected setServoColor: ", module, ", ", slot }));
    }

    public void setServoIndex()
    {
        if (null == this.BLE || !this.BLE.getConnectState() || !this.BLE.IsDrone())
        {
            return;
        }
        Array.Clear(this.BLE.mapping, 255, (int)this.BLE.mapping.Length);
        int num = 0;
        for (int i = 0; i < 8 && num < 16; i++)
        {
            for (int j = 0; j < 4 && num < 16; j++)
            {
                if (this.BLE.config[i * 4 + j] == 1)
                {
                    int num1 = num;
                    num = num1 + 1;
                    this.BLE.mapping[num1] = (byte)((i & 15) << 4 | j & 15);
                    Debug.Log(string.Concat(new object[] { "MAPPING : ", num - 1, " : ", this.BLE.mapping[num - 1] }));
                }
            }
        }
        if (num > 0)
        {
            Debug.Log("INDEX GOOD, SET MAPPING VIA BLE");
            this.BLE.setMapping();
        }
    }

    public void setSound(string effect)
    {
        if (this.debugOutput)
        {
            Debug.Log(string.Concat("ScriptControl.setSound called: ", effect));
        }
    }

    public void stopMotors()
    {
        this.setMotors(false);
    }

    private void Update()
    {
        float _realtimeSinceStartup = Time.realtimeSinceStartup;
        //_realtimeSinceStartup <= lastInterval + updateInterval;
        if (_realtimeSinceStartup < this.commandTimer)
        {
            return;
        }
        this.available = true;
        this.lastInterval = _realtimeSinceStartup;
    }
}
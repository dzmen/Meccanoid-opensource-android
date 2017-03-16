using System;
using UnityEngine;

public class CommandMotors : MonoBehaviour
{
    public BluetoothLE BLE;

    private bool[] motors;

    public bool[] getMotors()
    {
        return this.motors;
    }

    public void leftBackwardPressed()
    {
        this.motors[1] = true;
        this.setMotors();
    }

    public void leftBackwardReleased()
    {
        this.motors[1] = false;
        this.setMotors();
    }

    public void leftForwardPressed()
    {
        this.motors[0] = true;
        this.setMotors();
    }

    public void leftForwardReleased()
    {
        this.motors[0] = false;
        this.setMotors();
    }

    public void leftReleased()
    {
        this.motors[0] = false;
        this.motors[1] = false;
        this.setMotors();
    }

    private void OnDisable()
    {
        this.motors = new bool[4];
        if (null != this.BLE)
        {
            this.BLE.setMotors(this.motors);
        }
    }

    private void OnEnable()
    {
        this.motors = new bool[4];
    }

    public void Released()
    {
        this.OnDisable();
    }

    public void rightBackwardPressed()
    {
        this.motors[3] = true;
        this.setMotors();
    }

    public void rightBackwardReleased()
    {
        this.motors[3] = false;
        this.setMotors();
    }

    public void rightForwardPressed()
    {
        this.motors[2] = true;
        this.setMotors();
    }

    public void rightForwardReleased()
    {
        this.motors[2] = false;
        this.setMotors();
    }

    public void rightReleased()
    {
        this.motors[2] = false;
        this.motors[3] = false;
        this.setMotors();
    }

    private void setMotors()
    {
        bool[] flagArray = new bool[] { this.motors[0], this.motors[1], this.motors[2], this.motors[3] };
        if (flagArray[0] == flagArray[1])
        {
            flagArray[1] = false;
            flagArray[0] = false ;
        }
        if (flagArray[2] == flagArray[3])
        {
            flagArray[3] = false;
            flagArray[2] = false;
        }
        if (null != this.BLE)
        {
            this.BLE.setMotors(flagArray);
        }
    }
}
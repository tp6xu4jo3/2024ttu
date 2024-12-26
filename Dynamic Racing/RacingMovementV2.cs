using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class RacingMovementV2 : MonoBehaviour
{
    [Header("Wheel GameObject Meshes")]
    public GameObject FrontWheelLeft;
    public GameObject FrontWheelRight;
    public GameObject BackWheelLeft;
    public GameObject BackWheelRight;

    [Header("WheelCollider")]
    public WheelCollider FrontWheelLeftCollider;
    public WheelCollider FrontWheelRightCollider;
    public WheelCollider BackWheelLeftCollider;
    public WheelCollider BackWheelRightCollider;

    [Header("Movemwnt, Steering and Braking")]
    private float currentSpeed;
    float maximumMotorTorque = 1000f;
    float maximumSteeringAngle = 30f;
    float maximumSpeed = 5000f;
    float brakePower = 5000f;
    public Transform COM;
    float carSpeed;
    float carSpeedConverted;
    float motorTorque;
    Rigidbody carRigidBody;

    int vehicleMass = 1000;

    float mpu1X;
    float mpu2X;
    float mpu3X;

    [Header("Sounds & Effect")]
    public ParticleSystem[] smokeEffects;
    private bool smokeEffectEnabled;

    public AudioSource engineSound;
    public AudioClip engineClip;

    private void Start()
    {
        smokeEffectEnabled = false;

        carRigidBody = GetComponent<Rigidbody>();

        if (carRigidBody != null)
        {
            carRigidBody.centerOfMass = COM.localPosition;
        }

        engineSound.loop = true;
        engineSound.playOnAwake = false;
        engineSound.volume = 100f;
        engineSound.pitch = 1f;
        engineSound.Play();

    }
    void Update()
    {
        CalculateCarMovement();
        ApplySteering();
        ApplyTransformToWheels();
    }

    public void UpdateBLEData(float receivedMPU1X, float receivedMPU2X, float receivedMPU3X)
    {
        mpu1X = receivedMPU1X;
        mpu2X = receivedMPU2X;
        mpu3X = receivedMPU3X;
    }

    void CalculateCarMovement()
    {
        //carSpeed = carRigidBody.velocity.magnitude;
        //carSpeedConverted = Mathf.Round(carSpeed * 3.6f);

        if (Mathf.Abs(mpu1X) > 20f)
        {
            motorTorque = 0;
            ApplyBrake();
            if (!smokeEffectEnabled)
            {
                EnableSmokeEffect(true);
                smokeEffectEnabled = true;
            }
        }
        else
        {
            ReleaseBrake();

            if (smokeEffectEnabled)
            {
                EnableSmokeEffect(false);
                smokeEffectEnabled = false;
            }
            UpdateEngineSound();
            ApplyMotorTorque();
            ApplyThrottle();
        }
    }
    void UpdateEngineSound()
    {
        float volumeMultiplier = Mathf.InverseLerp(0, maximumMotorTorque, Mathf.Abs(motorTorque));
        engineSound.volume = Mathf.Lerp(100f, 500f, volumeMultiplier);

        float pitchMultiplier = Mathf.InverseLerp(0, maximumMotorTorque, Mathf.Abs(motorTorque));
        engineSound.pitch = Mathf.Lerp(-1f, 3f, pitchMultiplier); 
    }
    void ApplySteering()
    {
        float clampedInput = Mathf.Clamp(mpu2X, -45f, 45f);
        float targetSteeringAngle = Mathf.Lerp(-maximumSteeringAngle, maximumSteeringAngle, (clampedInput + 45f) / 90f);
        float currentSteeringAngle = Mathf.Lerp(FrontWheelLeftCollider.steerAngle, targetSteeringAngle, Time.deltaTime * 10f);

        FrontWheelLeftCollider.steerAngle = currentSteeringAngle;
        FrontWheelRightCollider.steerAngle = currentSteeringAngle;
    }

    void ApplyMotorTorque()
    {
        BackWheelLeftCollider.motorTorque = motorTorque;
        BackWheelRightCollider.motorTorque = motorTorque;
    }

    void ApplyThrottle()
    {
        // 限制 mpu3X 在 -10 到 30 度之間
        float clampedInput = Mathf.Clamp(mpu3X, -10f, 30f);

        // 判斷車輛的加速模式
        float throttleInput;
        if (clampedInput >= 0f)
        {
            throttleInput = Mathf.InverseLerp(0f, 30f, clampedInput);
        }
        else
        {
            // 倒車：將 -10 到 0 度映射到 -1 到 0
            throttleInput = Mathf.InverseLerp(-10f, 0f, clampedInput) - 1f;
        }

        // 設定馬達扭矩和速度限制
        float motorTorque = throttleInput * maximumMotorTorque; // 馬達扭矩由輸入控制
        if (throttleInput >= 0f && carSpeedConverted < maximumSpeed)
        {
            // 正向加速時的扭矩
            BackWheelLeftCollider.motorTorque = motorTorque;
            BackWheelRightCollider.motorTorque = motorTorque;
        }
        else if (throttleInput < 0f && carSpeedConverted > -maximumSpeed / 2f)
        {
            // 增加倒車轉換的緩衝
            BackWheelLeftCollider.motorTorque = Mathf.Lerp(BackWheelLeftCollider.motorTorque, motorTorque, Time.deltaTime * 3f);
            BackWheelRightCollider.motorTorque = Mathf.Lerp(BackWheelRightCollider.motorTorque, motorTorque, Time.deltaTime * 3f);
        }
        else
        {
            // 停止加速
            BackWheelLeftCollider.motorTorque = 0;
            BackWheelRightCollider.motorTorque = 0;
        }
    }


    public void ApplyBrake() 
    {
        FrontWheelLeftCollider.brakeTorque = brakePower;
        FrontWheelRightCollider.brakeTorque = brakePower;
        BackWheelLeftCollider.brakeTorque = brakePower;
        BackWheelRightCollider.brakeTorque = brakePower;
    }

    void ReleaseBrake()
    {
        FrontWheelLeftCollider.brakeTorque = 0;
        FrontWheelRightCollider.brakeTorque = 0;
        BackWheelLeftCollider.brakeTorque = 0;
        BackWheelRightCollider.brakeTorque = 0;
    }

    public void ApplyTransformToWheels()
    {
        Vector3 position;
        Quaternion rotation;

        FrontWheelLeftCollider.GetWorldPose(out position, out rotation);
        FrontWheelLeft.transform.position = position;
        FrontWheelLeft.transform.rotation = rotation;

        FrontWheelRightCollider.GetWorldPose(out position, out rotation);
        FrontWheelRight.transform.position = position;
        FrontWheelRight.transform.rotation = rotation;

        BackWheelLeftCollider.GetWorldPose(out position, out rotation);
        BackWheelLeft.transform.position = position;
        BackWheelLeft.transform.rotation = rotation;

        BackWheelRightCollider.GetWorldPose(out position, out rotation);
        BackWheelRight.transform.position = position;
        BackWheelRight.transform.rotation = rotation;
    }

    private void EnableSmokeEffect(bool enable)
    {
        if (smokeEffectEnabled != enable)
        {
            smokeEffectEnabled = enable;
            foreach (ParticleSystem smokeEffect in smokeEffects)
            {
                if (enable)
                {
                    smokeEffect.Play();
                }
                else
                {
                    smokeEffect.Stop();
                }
            }
        }
    }
}

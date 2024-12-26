using UnityEngine;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class BLEManager : MonoBehaviour
{
    public string DeviceName = "ESP32"; // 修改为 ESP32 的 BLE 设备名
    public string ServiceUUID = "4fafc201-1fb5-459e-8fcc-c5c9c331914b";
    public string CharacteristicUUID = "beb5483e-36e1-4688-b7f5-ea07361b26a8";

    public RacingMovementV2 racingMovementV2;

    public TextMeshProUGUI LoadingTextTMP;
    public TextMeshProUGUI AngleTextTMP;// TextMeshPro 元素
    public GameObject canvas;           // 用于显示 Debug 信息的 Canvas

    enum States
    {
        None,
        Scan,
        Connect,
        Subscribe,
        Communication,
        Disconnect
    }

    private States _state = States.None;
    private string _connectedDeviceAddress;
    private bool _connected = false;

    private float _timeout = 0f;


    void AppendDebugText(string message)
    {
        if (LoadingTextTMP != null && AngleTextTMP != null)
        {
            LoadingTextTMP.text = message;
            AngleTextTMP.text = message;
        }
        Debug.Log(message); // 同时打印到控制台
    }

    void Reset()
    {
        _state = States.None;
        _connectedDeviceAddress = null;
        _connected = false;
    }

    void SetState(States newState, float timeout)
    {
        _state = newState;
        _timeout = timeout;
    }

    void StartProcess()
    {
        Reset();

        BluetoothLEHardwareInterface.Initialize(true, false, () =>
        {
            SetState(States.Scan, 0.1f);
            AppendDebugText("BLE Initialized");
        },
        (error) =>
        {
            AppendDebugText($"BLE Initialization Error: {error}");
        });
    }

    void Start()
    {
        canvas.SetActive(true); // 启动时显示 Canvas
        StartProcess();
    }

    void Update()
    {
        if (_timeout > 0f)
        {
            _timeout -= Time.deltaTime;
            if (_timeout <= 0f)
            {
                _timeout = 0f;

                switch (_state)
                {
                    case States.Scan:
                        AppendDebugText("Subscribing to characteristic...");
                        BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
                        {
                            if (name.Contains(DeviceName))
                            {
                                AppendDebugText($"Found device: {name} ({address})");
                                _connectedDeviceAddress = address;
                                SetState(States.Connect, 0.5f);
                                BluetoothLEHardwareInterface.StopScan();
                            }
                        }, null);
                        break;

                    case States.Connect:
                        AppendDebugText("Connecting to device...");
                        BluetoothLEHardwareInterface.ConnectToPeripheral(_connectedDeviceAddress, null, null, (address, serviceUUID, characteristicUUID) =>
                        {
                            if (serviceUUID == ServiceUUID && characteristicUUID == CharacteristicUUID)
                            {
                                _connected = true;
                                SetState(States.Subscribe, 1f);
                                AppendDebugText("Connected to device.");
                            }
                        },
                        (disconnectedAddress) =>
                        {
                            AppendDebugText($"Disconnected from device: {disconnectedAddress}");
                            Reset();
                            SetState(States.Scan, 1f);
                        });
                        break;

                    case States.Subscribe:
                        AppendDebugText("Subscribing to characteristic...");
                        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_connectedDeviceAddress, ServiceUUID, CharacteristicUUID, null, (address, characteristicUUID, bytes) =>
                        {
                            if (bytes.Length == 12) // 确保接收到的数据长度正确
                            {
                                float mpu1X = System.BitConverter.ToSingle(bytes, 0);
                                float mpu2X = System.BitConverter.ToSingle(bytes, 4);
                                float mpu3X = System.BitConverter.ToSingle(bytes, 8);

                                AppendDebugText($"Received Data: MPU1_X={mpu1X}, MPU2_X={mpu2X}, MPU3_X={mpu3X}");

                                // 将数据传递给 RacingMovement
                                if (racingMovementV2 != null)
                                {
                                    racingMovementV2.UpdateBLEData(mpu1X, mpu2X, mpu3X);
                                }
                                canvas.SetActive(false);
                            }
                            else
                            {
                                AppendDebugText("Invalid data length received.");
                            }
                        });
                        _state = States.Communication;
                        break;

                    case States.Communication:
                        break;

                    case States.Disconnect:
                        AppendDebugText("Disconnecting...");
                        BluetoothLEHardwareInterface.DisconnectPeripheral(_connectedDeviceAddress, (address) =>
                        {
                            BluetoothLEHardwareInterface.DeInitialize(() =>
                            {
                                AppendDebugText("Disconnected and Deinitialized");
                                Reset();
                                SetState(States.Scan, 1f);
                            });
                        });
                        break;
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        if (_connected)
        {
            BluetoothLEHardwareInterface.DisconnectPeripheral(_connectedDeviceAddress, (address) =>
            {
                BluetoothLEHardwareInterface.DeInitialize(() =>
                {
                    AppendDebugText("Disconnected and Deinitialized on quit.");
                });
            });
        }
        else
        {
            BluetoothLEHardwareInterface.DeInitialize(() =>
            {
                AppendDebugText("Deinitialized on quit.");
            });
        }
    }
}

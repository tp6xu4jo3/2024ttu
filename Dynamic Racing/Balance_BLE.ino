#include <Wire.h>
#include <PWFusion_TCA9548A.h>
#include <ESP32Servo.h>
#include <Arduino.h>
#include <TinyMPU6050.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <BLE2901.h>

#define SERVICE_UUID "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define CHARACTERISTIC_UUID "beb5483e-36e1-4688-b7f5-ea07361b26a8"

TCA9548A i2cMux;
#define TCA_I2C_SDA 21
#define TCA_I2C_SCL 22

Servo myservo;

MPU6050 mpu1(Wire, 0x68);
MPU6050 mpu2(Wire, 0x68);
MPU6050 mpu3(Wire, 0x68);

BLEServer *pServer = NULL;
BLECharacteristic *pCharacteristic = NULL;
BLE2901 *descriptor_2901 = NULL;
bool deviceConnected = false;
bool oldDeviceConnected = false;
uint32_t value = 0;

int rollServo = 0;
int angServo = 0;

float mpu1X = 0.0;
float mpu2X = 0.0;
float mpu3X = 0.0;

class MyServerCallbacks : public BLEServerCallbacks {
  void onConnect(BLEServer *pServer) {
    deviceConnected = true;
  };

  void onDisconnect(BLEServer *pServer) {
    deviceConnected = false;
  }
};

void setup() {
    Serial.begin(115200);
    Wire.begin(TCA_I2C_SDA, TCA_I2C_SCL);

    BLEDevice::init("ESP32");

    i2cMux.begin();

    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new MyServerCallbacks());

    BLEService* pService = pServer->createService(SERVICE_UUID);

    pCharacteristic = pService->createCharacteristic(
    CHARACTERISTIC_UUID,
    BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_INDICATE
    );

    pCharacteristic->addDescriptor(new BLE2902());

    descriptor_2901 = new BLE2901();
    descriptor_2901->setDescription("My own description for this characteristic.");
    descriptor_2901->setAccessPermissions(ESP_GATT_PERM_READ); 
    pCharacteristic->addDescriptor(descriptor_2901);

    pService->start();

    BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(false);
    pAdvertising->setMinPreferred(0x0); 
    BLEDevice::startAdvertising();
    Serial.println("Waiting a client connection to notify...");

    myservo.attach(26);
    myservo.write(90);

    initializeMPUs();

    calibrateMPUs();

    myservo.write(180);
    delay(1000);
    myservo.write(0);
    delay(1000);
    myservo.write(90);
}

void initializeMPUs() {
  i2cMux.setChannel(CHAN5);
  mpu1.Initialize();

  i2cMux.setChannel(CHAN6);
  mpu2.Initialize();

  i2cMux.setChannel(CHAN7);
  mpu3.Initialize();
}

void calibrateMPUs() {
    if (deviceConnected) {
        String calibrationMessage = "Calibrating_MPU6050";
        pCharacteristic->setValue(calibrationMessage.c_str());
        pCharacteristic->notify();
        Serial.println(calibrationMessage);
    }
    
    i2cMux.setChannel(CHAN5);
    mpu1.Calibrate();
  
    i2cMux.setChannel(CHAN6);
    mpu2.Calibrate();

    i2cMux.setChannel(CHAN7);
    mpu3.Calibrate();

    if (deviceConnected) {
        String completionMessage = "Calibration_Complete";
        pCharacteristic->setValue(completionMessage.c_str());
        pCharacteristic->notify();
        Serial.println(completionMessage);
    }
}

void calculateAndControlServo() {
  i2cMux.setChannel(CHAN5);
  mpu1.Execute();
  rollServo = mpu1.GetAngX();
  angServo = map(rollServo, -90, 90, 0, 180);
  myservo.write(angServo);
}

void connectBLE() {
    if (deviceConnected) {
        i2cMux.setChannel(CHAN5);
        mpu1.Execute();
        mpu1X = mpu1.GetAngX();

        i2cMux.setChannel(CHAN6);
        mpu2.Execute();
        mpu2X = mpu2.GetAngX();

        i2cMux.setChannel(CHAN7);
        mpu3.Execute();
        mpu3X = mpu3.GetAngX();

        String dataString = String(mpu1X, 2) + "," + 
                            String(mpu2X, 2) + "," + 
                            String(mpu3X, 2);
        
        pCharacteristic->setValue(dataString.c_str());
        pCharacteristic->notify();

        Serial.printf("Sent: %s\n", dataString.c_str());
    }

    if (!deviceConnected && oldDeviceConnected) {
        delay(500);
        pServer->startAdvertising();
        Serial.println("start advertising");
        oldDeviceConnected = deviceConnected;
    }

    if (deviceConnected && !oldDeviceConnected) {
        oldDeviceConnected = deviceConnected;
    }
}

void loop() {
  calculateAndControlServo();
  connectBLE();
}

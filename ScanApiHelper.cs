/*
 * Title:
 * ScanAPIHelper.cs
 * 
 * Copyright 2015 Socket Mobile, Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 *
 * Description:
 * this class provides a set of common functions to retrieve
 * or configure a scanner or ScanAPI and to receive decoded
 * data from a scanner.<p>
 * This helper manages a commands list so the application
 * can send multiple command in a row, the helper will send
 * them one at a time. Each command has an optional callback 
 * function that will be called each time a command complete.
 * By example, to get a device friendly name, use the 
 * PostGetFriendlyName method and pass a callback function in 
 * which you can update the UI with the newly fetched friendly 
 * name. This operation will be completely asynchronous.<p>
 * ScanAPI Helper manages a list of device information. Most of 
 * the time only one device is connected to the host. This list
 * could be configured to have always one item, that will be a 
 * "No device connected" item in the case where there is no device
 * connected, or simply a device name when there is one device
 * connected. Use isDeviceConnected method to know if there is at
 * least one device connected to the host.<br> 
 * Common usage scenario of ScanAPIHelper:<br>
 * <li> create an instance of ScanApiHelper: _scanApi=new ScanApiHelper();
 * <li> [optional] if a UI device list is used a no device connected 
 * string can be specified:_scanApi.setNoDeviceText(getString(R.string.no_device_connected));
 * <li> register for notification: _scanApi.setNotification(_scanApiNotification);
 * <li> derive from ScanApiHelperNotification to handle the notifications coming
 * from ScanAPI including "Device Arrival", "Device Removal", "Decoded Data" etc...
 * <li> open ScanAPI to start using it:_scanApi.open();
 * <li> check the ScanAPI initialization result in the notifications: 
 * _scanApiNotification.onScanApiInitializeComplete(long result){}
 * <li> monitor a scanner connection by using the notifications:
 * _scanApiNotification.onDeviceArrival(long result,DeviceInfo newDevice){}
 * _scanApiNotification.onDeviceRemoval(DeviceInfo deviceRemoved){}
 * <li> retrieve the decoded data from a scanner
 * _scanApiNotification.onDecodedData(DeviceInfo device,ISktScanDecodedData decodedData){}
 * <li> once the application is done using ScanAPI, close it using:
 * _scanApi.close();
 *
 * Revision 	Who 		History
 * 04/19/11		EricG		First release
 *
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using ScanAPI;

namespace ScanApiHelper
{
    internal class Debug
    {
        public const int kLevelTrace = 1;
        public const int kLevelWarning = 2;
        public const int kLevelError = 3;

        public static void MSG(int level, String traces)
        {
            switch (level)
            {
                case kLevelTrace:
                    System.Diagnostics.Debug.WriteLine(traces, "INFO");
                    break;
                case kLevelWarning:
                    System.Diagnostics.Debug.WriteLine(traces, "WARNING");
                    break;
                case kLevelError:
                    System.Diagnostics.Debug.WriteLine(traces, "ERROR");
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine(traces, "VERBOSE");
                    break;
            }
        }
    }

    public delegate void ICommandContextCallback(long result,ISktScanObject scanObj);
    
    class CommandContext
    {
	    public const int statusReady=1;
	    public const int statusNotCompleted=2;
	    public const int statusCompleted=3;
    	
	    private readonly ICommandContextCallback _callback;
	    private readonly bool _getOperation;
	    private readonly ISktScanObject _scanObj;
	    private int _status;
	    private readonly ISktScanDevice _scanDevice;
	    private int _retries;
	    private readonly DeviceInfo _deviceInfo;

        public CommandContext(bool getOperation,ISktScanObject scanObj,ISktScanDevice scanDevice,DeviceInfo deviceInfo,ICommandContextCallback callback){
		    this._getOperation=getOperation;
		    scanObj.Property.Context=this;
		    this._scanObj=scanObj;
		    this._callback=callback;
		    this._status=statusReady;
		    this._scanDevice=scanDevice;
		    this._retries=0;
		    this._deviceInfo=deviceInfo;
		    this.SymbologyId=0;
	    }
    	
	    public bool Operation
        {
            get { return _getOperation; }
	    }
    	
	    public ISktScanObject ScanObject
        {
		    get {return _scanObj;}
	    }
    	
	    public int Status
        {
            get { return _status; }
            set { _status=value; }
	    }

	    public int Retries
        {
            get { return _retries; }
	    }

	    public ISktScanDevice ScanDevice
        {
            get { return _scanDevice; }
	    }

	    public DeviceInfo DeviceInfo
        {
            get { return _deviceInfo; }
	    }

	    public void DoCallback(long result,ISktScanObject scanObj)
        {
		    _status=statusCompleted;
		    if(_callback!=null)
			    _callback(result,scanObj);
	    }

        public int SymbologyId { get; set; }


        public long DoGetOrSetProperty() {
		    long result=SktScanErrors.ESKT_NOERROR;
		    if(ScanDevice==null)
			    result=SktScanErrors.ESKT_INVALIDPARAMETER;
    		
		    if(SktScanErrors.SKTSUCCESS(result)){
			    if(Operation){
				    Debug.MSG(Debug.kLevelTrace,"About to do a get for ID:0x"+Convert.ToString(ScanObject.Property.ID,16));
				    result=ScanDevice.GetProperty(ScanObject);
			    }
			    else{
                    Debug.MSG(Debug.kLevelTrace, "About to do a set for ID:0x" + Convert.ToString(ScanObject.Property.ID,16));
				    result=ScanDevice.SetProperty(ScanObject);
			    }
		     }
		    _retries++;
		    if(SktScanErrors.SKTSUCCESS(result)){
			    _status=statusNotCompleted;
		    }
		    return result;
	    }

    }
    
    public class SymbologyInfo
    {
        public String Name { get; set; }
        public int Status { get; set; }
        public int ID { get; set; }
        public override string ToString()
        {
            return Name;
        }
    }

    public class DeviceInfo
    {
        public interface Notification
        {
            void OnNotify(DeviceInfo device);
        }

        private readonly ISktScanDevice _device;
        private String _name;
        private String _bdAddress;
        private readonly long _type;
        private String _version;
        private String _batteryLevel;
        private readonly int _decodeValue;
        private readonly bool _rumble;
        private String _suffix;
        private readonly SymbologyInfo[] _symbologyInfo;
        private Notification _notification;

        public String Name
        {
            get { return _name; }
            set { _name = value; }
        }
        
        public String BdAddress
        {
            get { return _bdAddress; }
            set { _bdAddress = value; Notify(); }
        }

        public ISktScanDevice SktScanDevice
        {
            get { return _device;}
        }

        public String Type
        {
            get 
            {
                String type;
                // Note this can't be a switch as the kSktScanDeviceTypesScanner's are actually functions
                if (_type == SktScanDeviceType.kSktScanDeviceTypeScanner7)
                    type = "CHS Scanner";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScanner7x)
                    type = "CHS 7X Scanner";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScanner7xi)
                    type = "CHS 7Xi/Qi Scanner";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScanner9)
                    type = "CRS Scanner";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScanner8ci)
                    type = "SocketScan S800 Scanner";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScanner8qi)
                    type = "SocketScan S850 Scanner";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScannerD750)
                    type = "DuraScan D750";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScannerD730)
                    type = "DuraScan D730";
                else if (_type == SktScanDeviceType.kSktScanDeviceTypeScannerD700)
                    type = "DuraScan D700";
                else
                    type = "Unknown scanner type!";
                return type;
            }
        }

        public String Version
        {
            get { return _version; }
            set { _version = value; Notify(); }
        }

        public String BatteryLevel
        {
            get { return _batteryLevel; }
            set { _batteryLevel = value; Notify(); }
        }

        public int DecodeValue
        {
            get { return _decodeValue; }
        }

        public bool Rumble
        {
            get { return _rumble; }
        }

        public String Suffix
        {
            get { return _suffix; }
            set { _suffix = value; Notify(); }
        }

        public int SymbologyIndex { get; set; }

        public int GetSymbologyStatus(int symbologyId)
        {
            return _symbologyInfo[symbologyId].Status;
        }

        public void SetSymbologyStatus(int symbologyId,int status)
        {
            _symbologyInfo[symbologyId].Status=status;
        }

        public String GetSymbologyName(int symbologyId)
        {
            return _symbologyInfo[symbologyId].Name;
        }

        public void SetSymbologyName(int symbologyId,String name)
        {
            _symbologyInfo[symbologyId].Name=name;
            Notify();
        }

        public DeviceInfo(String name, ISktScanDevice device, long type)
        {
            _device = device;
            _name = name;
            _bdAddress = "Not available";
            _type = type;
            _version = "Unknown";
            _batteryLevel = "Unknown";
            _decodeValue = 0;
            _rumble = true;
            _suffix = "\n";
            _notification = null;
            _symbologyInfo=new SymbologyInfo[ISktScanSymbology.id.kSktScanSymbologyLastSymbologyID];
            for (int i = 0; i < _symbologyInfo.Length; i++)
            {
                _symbologyInfo[i] = new SymbologyInfo();
            }
        }

        public void SetNotification(Notification notification)
        {
            _notification = notification;
        }

        private void Notify()
        {
            if (_notification != null)
                _notification.OnNotify(this);
        }

        public override string ToString()
        {
            return _name;
        }
    }

    /**
     * this class provides a set of common functions to retrieve
     * or configure a scanner or ScanAPI and to receive decoded
     * data from a scanner.<p>
     * This helper manages a commands list so the application
     * can send multiple command in a row, the helper will send
     * them one at a time. Each command has an optional callback 
     * function that will be called each time a command complete.
     * By example, to get a device friendly name, use the 
     * PostGetFriendlyName method and pass a callback function in 
     * which you can update the UI with the newly fetched friendly 
     * name. This operation will be completely asynchronous.<p>
     * ScanAPI Helper manages a list of device information. Most of 
     * the time only one device is connected to the host. This list
     * could be configured to have always one item, that will be a 
     * "No device connected" item in the case where there is no device
     * connected, or simply a device name when there is one device
     * connected. Use isDeviceConnected method to know if there is at
     * least one device connected to the host.<br> 
     * Common usage scenario of ScanAPIHelper:<br>
     * <li> create an instance of ScanApiHelper: _scanApi=new ScanApiHelper();
     * <li> [optional] if a UI device list is used a no device connected 
     * string can be specified:_scanApi.setNoDeviceText(getString(R.string.no_device_connected));
     * <li> register for notification: _scanApi.setNotification(_scanApiNotification);
     * <li> derive from ScanApiHelperNotification to handle the notifications coming
     * from ScanAPI including "Device Arrival", "Device Removal", "Decoded Data" etc...
     * <li> open ScanAPI to start using it:_scanApi.open();
     * <li> check the ScanAPI initialization result in the notifications: 
     * _scanApiNotification.onScanApiInitializeComplete(long result){}
     * <li> monitor a scanner connection by using the notifications:
     * _scanApiNotification.onDeviceArrival(long result,DeviceInfo newDevice){}
     * _scanApiNotification.onDeviceRemoval(DeviceInfo deviceRemoved){}
     * <li> retrieve the decoded data from a scanner
     * _scanApiNotification.onDecodedData(DeviceInfo device,ISktScanDecodedData decodedData){}
     * <li> once the application is done using ScanAPI, close it using:
     * _scanApi.close();
     * @author ericg
     *
     */
    class ScanApiHelper
    {
        /**
         * notification coming from ScanApiHelper the application
         * can override for its own purpose
         * @author ericg
         *
         */
        public interface ScanApiHelperNotification
        {
            /**
             * called each time a device connects to the host
             * @param result contains the result of the connection
             * @param newDevice contains the device information
             */
            void OnDeviceArrival(long result, DeviceInfo newDevice);

            /**
             * called each time a device disconnect from the host
             * @param deviceRemoved contains the device information
             */
            void OnDeviceRemoval(DeviceInfo deviceRemoved);

            /**
             * called each time ScanAPI is reporting an error
             * @param result contains the error code
             */
            void OnError(long result, string errMsg);

            /**
             * called each time ScanAPI receives decoded data from scanner
             * @param deviceInfo contains the device information from which
             * the data has been decoded
             * @param decodedData contains the decoded data information
             */
            void OnDecodedData(DeviceInfo device, ISktScanDecodedData decodedData);

            /**
             * called when ScanAPI initialization has been completed
             * @param result contains the initialization result
             */
            void OnScanApiInitializeComplete(long result);

            /**
             * called when ScanAPI has been terminated. This will be
             * the last message received from ScanAPI
             */
            void OnScanApiTerminated();

            /**
             * called when an error occurs during the retrieval
             * of a ScanObject from ScanAPI.
             * @param result contains the retrieval error code
             */
            void OnErrorRetrievingScanObject(long result);
        }

        public const int MAX_RETRIES = 5;
        private readonly List<CommandContext> _commandContexts;
        private readonly ISktScanApi _scanApi;
        private bool _scanApiOpen;
        private ScanApiHelperNotification _notification;
        private readonly List<DeviceInfo> _devicesList;
        private readonly DeviceInfo _noDeviceConnected;

        public ScanApiHelper()
        {
            _commandContexts = new List<CommandContext>();
            _scanApi = SktClassFactory.createScanApiInstance();
            _notification = null;
            _devicesList = new List<DeviceInfo>();
            _noDeviceConnected = new DeviceInfo("", null, SktScanDeviceType.kSktScanDeviceTypeNone);
            _scanApiOpen = false;
        }

        /**
         * register for notifications in order to receive notifications such as
         * "Device Arrival", "Device Removal", "Decoded Data"...etc...
         * @param notification
         */
        public void SetNotification(ScanApiHelperNotification notification)
        {
            _notification = notification;
        }

        /**
         * specifying a name to display when no device is connected
         * will add a no device connected item in the list with 
         * the name specified, otherwise if there is no device connected
         * the list will be empty.
         */
        public void SetNoDeviceText(String noDeviceText)
        {
            _noDeviceConnected.Name = noDeviceText;
        }

        /**
         * get the list of devices. If there is no device
         * connected and a text has been specified for
         * when there is no device then the list will
         * contain one item which is the no device in the 
         * list
         * @return
         */
        public List<DeviceInfo> GetDevicesList()
        {
            return _devicesList;
        }

        /**
         * check if there is a device connected
         * @return
         */
        public bool IsDeviceConnected()
        {
            return _devicesList.Count > 0;
        }

        /**
         * flag to know if ScanAPI is open
         * @return
         */
        public bool IsScanApiOpen()
        {
            return _scanApiOpen;
        }

        /**
         * open ScanAPI and initialize ScanAPI
         * The result of opening ScanAPI is returned in the callback
         * onScanApiInitializeComplete
         */
        public void Open()
        {
            _devicesList.Clear();
            if (_noDeviceConnected.Name.Length > 0)
                _devicesList.Add(_noDeviceConnected);

            Thread thread = new Thread(ScanApiInitializationThread) {Name = "ScanAPIHelperInit"};
            thread.Start();
        }

        /**
         * close ScanAPI. The callback onScanApiTerminated
         * is invoked as soon as ScanAPI is completely closed.
         * If a device is connected, a device removal will be received
         * during the process of closing ScanAPI.
         */
        public void Close()
        {
            if (_scanApiOpen)
                PostScanApiAbort(onSetScanApiAbort);
        }

        public long DoScanAPIReceive()
        {
            long result = SktScanErrors.ESKT_NOERROR;
            bool closeScanApi = false;
            if (_scanApiOpen)
            {
                ISktScanObject scanObj;
                result = _scanApi.WaitForScanObject(out scanObj, 1);
                if (SktScanErrors.SKTSUCCESS(result))
                {
                    if (result == SktScanErrors.ESKT_NOERROR)
                    {
                        closeScanApi = HandleScanObject(scanObj);
                        _scanApi.ReleaseScanObject(scanObj);
                    }
                    if (closeScanApi == false)
                        SendNextCommand();
                    else
                    {
                        _scanApi.Close();
                        _scanApiOpen = false;
                        if (_notification != null)
                            _notification.OnScanApiTerminated();
                    }
                }
                else
                {
                    _scanApi.Close();
                    _scanApiOpen = false;
                    if (_notification != null)
                    {
                        _notification.OnErrorRetrievingScanObject(result);
                        _notification.OnScanApiTerminated();
                    }
                }
            }
            return result;
        }

        /**
         * remove the pending commands for a specific device
         * or all the pending commands if null is passed as
         * iDevice parameter
         * @param iDevice reference to the device for which
         * the commands must be removed from the list or <b>null</b>
         * if all the commands must be removed.
         */

        public void RemoveCommands(DeviceInfo device)
	    {
		    ISktScanDevice iDevice=null;
		    if(device!=null)
			    iDevice=device.SktScanDevice;
		    // remove all the pending command for this device
		    lock(_commandContexts){
			    if(iDevice!=null)
                    _commandContexts.RemoveAll(scandevice => scandevice.ScanDevice == iDevice);
			    else
				    _commandContexts.Clear();
		    }
	    }

        /**
         * PostGetScanAPIVersion
         * retrieve the ScanAPI Version
         */
        public void PostGetScanAPIVersion(ICommandContextCallback callback)
        {
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdVersion;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;
            CommandContext command = new CommandContext(true, newScanObj, _scanApi, null, callback);
            AddCommand(command);
        }

        /**
         * PostSetConfirmationMode
         * Configures ScanAPI so that scanned data must be confirmed by this application before the
         * scanner can be triggered again.
         */
        public void PostSetConfirmationMode(char mode, ICommandContextCallback callback)
        {
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdDataConfirmationMode;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeByte;
            newScanObj.Property.Byte=(byte)mode;

            CommandContext command = new CommandContext(false, newScanObj, _scanApi, null, callback);
            AddCommand(command);
        }
        /**
         * PostSetLocalAcknowledgement
         * Set the scanner LocalAcknowledgement<p>
         * This is only required if the scanner Confirmation Mode is set to kSktScanDataConfirmationModeApp 
         * or kSktScanDataConfirmationModeScanAPI
         */
        public void PostSetLocalAcknowledgement(DeviceInfo deviceInfo, bool bLocalAcknowledgement, ICommandContextCallback callback)
        {

            ISktScanDevice device = deviceInfo.SktScanDevice;
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdLocalAcknowledgmentDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeByte;
            newScanObj.Property.Byte = bLocalAcknowledgement ? ScanAPI.ISktScanProperty.values.deviceDataAcknowledgment.kSktScanDeviceDataAcknowledgmentOn : ScanAPI.ISktScanProperty.values.deviceDataAcknowledgment.kSktScanDeviceDataAcknowledgmentOff;
            CommandContext command = new CommandContext(false, newScanObj, device, null, callback);
            AddCommand(command);
        }



        /**
         * PostSetDataConfirmation
         * acknowledge the decoded data<p>
         * This is only required if the scanner Confirmation Mode is set to kSktScanDataConfirmationModeApp
         */
        public void PostSetDataConfirmation(DeviceInfo deviceInfo, bool bGoodScan, ICommandContextCallback callback)
        {

            ISktScanDevice device = deviceInfo.SktScanDevice;
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdDataConfirmationDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeUlong;
            newScanObj.Property.Ulong=
                    bGoodScan ? 
                    SktScan.helper.SKTDATACONFIRMATION(
                            0,
                            ISktScanProperty.values.dataConfirmation.kSktScanDataConfirmationRumbleNone,
                            ISktScanProperty.values.dataConfirmation.kSktScanDataConfirmationBeepGood,
                            ISktScanProperty.values.dataConfirmation.kSktScanDataConfirmationLedGreen)
                            :
                    SktScan.helper.SKTDATACONFIRMATION(
                            0,
                            ISktScanProperty.values.dataConfirmation.kSktScanDataConfirmationRumbleNone,
                            ISktScanProperty.values.dataConfirmation.kSktScanDataConfirmationBeepBad,
                            ISktScanProperty.values.dataConfirmation.kSktScanDataConfirmationLedRed);


            CommandContext command = new CommandContext(false, newScanObj, device, null, callback);
            lock (_commandContexts)
            {
                if (_commandContexts.Count == 0)
                    AddCommand(command);
                else
                {
                    const int index = 0;
                    CommandContext pendingCommand = _commandContexts.ElementAt(index);
                    if (pendingCommand.Status == CommandContext.statusNotCompleted)
                        _commandContexts.Insert(index + 1,command);
                }
            }

            // try to see if the confirmation can be sent right away
            SendNextCommand();
        }

        /**
         * PostGetBtAddress
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Bluetooth address in the scanner.
         */
        public void PostGetBtAddress(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdBluetoothAddressDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);

        }

        /**
         * PostGetDeviceType
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Device Type of the scanner.
         */
        public void PostGetDeviceType(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdDeviceType;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);

        }

        /**
         * DoGetFirmware
         * Creates a TSktScanObject and initializes it to perform a request for the
         * firmware revision in the scanner.
         */
        public void PostGetFirmware(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdVersionDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);

        }
        /**
         * DoStartDecode
         */
        public void PostStartDecode(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdTriggerDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeByte;
            newScanObj.Property.Byte = (byte)ISktScanProperty.values.trigger.kSktScanTriggerStart;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(false, newScanObj, device, deviceInfo, callback);
            AddCommand(command);
        }
        /**
         * PostGetBattery
         * Creates a TSktScanObject and initializes it to perform a request for the
         * battery level in the scanner.
         */
        public void PostGetBattery(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdBatteryLevelDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);
        }

        /**
         * PostGetStandConfig
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Stand configuration of the scanner.
         */
        public void PostGetStandConfig(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdStandConfigDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);
        }

        /**
         * PostGetDecodeAction
         * 
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Decode Action in the scanner.
         * 
         */
        public void PostGetDecodeAction(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdLocalDecodeActionDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);

        }

        /**
         * DoGetCapabilitiesDevice
         * 
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Capabilities Device in the scanner.
         */
        public void PostGetCapabilitiesDevice(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdCapabilitiesDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeByte;
            newScanObj.Property.Byte=(byte)ISktScanProperty.values.capabilityGroup.kSktScanCapabilityLocalFunctions;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);

        }

        /**
         * DoGetPostambleDevice
         * 
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Postamble Device in the scanner.
         * 
         */
        public void PostGetPostambleDevice(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdPostambleDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;

            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);
        }

        /**
         * PostGetSymbologyInfo
         * 
         * Creates a TSktScanObject and initializes it to perform a request for the
         * Symbology Info in the scanner.
         * 
         */
        public void PostGetSymbologyInfo(DeviceInfo deviceInfo, int symbologyId, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdSymbologyDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeSymbology;
            newScanObj.Property.Symbology.Flags=ISktScanSymbology.flags.kSktScanSymbologyFlagStatus;
            newScanObj.Property.Symbology.ID=symbologyId;
            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);
        }

        /**
         * PostGetAllSymbologyInfo
         * 
         * Post a series of get Symbology info in order to retrieve all the
         * Symbology Info of the scanner.
         * The callback would be called each time a Get Symbology request has completed 
         */
        public void PostGetAllSymbologyInfo(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            for (int symbologyId = ISktScanSymbology.id.kSktScanSymbologyNotSpecified + 1;
                symbologyId < ISktScanSymbology.id.kSktScanSymbologyLastSymbologyID; symbologyId++)
            {
                ISktScanObject newScanObj = SktClassFactory.createScanObject();
                newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdSymbologyDevice;
                newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeSymbology;
                newScanObj.Property.Symbology.Flags=ISktScanSymbology.flags.kSktScanSymbologyFlagStatus;
                newScanObj.Property.Symbology.ID=symbologyId;
                // add the property and the device to the command context list
                // to send it as soon as it is possible
                CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
                AddCommand(command);
            }
        }

        /**
         * PostSetSymbologyInfo
         * Constructs a request object for setting the Symbology Info in the scanner
         * 
         */
        public void PostSetSymbologyInfo(DeviceInfo deviceInfo, int Symbology, bool Status, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdSymbologyDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeSymbology;
            newScanObj.Property.Symbology.Flags=ISktScanSymbology.flags.kSktScanSymbologyFlagStatus;
            newScanObj.Property.Symbology.ID=Symbology;
            newScanObj.Property.Symbology.Status = Status ? ISktScanSymbology.status.kSktScanSymbologyStatusEnable : ISktScanSymbology.status.kSktScanSymbologyStatusDisable;

            CommandContext command = new CommandContext(false, newScanObj, device, null, callback)
            {
                SymbologyId = Symbology // keep the symbology ID because the Set Complete won't return it
            };
            AddCommand(command);
        }


        /**
         * PostGetFriendlyName
         * 
         * Creates a TSktScanObject and initializes it to perform a request for the
         * friendly name in the scanner.
         * 
         */

        public void PostGetFriendlyName(DeviceInfo deviceInfo, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdFriendlyNameDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;
            // add the property and the device to the command context list
            // to send it as soon as it is possible
            CommandContext command = new CommandContext(true, newScanObj, device, deviceInfo, callback);
            AddCommand(command);
        }

        /** 
         * PostSetFriendlyName
         * Constructs a request object for setting the Friendly Name in the scanner
         * 
         */
        public void PostSetFriendlyName(DeviceInfo deviceInfo, String friendlyName, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdFriendlyNameDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeString;
            newScanObj.Property.String.Value=friendlyName;
            CommandContext command = new CommandContext(false, newScanObj, device, null, callback);
            AddCommand(command);
        }

        /** 
         * PostSetStandConfig
         * Constructs a request object for setting the Stand Config of the scanner
         * 
         */
        public void PostSetStandConfig(DeviceInfo deviceInfo, int standConfig, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdStandConfigDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeUlong;
            newScanObj.Property.Ulong = standConfig;
            CommandContext command = new CommandContext(false, newScanObj, device, null, callback);
            AddCommand(command);
        }
        /**
         * PostGetDeviceSpecific
         * 
         * Send a device specific Get operation
         * 
         * @param device
         * @param bArray - byte array for a specific scanner type
         */
        public void PostGetDeviceSpecific(DeviceInfo deviceInfo, Byte[] bArray, ICommandContextCallback callback)
        {
            ISktScanDevice device = deviceInfo.SktScanDevice;
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdDeviceSpecific;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeArray;
            newScanObj.Property.Array.Value = bArray;
            CommandContext command = new CommandContext(false, newScanObj, device, null, callback);
            AddCommand(command);
        }

        /**
         * PostSetDecodeAction
         * 
         * Configure the local decode action of the device
         * 
         * @param device
         * @param decodeVal
         */
        public void PostSetDecodeAction(DeviceInfo device, int decodeVal, ICommandContextCallback callback)
        {
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdLocalDecodeActionDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeByte;
            newScanObj.Property.Byte=(byte)(decodeVal & 0xffff);

            CommandContext command = new CommandContext(false, newScanObj, device.SktScanDevice, null, callback);
            AddCommand(command);
        }

        public void PostSetSoundConfig(DeviceInfo device, Byte[] bArray, ICommandContextCallback callback)
        {
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdSoundConfigDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeArray;
            newScanObj.Property.Array.Value = bArray;

            CommandContext command = new CommandContext(false, newScanObj, device.SktScanDevice, null, callback);
            AddCommand(command);
           
        }
        /**
         * PostSetPostamble
         * 
         * Configure the postamble of the device
         * @param device
         * @param suffix
         */
        public void PostSetPostamble(DeviceInfo device, String suffix, ICommandContextCallback callback)
        {
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdPostambleDevice;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeString;
            newScanObj.Property.String.Value=suffix;

            CommandContext command = new CommandContext(false, newScanObj, device.SktScanDevice, null, callback);
            AddCommand(command);
        }
        /**
         * PostSetTimersDevice
         * 
         * Configure the timers of the device
         * @param device
         * @param suffix
         */
        public void PostSetTimersDevice(DeviceInfo device, Byte[] bArray, ICommandContextCallback callback)
        {
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdTimersDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeArray;
            newScanObj.Property.Array.Value = bArray;
            newScanObj.Property.Array.Size = bArray.Length;

            CommandContext command = new CommandContext(false, newScanObj, device.SktScanDevice, null, callback);
            AddCommand(command);
        }
        /**
         * PostGetDataStore
         * 
         * Configure the timers of the device
         * @param device
         * @param suffix
         */
        public void PostGetDataStore(DeviceInfo device, byte index, ICommandContextCallback callback)
        {
            byte[] bArray = {0, 0};

            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID = ISktScanProperty.propId.kSktScanPropIdDataStoreDevice;
            newScanObj.Property.Type = ISktScanProperty.types.kSktScanPropTypeArray;
            newScanObj.Property.Array.Value = bArray;
            newScanObj.Property.Array.Size = bArray.Length;

            CommandContext command = new CommandContext(true, newScanObj, device.SktScanDevice, device, callback);
            AddCommand(command);
        }

        /**
         * PostScanApiAbort
         * 
         * Request ScanAPI to shutdown. If there is some devices connected
         * we will receive Remove event for each of them, and once all the
         * outstanding devices are closed, then ScanAPI will send a 
         * Terminate event upon which we can close this application.
         * If the ScanAPI Abort command failed, then the callback will
         * close ScanAPI
         */
        public void PostScanApiAbort(ICommandContextCallback callback)
        {
            // create and initialize the property to send to the device
            ISktScanObject newScanObj = SktClassFactory.createScanObject();
            newScanObj.Property.ID=ISktScanProperty.propId.kSktScanPropIdAbort;
            newScanObj.Property.Type=ISktScanProperty.types.kSktScanPropTypeNone;

            CommandContext command = new CommandContext(false, newScanObj, _scanApi, null, callback);
            AddCommand(command);
        }

        private void AddCommand(CommandContext newCommand)
        {
		    lock(_commandContexts)
		    {
			    if(newCommand.ScanObject.Property.ID==
				    ISktScanProperty.propId.kSktScanPropIdAbort){
				    Debug.MSG(Debug.kLevelTrace,"About to Add a ScanAPI Abort command so remove all previous commands");
				    _commandContexts.Clear();
			    }
			    _commandContexts.Add(newCommand);
			    Debug.MSG(Debug.kLevelTrace,"Add a new command to send");
		    }
        }



        /**
         * HandleScanObject
         * This method is called each time this application receives a
         * ScanObject from ScanAPI.
         * It returns true is the caller can safely close ScanAPI and
         * terminate its ScanAPI consumer.
         */
        private bool HandleScanObject(ISktScanObject scanObject)
        {
            bool closeScanApi = false;
            switch (scanObject.Msg.ID)
            {
                case ISktScanMsg.kSktScanMsgIdDeviceArrival:
                    HandleDeviceArrival(scanObject);
                    break;
                case ISktScanMsg.kSktScanMsgIdDeviceRemoval:
                    HandleDeviceRemoval(scanObject);
                    break;
                case ISktScanMsg.kSktScanMsgGetComplete:
                case ISktScanMsg.kSktScanMsgSetComplete:
                    DoGetOrSetComplete(scanObject);
                    break;
                case ISktScanMsg.kSktScanMsgIdTerminate:
                    Debug.MSG(Debug.kLevelTrace, "Receive a Terminate event, ask to close ScanAPI");
                    closeScanApi = true;
                    break;
                case ISktScanMsg.kSktScanMsgEvent:
                    HandleEvent(scanObject);
                    break;
            }
            return closeScanApi;
        }

        /**
         * HandleDeviceArrival
         * This method is called each time a device connect to the host.
         * 
         * We create a device info object to hold all the necessary
         * information about this device, including its interface
         * which is used as handle
         */
        private void HandleDeviceArrival(ISktScanObject scanObject)
        {
		    String friendlyName=scanObject.Msg.DeviceName;
		    String deviceGuid=scanObject.Msg.DeviceGuid;
		    long type=scanObject.Msg.DeviceType;
		    ISktScanDevice device=SktClassFactory.createDeviceInstance(_scanApi);
		    DeviceInfo newDevice=null;
		    long result=device.Open(deviceGuid);
		    if(SktScanErrors.SKTSUCCESS(result))
		    {
			    // add the new device into the list
			    newDevice=new DeviceInfo(friendlyName,device,type);
			    lock(_devicesList){
				    _devicesList.Add(newDevice);
				    _devicesList.Remove(_noDeviceConnected);
			    }
		    }
		    if(_notification!=null)
			    _notification.OnDeviceArrival(result,newDevice);
        }

        /**
         * HandleDeviceRemoval
         * This method is called each time a device is disconnected from the host.
         * Usually this will be a good opportunity to close the device
         */
        private void HandleDeviceRemoval(ISktScanObject scanObject)
        {
		    ISktScanDevice iDevice=scanObject.Msg.DeviceInterface;
		    DeviceInfo deviceFound=null;
		    lock(_devicesList){
			    foreach (DeviceInfo device in _devicesList)
			    {
				    if(device.SktScanDevice==iDevice)
				    {
					    deviceFound=device;
					    break;
				    }
			    }
    			
			    // let's notify whatever UI we might have
			    if(deviceFound!=null)
			    {
	        	    RemoveCommands(deviceFound);
	        	    _devicesList.Remove(deviceFound);
	        	    if(_devicesList.Count==0){
	        		    if(_noDeviceConnected.Name.Length>0)
	        			    _devicesList.Add(_noDeviceConnected);
	        	    }
	        	    if(_notification!=null)
	        		    _notification.OnDeviceRemoval(deviceFound);
			    }
		    }
		    iDevice.Close();
        }

        /**
         * HandleEvent
         * 
         * This method handles asynchronous events coming from ScanAPI
         * including decoded data
         */
        private void HandleEvent(ISktScanObject scanObject)
        {
		    ISktScanEvent Event=scanObject.Msg.Event;
		    ISktScanDevice iDevice=scanObject.Msg.DeviceInterface;
		    switch(Event.EventID)
		    {
		    case ISktScanEvent.id.kSktScanEventError:
			    if(_notification!=null){
			        _notification.OnError(scanObject.Msg.Result,
			            Event.DataType == ISktScanEvent.types.kSktScanEventDataTypeString ? Event.DataString.Value : null);
			    }
			    break;
		    case ISktScanEvent.id.kSktScanEventDecodedData:
                ISktScanDecodedData decodedData = Event.DataDecodedData;
			    DeviceInfo deviceInfo=GetDeviceInfo(iDevice);
			    if(_notification!=null){
				    _notification.OnDecodedData(deviceInfo,decodedData);
			    }
			    break;
		    case ISktScanEvent.id.kSktScanEventPower:
			    break;
		    case ISktScanEvent.id.kSktScanEventButtons:
			    break;
		    }
        }

        /**
         * DoGetOrSetComplete
         * "Get Complete" events arrive asynchonously via code in the timer handler of the Scanner List dialog. Even
         * though they may arrive asynchonously, they only arrive as the result of a successful corresponding "Get"
         * request.
         * 
         * This function examines the get complete event given in the pScanObj arg, and dispatches it to the correct
         * handler depending on the Property ID it contains.
         * 
         * Each property handler must return ESKT_NOERROR if it has successfully performed its processing.
         */
        private long DoGetOrSetComplete(ISktScanObject scanObj)
        {
		    long result=SktScanErrors.ESKT_NOERROR;
		    bool remove=true;
		    bool doCallback=true;
		    if (scanObj != null)
		    {
			    result=scanObj.Msg.Result;
			    CommandContext command=(CommandContext)scanObj.Property.Context;
			    Debug.MSG(Debug.kLevelTrace,"Complete event received for Context:"+command+"\n");
			    if(command!=null){
				    if(!SktScanErrors.SKTSUCCESS(result)&&
                        (scanObj.Property.ID!=ISktScanProperty.propId.kSktScanPropIdSetPowerOffDevice)){
					    if(command.Retries>=MAX_RETRIES){
						    remove=true;
					    }
					    else{
						    remove=false;// don't remove the command for a retry
						    doCallback=false;// don't call the callback for a silent retry
						    result=SktScanErrors.ESKT_NOERROR;
					    }
				    }
    				
				    if(doCallback)
					    command.DoCallback(result,scanObj);
    				
				    if(remove)
				    {
					    lock(_commandContexts){
						    Debug.MSG(Debug.kLevelTrace,"Remove command from the list\n");
						    _commandContexts.Remove(command);
					    }
				    }
				    else
				    {
					    command.Status=CommandContext.statusReady;
				    }
			    }
			    if(SktScanErrors.SKTSUCCESS(result))
			    {
				    result=SendNextCommand();
			    }
		    }
		    return result;
        }

        /**
         * sendNextCommand
         * This method checks if there is a command ready to be
         * sent at the top of the list. 
         */
        private long SendNextCommand()
        {
		    long result=SktScanErrors.ESKT_NOERROR;
            CommandContext command = null;
            lock (_commandContexts)
            {
                if (_commandContexts.Count > 0)
                {
                    Debug.MSG(Debug.kLevelTrace, "There are some commands to send\n");
                    command = _commandContexts.First();
                    Debug.MSG(Debug.kLevelTrace, "And this one has status=" + command.Status + " for command: " +
                                                 command.ScanObject.Property.ID);
                }
            }
            if(command != null && command.Status==CommandContext.statusReady)
            {
			    result=command.DoGetOrSetProperty();
			    if(!SktScanErrors.SKTSUCCESS(result))
                {
                    lock (_commandContexts)
                    {
                        _commandContexts.Remove(command);
                    }
				    // case where the command is not supported by the device
				    // we can ignore it
				    if(result==SktScanErrors.ESKT_NOTSUPPORTED)
				    {
					    Debug.MSG(Debug.kLevelWarning,"Remove an unsupported command\n");
				    }
				    // case where the device handle is invalid (propably disconnected)
				    // we can ignore it
				    else if(result==SktScanErrors.ESKT_INVALIDHANDLE)
				    {
					    Debug.MSG(Debug.kLevelWarning,"Remove a command with an invalid handle\n");
				    }
			    }
		    }
	        return result;
        }

        /**
         * retrieve the deviceInfo object matching to its ISktScanDevice interface
         * @param iDevice ScanAPI device interface
         * @return a deviceInfo object if it finds a matching device interface null
         * otherwise
         */
        private DeviceInfo GetDeviceInfo(ISktScanDevice iDevice)
        {
            DeviceInfo deviceInfo = null;
            lock (_devicesList)
            {
                foreach (DeviceInfo device in _devicesList)
                {
                    if (device.SktScanDevice == iDevice)
                    {
                        deviceInfo = device;
                        break;
                    }
                }
            }
            return deviceInfo;
        }

        private void ScanApiInitializationThread()
        {
            ScanApiHelper thisApp = this;
            long result = thisApp._scanApi.Open(null);
            _scanApiOpen = true;
            if (thisApp._notification != null)
                thisApp._notification.OnScanApiInitializeComplete(result);
        }

        /// <summary>
        /// This method calls OnScanApiTerminated if the set ScanAPI abort
        /// failed. If the set ScanAPI abort, the onScanApiTerminated is called
        /// upon reception of the Terminate event from ScanAPI
        /// </summary>
        /// <param name="result"></param>
        /// <param name="scanObj"></param>
        public void onSetScanApiAbort(long result,ISktScanObject scanObj)
        {
            if (!SktScanErrors.SKTSUCCESS(result))
            {
                if (_notification!=null)
                    _notification.OnScanApiTerminated();
            }
        }

    }

}

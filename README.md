# SingleEntry for Windows
Sample code for ScanAPI SDK

## Prerequisite
This application requires ScanAPI SDK and a Bluetooth INBOUND port 
(COM3 preferably).

## Installation
ScanAPI is now available as a "private" NuGet.

Copy SocketMobile.ScanAPI.10.2.xxx.x.nupkg NuGet file to a location of your 
choice. 

In Visual Studio, launch the NuGet Package Manager and add a new source, with 
the location set to where the ScanAPI NuGet is located.

Clone this project and load it in Visual Studio. There is a NuGet restore 
function that will automatically install the ScanAPI NuGet in the project. If 
this function is not enabled, go to the NuGet Package Manager and select the 
source pointing to where ScanAPI NuGet is located, and proceed to its 
installation.

Compile and run the project.

If an error occurs it might be cause the Bluetooth INBOUND port is not 
configured correctly.

## ScanAPI Configuration
ScanAPI requires at least one Bluetooth INBOUND port in order to open without 
error.

This ScanAPI configuration can be set by using the kSktScanPropIdConfiguration 
with the string value set as follow: "SerialPorts=COM3:" by example.

To retrieve the current configuration, a Get kSktScanPropIdConfiguration 
property with a string value set to "SerialPorts" will return the COM port 
ScanAPI is actually trying to listen to.

This configuration is stored on the disk to a shared location like by example on
a Windows 7 host: \ProgramData\Socket Mobile\ScanAPI\ScanAPI.xml.

It is not recommended to modify this ScanAPI.xml file directly as its format can
change or this file can be replaced in different version. The best for modifying
this configuration is to use the ScanAPI API.

## ScanAPI Documentation
A more detailed documentation can be found on Socket Mobile developer portal at:
http://www.socketmobile.com/developers/welcome

## ScanAPI Usage
This SingleEntry sample app is using ScanApiHelper which simplifies the ScanAPI
integration in an app.

ScanApiHelper is actually a data member of the SingleEntry Form object. In the 
constructor of the Form, the ScanApiHelper instance is allocated.
The Form actually derives from the ScanApiHelperNotification interface in order
to implement the various notifications coming from ScanAPI.
Once ScanApiHelper instance is created the ScanApiHelper SetNotification is 
called with this Form reference as argument.

Upon the Form Load, ScanApiHelper is open and a ScanAPI consumer timer is set.
Each time this timer elapses, its handler calls ScanApiHelper DoScanAPIReceive 
in order to receive any notification from ScanAPI.

Then most of the implementation is done in the notification handlers. 
The first notification received is OnScanApiInitializeComplete. This indicates 
the first open has successfully initialized ScanAPI. The Bluetooth INBOUND port
has be open correctly and ScanAPI is listening to it.

If a scanner is connecting to this host, then the OnDeviceArrival notification 
is invoked.

If the scanner decodes successfully a barcode, the OnDecodedData notification is 
invoked with the decoded data as argument.

If the scanner disconnects from the host then the app receives the 
OnDeviceRemoval notification.

ScanApiHelper provides few PostSetxxx and PostGetxxx methods to send a request 
to get the friendly name, the Bluetooth address of the device, or to get and set 
the barcode symbologies by example.

Each of these PostSetxxx / PostGetxxx accepts a callback as argument that is 
invoked upon completion of the request.

The close process is a 2-step process: first call the ScanApiHelper close method
and then wait for the OnScanApiTerminated notification, which can then close the
form.









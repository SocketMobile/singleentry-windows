// Copyright 2015 Socket Mobile, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using ScanAPI;
using ScanApiHelper;


namespace SingleEntry
{
    public partial class SingleEntry : Form, ScanApiHelper.ScanApiHelper.ScanApiHelperNotification
    {
        private const string strAppName = "SingleEntry";
        private const int SCANAPI_TIMER_PERIOD = 100;		// milliseconds
        private readonly ScanApiHelper.ScanApiHelper _scanApiHelper;
        private DeviceInfo _device;
        // for the Scan Test window to receive the decoded data
        public delegate void DecodedDataOutputDelegate(string strDecodedData);
        public delegate void StandardTextOutputDelegate(string strStatus);

        public SingleEntry()
        {
            InitializeComponent();
            lblStatus.Text = "Initializing...";
            _scanApiHelper = new ScanApiHelper.ScanApiHelper();
            _scanApiHelper.SetNotification(this);
        }

        private void SingleEntry_Load(object sender, EventArgs e)
        {
            // Start ScanAPI Helper
            _scanApiHelper.Open();
            timerScanners.Interval = SCANAPI_TIMER_PERIOD;
            timerScanners.Start();
        }
	
        // if ScanAPI is fully initialized then we can
        // receive ScanObject from ScanAPI.
        private void timerScanners_Tick_1(object sender, EventArgs e)
        {
		    _scanApiHelper.DoScanAPIReceive();
        }

        // ScanAPI Helper provides a series of Callbacks
        // indicating some asynchronous events has occurred
        #region ScanApiHelperNotification Members

        // a scanner has connected to the host
        public void OnDeviceArrival(long result, ScanApiHelper.DeviceInfo newDevice)
        {
            if (SktScanErrors.SKTSUCCESS(result))
            {
                UpdateStatusText("New Scanner: " + newDevice.Name);
                _device = newDevice;
            }
            else
            {
                string strMsg = String.Format("Unable to open scanner, error = {0}.", result);
                MessageBox.Show(strMsg, strAppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // a scanner has disconnected from the host
        public void OnDeviceRemoval(ScanApiHelper.DeviceInfo deviceRemoved)
        {
            UpdateStatusText("Scanner Removed: " + deviceRemoved.Name);

            _device = null;
        }

        // a ScanAPI error occurs.
        public void OnError(long result, string errMsg)
        {
            MessageBox.Show("ScanAPI Error: " + Convert.ToString(result) + " [" + (errMsg ?? "") + "]",
                strAppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // some decoded data have been received
        public void OnDecodedData(ScanApiHelper.DeviceInfo device, ISktScanDecodedData decodedData)
        {
            UpdateDecodedDataText(decodedData.DataToUTF8String);
        }

        // ScanAPI is now initialized and fully functional
        // (ScanAPI has some internal testing that might take
        // few seconds to complete)
        public void OnScanApiInitializeComplete(long result)
        {            
                UpdateStatusText("SktScanAPI " + (SktScanErrors.SKTSUCCESS(result) ? "opened!" : "failed!"));
        }

        public void UpdateStatusText(string strStatus)
        {
            if (InvokeRequired)
                Invoke(new StandardTextOutputDelegate(UpdateStatusText), new object[] { strStatus });
            else
                lblStatus.Text = strStatus;
        }
        public void UpdateDecodedDataText(string strDecodedData)
        {
            if (InvokeRequired)
                Invoke(new DecodedDataOutputDelegate(UpdateDecodedDataText), new object[] { strDecodedData });
            else
                textScannedData.Text = strDecodedData;
        }
        // ScanAPI has now terminated, it is safe to
        // close the application now
        public void OnScanApiTerminated()
        {
            //timerScanner.Stop();
            Close();// we can now close this form
        }

        // the ScanAPI Helper encountered an error during
        // the retrieval of a ScanObject
        public void OnErrorRetrievingScanObject(long result)
        {
            MessageBox.Show("Unable to retrieve a ScanAPI ScanObject: " + Convert.ToString(result),
                strAppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion
    }
}

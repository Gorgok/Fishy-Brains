using System;
using System.IO.Ports;
using System.Threading;
using System.Collections;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.Net.NetworkInformation;
using JDI.NETMF.Net;
using JDI.NETMF.Web;
using JDI.NETMF.WIZnet;
using JDI.NETMF.Storage;
using JDI.NETMF.Shields;
using WCR.CONVERT;


namespace FishyBrains
{
    public class Program
    {
        #region Constants
        private const int constPostDataMaxLength = 2048;
        private const string constAppName = "Fishy Brains";
        private const string constTankStatusPageFileName = "TankStatus.htm";
        private const string constTankSettingsPageFileName = "TankSettings.htm";
        private const string constTankTimersPageFileName = "TankTimers.htm";
        private const string constDevicePageFileName = "Device.htm";
        private const string constSettingsPageFileName = "Settings.htm";
        private const string constRestartPageFileName = "Restart.htm";
        private const string constColorBalancePageFileName = "ColorBalance.htm";
        private const sbyte up = 1;
        private const sbyte down = -1;
        public static class DS18B20
        {
            public const byte SearchROM = 0xF0;
            public const byte ReadROM = 0x33;
            public const byte MatchROM = 0x55;
            public const byte SkipROM = 0xCC;
            public const byte AlarmSearch = 0xEC;
            public const byte StartTemperatureConversion = 0x44;
            public const byte ReadScratchPad = 0xBE;
            public const byte WriteScratchPad = 0x4E;
            public const byte CopySratchPad = 0x48;
            public const byte RecallEEPROM = 0xB8;
            public const byte ReadPowerSupply = 0xB4;
        }
        #endregion

        #region Static objects and variables
        private static DateTime lastRebootTime = DateTime.MinValue;
        private static bool rtcIsWorking = false;
        private static AppSettings appSettings = null;
        private static FEZConnect fezConnect = null;
        private static HTTPServer httpServer = null;
        private static DateTime rebootTime = DateTime.MaxValue;
        private static Object threadLockObject = new object();
        private static byte[] Probe1 = new byte[8] { 0x28, 0x95, 0x88, 0x60, 0x03, 0x00, 0x00, 0xC5 }; //Main Probe 0x28:0x95:0x88:0x60:0x03:0x00:0x00:0xC5:
        private static byte[] Probe2 = new byte[8] { 0x28, 0x76, 0xBC, 0x60, 0x03, 0x00, 0x00, 0x2E }; //Sump Probe 0x28:0x76:0xBC:0x60:0x03:0x00:0x00:0x2E:
        private static byte[] Probe3 = new byte[8] { 0x28, 0xFB, 0x6C, 0x60, 0x03, 0x00, 0x00, 0xD0 }; //Board Probe 0x28:0xFB:0x6C:0x60:0x03:0x00:0x00:0xD0:
        private static SerialPort UART1 = new SerialPort("COM1", 9600); //Lights
        private static SerialPort UART2 = new SerialPort("COM2", 9600); //Flow
        private static SerialPort UART3 = new SerialPort("COM3", 9600); //Main
        private static SerialPort UART4 = new SerialPort("COM4", 9600); //Sump
        private static PWM blue = new PWM((PWM.Pin)FEZ_Pin.PWM.Di8);
        private static PWM violet = new PWM((PWM.Pin)FEZ_Pin.PWM.Di9);
        private static PWM white = new PWM((PWM.Pin)FEZ_Pin.PWM.Di6);
        private static OneWire ow = new OneWire((Cpu.Pin)FEZ_Pin.Digital.Di5);
        private static PWM[] color = new PWM[3] { blue, violet, white };
        public static double highProbe1 = 1;
        public static double highProbe2 = 1;
        public static double lowProbe1 = 30;
        public static double lowProbe2 = 30;
        public static double highPH = 3;
        public static double lowPH = 10;
        public static double temp1 = 20;
        public static double temp2 = 20;
        public static double temp3 = 20;
        public static bool rTemp = true;
        public static bool rPH = false;
        public static bool rTimer = true;
        public static bool restartTimer = false;
        public static int minRamp = 0;
        public static bool mPump = true;
        public static bool sPump = true;
        public static bool fPump = true;
        public static int wNow = 0;
        public static int bNow = 0;
        public static int vNow = 0;
        #endregion

        #region Main Method

        public static void Main()
        {
            Debug.EnableGCMessages(false);

            Debug.Print("");
            Debug.Print("");
            Debug.Print(constAppName + " : Startup...");
            Debug.Print("Free mem : " + Debug.GC(false).ToString());

            // set system time
            SetSystemTime();
            Debug.GC(true);
            Debug.Print("Free mem : " + Debug.GC(false).ToString());

            // initialize and mount SD card
            MountSDCard();
            Debug.GC(true);
            Debug.Print("Free mem : " + Debug.GC(false).ToString());

            // load appSettings
            appSettings = new AppSettings();
            LoadAppSettings();
            Debug.GC(true);
            Debug.Print("Free mem : " + Debug.GC(false).ToString());

            // initialize fezConnect
            fezConnect = new FEZConnect();
            InitializeFezConnect();
            Debug.GC(true);
            Debug.Print("Free mem : " + Debug.GC(false).ToString());

            // initialize network
            if (fezConnect.DeviceStatus == FEZConnect.DevStatus.Ready)
            {
                InitializeNetwork();
                Debug.GC(true);
                Debug.Print("Free mem : " + Debug.GC(false).ToString());
            }

            // set clock from NTP server
            if (fezConnect.NetworkStatus == FEZConnect.NetStatus.Ready && appSettings.NTPEnabled)
            {
                SetClockFromNTPTime();
                Debug.GC(true);
                Debug.Print("Free mem : " + Debug.GC(false).ToString());
            }

            // start http server
            if (fezConnect.NetworkStatus == FEZConnect.NetStatus.Ready && appSettings.HTTPEnabled)
            {
                httpServer = new HTTPServer();
                httpServer.HttpStatusChanged += new HTTPServer.HttpStatusChangedHandler(httpServer_HttpStatusChanged);

                RegisterHTTPEventHandlers();
                Debug.GC(true);
                Debug.Print("Free mem : " + Debug.GC(false).ToString());

                StartHTTPServer();
                Debug.GC(true);
            }

            // run application
            //readOWbus();
            Thread Timer = new Thread(CheckTime);
            Timer.Start();
            Debug.GC(true);
            Thread Temps = new Thread(Temp);
            Temps.Start();
            Debug.GC(true);

            // this is the main program loop
            rebootTime = DateTime.MaxValue;
            while (true)
            {
                // check for reboot
                if (DateTime.Now >= rebootTime)
                {
                    rebootTime = DateTime.MaxValue; ;
                    RebootDevice();
                    Debug.GC(true);
                }

                // your application code goes here

                // sleep 1 sec
                Thread.Sleep(1000);
            }
        }

        #endregion

        #region Helper Methods
        private static void CheckTime()
        {
            DateTime midnight = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 0, 0, 0);
            bool bDoneUp = false;
            bool wDoneUp = false;
            bool wDoneDown = false;
            bool bDoneDown = false;
            bool rDoneUp = false;
            bool rDoneDown = false;
            while (true)
            {
                while (rTimer == true)
                {
                    long wStartTime = TimeSpan.TicksPerDay - (appSettings.wStartHint * TimeSpan.TicksPerHour) - (appSettings.wStartMint * TimeSpan.TicksPerMinute);
                    long bStartTime = TimeSpan.TicksPerDay - (appSettings.bStartHint * TimeSpan.TicksPerHour) - (appSettings.bStartMint * TimeSpan.TicksPerMinute);
                    long rStartTime = TimeSpan.TicksPerDay - (appSettings.rStartHint * TimeSpan.TicksPerHour) - (appSettings.rStartMint * TimeSpan.TicksPerMinute);
                    long wEndTime = TimeSpan.TicksPerDay - (appSettings.wEndHint * TimeSpan.TicksPerHour) - (appSettings.wEndMint * TimeSpan.TicksPerMinute);
                    long bEndTime = TimeSpan.TicksPerDay - (appSettings.bEndHint * TimeSpan.TicksPerHour) - (appSettings.bEndMint * TimeSpan.TicksPerMinute);
                    long rEndTime = TimeSpan.TicksPerDay - (appSettings.rEndHint * TimeSpan.TicksPerHour) - (appSettings.rEndMint * TimeSpan.TicksPerMinute);
                    long bDurr = appSettings.bRampint * TimeSpan.TicksPerMinute;
                    long wDurr = appSettings.wRampint * TimeSpan.TicksPerMinute;
                    restartTimer = false;
                    while (restartTimer == false)
                    {
                        DateTime currentTime = DateTime.Now;
                        long ticksToGo = midnight.Ticks - currentTime.Ticks;
                        for (; ticksToGo <= 0; ticksToGo = ticksToGo + TimeSpan.TicksPerDay)
                        {
                            midnight = midnight.AddDays(1);
                            bDoneUp = false;
                            wDoneUp = false;
                            rDoneUp = false;
                            wDoneDown = false;
                            bDoneDown = false;
                            rDoneDown = false;
                        }
                        if (rDoneDown == false && ticksToGo <= rEndTime)
                        {
                            Debug.Print(constAppName + " : " + DateTime.Now.ToString("T") + " Turning off red lights.");
                            if (UART1.IsOpen == true)
                            {
                                UART1.Close();
                            }
                            rDoneDown = true;
                        }
                        Thread.Sleep(5000);
                        if (bDoneUp == false && ticksToGo <= bStartTime && ticksToGo > bEndTime)
                        {
                            int bRamp = System.Math.Max(minRamp, (int)((ticksToGo - (bStartTime - bDurr)) / TimeSpan.TicksPerMinute));
                            Debug.Print(constAppName + " : " + DateTime.Now.ToString("T") + " Ramping up blue. " + bRamp.ToString() + " minute ramp duration.");
                            Thread RampBlue = new Thread(() => Ramp(0, appSettings.bMax1int, up, bRamp, blue));
                            Thread RampViolet = new Thread(() => Ramp(0, appSettings.bMax2int, up, bRamp, violet));
                            RampBlue.Start();
                            RampViolet.Start();
                            bDoneUp = true;
                        }
                        if (wDoneUp == false && ticksToGo <= wStartTime && ticksToGo > wEndTime)
                        {
                            int wRamp = System.Math.Max(minRamp, (int)((ticksToGo - (wStartTime - wDurr)) / TimeSpan.TicksPerMinute));
                            Debug.Print(constAppName + " : " + DateTime.Now.ToString("T") + " Ramping up white. " + wRamp.ToString() + " minute ramp duration.");
                            Thread RampWhite = new Thread(() => Ramp(0, appSettings.wMaxint, up, wRamp, white));
                            RampWhite.Start();
                            wDoneUp = true;
                        }
                        if (wDoneUp == true && wDoneDown == false && ticksToGo <= wEndTime + wDurr)
                        {
                            int wRamp = System.Math.Max(minRamp, System.Math.Min(appSettings.wRampint, (int)((ticksToGo - wEndTime) / TimeSpan.TicksPerMinute)));
                            Debug.Print(constAppName + " : " + DateTime.Now.ToString("T") + " Ramping down white. " + wRamp.ToString() + " minute ramp duration.");
                            Thread RampWhite = new Thread(() => Ramp(appSettings.wMaxint, 0, down, wRamp, white));
                            RampWhite.Start();
                            wDoneDown = true;
                        }
                        if (bDoneUp == true && bDoneDown == false && ticksToGo <= bEndTime + bDurr)
                        {
                            int bRamp = System.Math.Max(minRamp, System.Math.Min(appSettings.bRampint, (int)((ticksToGo - bEndTime) / TimeSpan.TicksPerMinute)));
                            Debug.Print(constAppName + " : " + DateTime.Now.ToString("T") + " Ramping down blue. " + bRamp.ToString() + " minute ramp duration.");
                            Thread RampBlue = new Thread(() => Ramp(appSettings.bMax1int, 0, down, bRamp, blue));
                            Thread RampViolet = new Thread(() => Ramp(appSettings.bMax2int, 0, down, bRamp, violet));
                            RampBlue.Start();
                            RampViolet.Start();
                            bDoneDown = true;
                        }
                        if (rDoneUp == false && ticksToGo <= rStartTime)
                        {
                            Debug.Print(constAppName + " : " + DateTime.Now.ToString("T") + " Turning on red lights.");
                            if (UART1.IsOpen == false)
                            {
                                UART1.Open();
                            }
                            rDoneUp = true;
                        }
                        Thread.Sleep(10000);
                    }
                }
                Thread.Sleep(10000);
            }
        }
        private static void Ramp(int start, int end, sbyte dir, int dur, PWM color)
        {
            sbyte diff = 0;
            int durStep = 0;
            if (dur > 0)
            {
                diff = (sbyte)System.Math.Abs(start - end);
                if (diff > 0)
                {
                    durStep = (int)((dur * TimeSpan.TicksPerMinute) / (diff * TimeSpan.TicksPerMillisecond));
                }
            }
            for (byte x = (byte)start; start != end; start = (byte)(start + dir))
            {
                x = (byte)(x + dir);
                Thread.Sleep(durStep);
                color.Set(5000, x);
            }
        }
        private static void Feed()
        {
            if (UART2.IsOpen == true)
            {
                return;
            }
            else
            {
                UART2.Open();
                fPump = false;
                int min = 5;
                Thread.Sleep(min * 60000);
                UART2.Close();
                fPump = true;
            }
        }
        private static void Temp()
        {
            while (true)
            {
                while (rTemp == true)
                {
                    temp1 = getTemp(Probe1);
                    Thread.Sleep(500);
                    if (highProbe1 < temp1)
                    {
                        highProbe1 = temp1;
                    }
                    if (lowProbe1 > temp1)
                    {
                        lowProbe1 = temp1;
                    }
                    Thread.Sleep(2000);
                    temp2 = getTemp(Probe2);
                    Thread.Sleep(500);
                    if (highProbe2 < temp2)
                    {
                        highProbe2 = temp2;
                    }
                    if (lowProbe2 > temp2)
                    {
                        lowProbe2 = temp2;
                    }
                    Thread.Sleep(2000);
                    temp3 = getTemp(Probe3);
                    Thread.Sleep(10000);
                }
                Thread.Sleep(15000);
            }
        }
        private static double getTemp(byte[] addr)
        {
            ushort temp;
            double last = 0;
            double tempc = 1; // temperature in degrees Celsius

            if (ow.Reset())
            {
                ow.WriteByte(DS18B20.MatchROM); // Match ROM
                ow.Write(addr, 0, 8);
                ow.WriteByte(DS18B20.StartTemperatureConversion); // Start temperature conversion

                while (ow.ReadByte() == 0)
                {
                    ow.Reset();
                    ow.WriteByte(DS18B20.MatchROM); // Match ROM
                    ow.Write(addr, 0, 8);
                    ow.WriteByte(DS18B20.ReadScratchPad); // Read Scratchpad

                    temp = ow.ReadByte();                   // LSB
                    temp |= (ushort)(ow.ReadByte() << 8);   // MSB

                    tempc = (temp * .0625);
                    if (tempc != last)
                    {
                        //Debug.Print("Temp: " + tempc + "C");
                        last = tempc;
                    }
                }
            }
            else
            {
                Debug.Print("Device is not detected.");
            }
            return tempc;
        }
        private static void readOWbus()
        {
            int i;
            int OW_number;
            byte[] OW_Address = new byte[8];
            c_Convert Convert = new c_Convert();
            if (ow.Reset())
            {
                OW_number = 0;
                while (ow.Search_GetNextDevice(OW_Address))
                {
                    for (i = 0; i < 8; i++) Debug.Print("0x" + (Convert.TwoByte_ToHex((byte)OW_Address[i])) + ":");
                    if (OW_Address[0] == 0x10) { Debug.Print("Device is a DS18S20 family device.\n"); }
                    else if (OW_Address[0] == 0x28) { Debug.Print("Device is a DS18B20 family device.\n"); }
                    else { Debug.Print("Device family is not recognized: " + OW_Address[0].ToString() + " decimal address"); }
                    OW_number++;
                }
                Debug.Print("Found: " + OW_number + " devices");
            }
        }
        private static void SetSystemTime()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Setting system time...");
            try
            {
                lastRebootTime = RealTimeClock.GetTime();
            }
            catch { }
            if (lastRebootTime.Year > 2010)
            {
                rtcIsWorking = true;
                Utility.SetLocalTime(RealTimeClock.GetTime());
                Debug.Print(constAppName + " : System time set : " + RealTimeClock.GetTime().ToString());
            }
            else
            {
                Debug.Print(constAppName + " : Error setting system time (RealTimeClock battery may be low).");
            }
        }
        private static void MountSDCard()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Mounting the SD card...");
            SDCard.Initialize();
            if (SDCard.MountSD() == true)
            {
                Debug.Print(constAppName + " : SD card mounted.");
            }
            else
            {
                Debug.Print(constAppName + " : Error mounting SD card: " + SDCard.LastErrorMsg);
            }
        }
        private static void LoadAppSettings()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Loading application settings...");
            bool restoreDefSettings = false;
            InputPort button = new InputPort((Cpu.Pin)FEZ_Pin.Digital.Di11, false, Port.ResistorMode.PullUp);
            if (button.Read() == false)
            {
                Debug.Print(constAppName + " : Reset settings button is pressed.");
                restoreDefSettings = true;
            }
            else if (appSettings.LoadFromFlash() == false)
            {
                Debug.Print(constAppName + " : Error loading settings: " + appSettings.LastErrorMsg);
                restoreDefSettings = true;
            }

            if (restoreDefSettings == true)
            {
                Debug.Print(constAppName + " : Restoring default settings...");

                appSettings = GetDefaultSettings();
                if (appSettings.SaveToFlash() == true)
                {
                    Debug.Print(constAppName + " : Application settings saved.");
                }
                else
                {
                    Debug.Print(constAppName + " : Error saving settings: " + appSettings.LastErrorMsg);
                }
            }
            else
            {
                Debug.Print(constAppName + " : Application settings loaded.");
            }
            button.Dispose();
            button = null;
        }
        private static void InitializeFezConnect()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Initializing FEZ Connect...");
            fezConnect.InitializeDevice(SPI.SPI_module.SPI1, (Cpu.Pin)FEZ_Pin.Digital.Di10, (Cpu.Pin)FEZ_Pin.Digital.Di7, appSettings.DHCPEnabled);
            if (fezConnect.DeviceStatus == FEZConnect.DevStatus.Error)
            {
                Debug.Print(constAppName + " : Error : " + fezConnect.LastErrorMsg);
            }
        }
        private static void InitializeNetwork()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Initializing network...");
            fezConnect.InitializeNetwork(appSettings);
            if (fezConnect.DeviceStatus == FEZConnect.DevStatus.Error)
            {
                Debug.Print(constAppName + " : Error : " + fezConnect.LastErrorMsg);
            }
            else
            {
                Debug.Print(constAppName + " : Network ready.");
                Debug.Print("  IP Address: " + NetUtils.IPBytesToString(NetworkInterface.IPAddress));
                Debug.Print("  Subnet Mask: " + NetUtils.IPBytesToString(NetworkInterface.SubnetMask));
                Debug.Print("  Default Getway: " + NetUtils.IPBytesToString(NetworkInterface.GatewayAddress));
                Debug.Print("  DNS Server: " + NetUtils.IPBytesToString(NetworkInterface.DnsServer));
            }
        }
        private static void SetClockFromNTPTime()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Requesting NTP date-time...");
            using (NTPClient ntpClient = new NTPClient())
            {
                DateTime ntpDateTime = ntpClient.GetNTPTime(appSettings.NTPServer, appSettings.NTPOffsetInt);
                if (ntpDateTime != DateTime.MinValue)
                {
                    Utility.SetLocalTime(ntpDateTime);
                    RealTimeClock.SetTime(ntpDateTime);
                    Debug.Print(constAppName + " : NTP date-time set : " + ntpDateTime.ToString());
                }
                else
                {
                    Debug.Print(constAppName + " : Error : " + ntpClient.LastErrorMsg);
                }
            }
        }
        private static void RegisterHTTPEventHandlers()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Registering Http event handlers...");
            if (httpServer != null)
            {
                httpServer.RegisterRequestHandler("/", new HTTPServer.HttpRequestHandler(DefaultPageHandler));
                httpServer.RegisterRequestHandler("/" + constTankStatusPageFileName, new HTTPServer.HttpRequestHandler(TankStatusPageHandler));
                httpServer.RegisterRequestHandler("/" + constTankSettingsPageFileName, new HTTPServer.HttpRequestHandler(TankSettingsPageHandler));
                httpServer.RegisterRequestHandler("/" + constTankTimersPageFileName, new HTTPServer.HttpRequestHandler(TankTimersPageHandler));
                httpServer.RegisterRequestHandler("/" + constDevicePageFileName, new HTTPServer.HttpRequestHandler(DevicePageHandler));
                httpServer.RegisterRequestHandler("/" + constSettingsPageFileName, new HTTPServer.HttpRequestHandler(SettingsPageHandler));
                httpServer.RegisterRequestHandler("/" + constRestartPageFileName, new HTTPServer.HttpRequestHandler(RestartPageHandler));
                httpServer.RegisterRequestHandler("/" + constColorBalancePageFileName, new HTTPServer.HttpRequestHandler(ColorBalancePageHandler));
            }
        }
        private static void StartHTTPServer()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Starting HTTP server...");
            httpServer.InitializeServer(appSettings.HostName, appSettings.HTTPPrefix, appSettings.HTTPPortInt, constPostDataMaxLength);
            httpServer.StartServer();
            DateTime timeoutAt = DateTime.Now.AddSeconds(5);
            while (DateTime.Now < timeoutAt)
            {
                if (httpServer.Status == HTTPServer.HTTPStatus.Listening)
                {
                    break;
                }
                else if (httpServer.Status == HTTPServer.HTTPStatus.Error)
                {
                    break;
                }
                Thread.Sleep(100);
            }
        }
        private static void StopHTTPServer()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Stopping Http server...");
            httpServer.StopServer();
            DateTime timeoutAt = DateTime.Now.AddSeconds(10);
            while (DateTime.Now < timeoutAt)
            {
                if (httpServer.Status == HTTPServer.HTTPStatus.Stopped)
                {
                    break;
                }
                else if (httpServer.Status == HTTPServer.HTTPStatus.Error)
                {
                    break;
                }
                Thread.Sleep(100);
            }
        }
        private static void RebootDevice()
        {
            Debug.Print("");
            Debug.Print(constAppName + " : Rebooting...");
            Thread.Sleep(100);

            PowerState.RebootDevice(true);
        }
        private static AppSettings GetDefaultSettings()
        {
            AppSettings settings = new AppSettings();

            // network appSettings
            settings.HostName = "FishyBrains";
            settings.MACAddress = "00-EF-18-84-E8-BE";
            settings.DHCPEnabled = false;
            settings.IPAddress = "192.168.100.15";
            settings.IPMask = "255.255.255.0";
            settings.IPGateway = "192.168.100.2";
            settings.DNSAddress = "75.75.75.75";
            settings.NTPEnabled = true;
            settings.NTPServer = "us.pool.ntp.org";
            settings.NTPOffset = "-300";
            settings.HTTPEnabled = true;
            settings.HTTPPrefix = "http";
            settings.HTTPPort = "80";

            // appSettings
            settings.Password = "test";
            settings.wStartH = "11";
            settings.wStartM = "00";
            settings.wEndH = "21";
            settings.wEndM = "00";
            settings.wMax = "80";
            settings.wRamp = "120";
            settings.bStartH = "10";
            settings.bStartM = "00";
            settings.bEndH = "22";
            settings.bEndM = "00";
            settings.bMax1 = "80";
            settings.bMax2 = "80";
            settings.bRamp = "120";
            settings.rStartH = "20";
            settings.rStartM = "00";
            settings.rEndH = "12";
            settings.rEndM = "00";

            return settings;
        }
        #endregion

        #region Http Request Handlers
        private static void DefaultPageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }
            e.ResponseRedirectTo = constTankStatusPageFileName;
        }
        private static void TankStatusPageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }
            if (e.HttpRequest.HttpMethod.ToLower() == "post")
            {
                if (e.PostParams.Contains("feed"))
                {
                    Thread Food = new Thread(Feed);
                    Food.Start();
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankStatusPageFileName;
                    return;
                }
                if (e.PostParams.Contains("resetT"))
                {
                    highProbe1 = 1;
                    highProbe2 = 1;
                    lowProbe1 = 30;
                    lowProbe2 = 30;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankStatusPageFileName;
                    return;
                }
                if (e.PostParams.Contains("resetPH"))
                {
                    highPH = 3;
                    lowPH = 10;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankStatusPageFileName;
                    return;
                }
            }
            Hashtable tokens = new Hashtable()
			{
                {"appName", constAppName},
                {"dateTime", DateTime.Now.ToString("T")},
				{"tankNow", temp1.ToString("F3")},
                {"sumpNow", temp2.ToString("F3")},
				{"phNow", "NA"},
				{"tankHighT", highProbe1.ToString("F3")},
				{"tankLowT", lowProbe1.ToString("F3")},
                {"sumpHighT", highProbe2.ToString("F3")},
				{"sumpLowT", lowProbe2.ToString("F3")},
                {"highPH", "NA"},
				{"lowPH", "NA"},
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constTankStatusPageFileName, tokens);
            Debug.GC(true);
        }
        private static void TankSettingsPageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }
            if (e.HttpRequest.HttpMethod.ToLower() == "post")
            {
                if (e.PostParams.Contains("rTemp"))
                {
                    rTemp = !rTemp;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
                if (e.PostParams.Contains("rPH"))
                {
                    rPH = !rPH;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
                if (e.PostParams.Contains("rTimer"))
                {
                    rTimer = !rTimer;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
                if (e.PostParams.Contains("test"))
                {
                    rTimer = false;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("mPump"))
                {
                    if (UART3.IsOpen == true)
                    {
                        UART3.Close();
                        mPump = true;
                    }
                    else
                    {
                        UART3.Open();
                        mPump = false;
                    }
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
                if (e.PostParams.Contains("sPump"))
                {
                    if (UART4.IsOpen == true)
                    {
                        UART4.Close();
                        sPump = true;
                    }
                    else
                    {
                        UART4.Open();
                        sPump = false;
                    }
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
                if (e.PostParams.Contains("fPump"))
                {
                    if (UART2.IsOpen == true)
                    {
                        UART2.Close();
                        fPump = true;
                    }
                    else
                    {
                        UART2.Open();
                        fPump = false;
                    }
                    // this is how it should work with any pin, but the voltage doesn't go high enough for it to work
                    //if (UART2.Read() == true)
                    //{
                    //    UART2.Write(false);
                    //    fPump = true;
                    //}
                    //else
                    //{
                    //    UART2.Write(true);
                    //    fPump = false;
                    //}
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
            }
            // return page
            Hashtable tokens = new Hashtable()
			{
				{"appName", constAppName},
                {"rTemp", rTemp ? "On" : "Off"},
                {"rPH", rPH ? "On" : "Off"},
                {"rTimer", rTimer ? "On" : "Off"},
                {"mPump", mPump ? "On" : "Off"},
                {"sPump", sPump ? "On" : "Off"},
                {"fPump", fPump ? "On" : "Off"},
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constTankSettingsPageFileName, tokens);
            Debug.GC(true);
        }
        private static void TankTimersPageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }
            // save appSettings
            string msg = "";
            if (e.HttpRequest.HttpMethod.ToLower() == "post" && e.PostParams != null)
            {
                if (e.PostParams.Contains("pwd") && (string)e.PostParams["pwd"] == appSettings.Password)
                {
                    if (e.PostParams.Contains("save"))
                    {
                        // save appSettings
                        bool saved = false;
                        try
                        {
                            appSettings.wStartH = (string)e.PostParams["wStartH"];
                            appSettings.wStartM = (string)e.PostParams["wStartM"];
                            appSettings.wEndH = (string)e.PostParams["wEndH"];
                            appSettings.wEndM = (string)e.PostParams["wEndM"];
                            appSettings.wMax = (string)e.PostParams["wMax"];
                            appSettings.wRamp = (string)e.PostParams["wRamp"];
                            appSettings.bStartH = (string)e.PostParams["bStartH"];
                            appSettings.bStartM = (string)e.PostParams["bStartM"];
                            appSettings.bEndH = (string)e.PostParams["bEndH"];
                            appSettings.bEndM = (string)e.PostParams["bEndM"];
                            appSettings.bMax1 = (string)e.PostParams["bMax1"];
                            appSettings.bMax2 = (string)e.PostParams["bMax2"];
                            appSettings.bRamp = (string)e.PostParams["bRamp"];
                            appSettings.rStartH = (string)e.PostParams["rStartH"];
                            appSettings.rStartM = (string)e.PostParams["rStartM"];
                            appSettings.rEndH = (string)e.PostParams["rEndH"];
                            appSettings.rEndM = (string)e.PostParams["rEndM"];

                            if (appSettings.SaveToFlash())
                            {
                                saved = true;
                            }
                            else
                            {
                                msg = "Error saving settings : " + appSettings.LastErrorMsg;
                            }
                        }
                        catch (Exception ex)
                        {
                            msg = "Error saving settings : " + ex.Message;
                        }
                        Debug.GC(true);
                        if (saved)
                        {
                            restartTimer = true;
                            e.ResponseRedirectTo = constTankStatusPageFileName;
                            return;
                        }
                    }
                }
            }
            // return page
            Hashtable tokens = new Hashtable()
			{
				{"appName", constAppName},
                {"wStartH", appSettings.wStartH},
                {"wStartM", appSettings.wStartM},
                {"wEndH", appSettings.wEndH},
                {"wEndM", appSettings.wEndM},
                {"wMax", appSettings.wMax},
                {"wRamp", appSettings.wRamp},
                {"bStartH", appSettings.bStartH},
                {"bStartM", appSettings.bStartM},
                {"bEndH", appSettings.bEndH},
                {"bEndM", appSettings.bEndM},
                {"bMax1", appSettings.bMax1},
                {"bMax2", appSettings.bMax2},
                {"bRamp", appSettings.bRamp},
                {"rStartH", appSettings.rStartH},
                {"rStartM", appSettings.rStartM},
                {"rEndH", appSettings.rEndH},
                {"rEndM", appSettings.rEndM},
                {"pwd", ""},
				{"msg", msg}
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constTankTimersPageFileName, tokens);
            Debug.GC(true);
        }
        private static void DevicePageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }

            Hashtable tokens = new Hashtable()
			{
				{"appName", constAppName},
				{"software", "Version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()},
				{"firmware", "Version " + SystemInfo.Version},
				{"dateTime", DateTime.Now.ToString()},
				{"lastReboot", lastRebootTime.ToString() + " (" + (fezConnect.LastResetCause == GHIElectronics.NETMF.Hardware.LowLevel.Watchdog.ResetCause.WatchdogReset ? "Watchdog" : "Power Up") + ")"},
				{"availMemory", Debug.GC(false).ToString("n0") + " bytes"},
                {"boardNow", temp3.ToString("F3")},
				{"voltage", ((float)Battery.ReadVoltage()/1000).ToString("f") + " volts"},
				{"rtcOK", (rtcIsWorking ? "" : "Not ") + "Working"},
				{"sdCard", (SDCard.IsMounted ? "Mounted" : "Not Mounted")},
				{"host", appSettings.HostName},
				{"mac", appSettings.MACAddress},
				{"ip", NetUtils.IPBytesToString(NetworkInterface.IPAddress)},
				{"mask", NetUtils.IPBytesToString(NetworkInterface.SubnetMask)},
				{"gateway", NetUtils.IPBytesToString(NetworkInterface.GatewayAddress)},
				{"dns", NetUtils.IPBytesToString(NetworkInterface.DnsServer)},
				{"restarts", httpServer.HttpRestarts.ToString()},
				{"requests", httpServer.HttpRequests.ToString()},
				{"404Hits", httpServer.Http404Hits.ToString()}
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constDevicePageFileName, tokens);
            Debug.GC(true);
        }
        private static void SettingsPageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }

            // save appSettings
            string msg = "";
            if (e.HttpRequest.HttpMethod.ToLower() == "post" && e.PostParams != null)
            {
                if (e.PostParams.Contains("pwd") && (string)e.PostParams["pwd"] == appSettings.Password)
                {
                    if (e.PostParams.Contains("save"))
                    {
                        // save appSettings
                        bool saved = false;
                        try
                        {
                            appSettings.HostName = (string)e.PostParams["host"];
                            appSettings.MACAddress = (string)e.PostParams["mac"];
                            appSettings.DHCPEnabled = (e.PostParams.Contains("dhcpEnabled") ? true : false);
                            appSettings.IPAddress = (string)e.PostParams["ip"];
                            appSettings.IPMask = (string)e.PostParams["mask"];
                            appSettings.IPGateway = (string)e.PostParams["gateway"];
                            appSettings.DNSAddress = (string)e.PostParams["dns"];
                            appSettings.NTPEnabled = (e.PostParams.Contains("ntpEnabled") ? true : false);
                            appSettings.NTPServer = (string)e.PostParams["ntpServer"];
                            appSettings.NTPOffset = (string)e.PostParams["ntpOffset"];
                            appSettings.HTTPEnabled = (e.PostParams.Contains("httpEnabled") ? true : false);
                            appSettings.HTTPPort = (string)e.PostParams["httpPort"];
                            if (((string)e.PostParams["npwd"]).Length > 0)
                            {
                                appSettings.Password = (string)e.PostParams["npwd"];
                            }
                            if (appSettings.SaveToFlash())
                            {
                                saved = true;
                            }
                            else
                            {
                                msg = "Error saving settings : " + appSettings.LastErrorMsg;
                            }
                        }
                        catch (Exception ex)
                        {
                            msg = "Error saving settings : " + ex.Message;
                        }
                        Debug.GC(true);

                        // enable restart
                        if (saved)
                        {
                            lock (threadLockObject)
                            {
                                rebootTime = DateTime.Now.AddSeconds(30);
                            }

                            // redirect to restarting page
                            e.ResponseRedirectTo = constRestartPageFileName;
                            return;
                        }
                    }
                }
                else
                {
                    // invalid password
                    msg = "Invalid password. Please try again.";
                }
            }

            // return page
            Hashtable tokens = new Hashtable()
			{
				{"appName", constAppName},
				{"host", appSettings.HostName},
				{"mac", appSettings.MACAddress},
				{"dhcpEnabled", (appSettings.DHCPEnabled ? "checked" : "")},
				{"ip", appSettings.IPAddress},
				{"mask", appSettings.IPMask},
				{"gateway", appSettings.IPGateway},
				{"dns", appSettings.DNSAddress},
				{"ntpEnabled", (appSettings.NTPEnabled ? "checked" : "")},
				{"ntpServer", appSettings.NTPServer},
				{"ntpOffset", appSettings.NTPOffset.ToString()},
				{"httpEnabled", (appSettings.HTTPEnabled ? "checked" : "")},
				{"httpPort", appSettings.HTTPPort.ToString()},
				{"pwd", ""},
				{"npwd", ""},
				{"msg", msg}
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constSettingsPageFileName, tokens);
            Debug.GC(true);
        }
        private static void RestartPageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }

            Hashtable tokens = new Hashtable()
			{
				{"appName", constAppName}
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constRestartPageFileName, tokens);
            Debug.GC(true);
        }
        private static void ColorBalancePageHandler(string requestURL, HTTPServer.HttpRequestEventArgs e)
        {
            if (SDCard.IsMounted == false && SDCard.MountSD() == false)
            {
                e.ResponseText = GetSDCardErrorResponse();
                return;
            }
            if (e.HttpRequest.HttpMethod.ToLower() == "post")
            {
                if (e.PostParams.Contains("w+1"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Min(wNow + 1, 100);
                    Ramp(wLast, wNow, up, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w+5"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Min(wNow + 5, 100);
                    Ramp(wLast, wNow, up, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w+10"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Min(wNow + 10, 100);
                    Ramp(wLast, wNow, up, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w+25"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Min(wNow + 25, 100);
                    Ramp(wLast, wNow, up, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w-1"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Max(wNow - 1, 0);
                    Ramp(wLast, wNow, down, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w-5"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Max(wNow - 5, 0);
                    Ramp(wLast, wNow, down, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w-10"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Max(wNow - 10, 0);
                    Ramp(wLast, wNow, down, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("w-25"))
                {
                    int wLast = wNow;
                    wNow = System.Math.Max(wNow - 25, 0);
                    Ramp(wLast, wNow, down, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b+1"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Min(bNow + 1, 100);
                    Ramp(bLast, bNow, up, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b+5"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Min(bNow + 5, 100);
                    Ramp(bLast, bNow, up, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b+10"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Min(bNow + 10, 100);
                    Ramp(bLast, bNow, up, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b+25"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Min(bNow + 25, 100);
                    Ramp(bLast, bNow, up, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b-1"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Max(bNow - 1, 0);
                    Ramp(bLast, bNow, down, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b-5"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Max(bNow - 5, 0);
                    Ramp(bLast, bNow, down, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b-10"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Max(bNow - 10, 0);
                    Ramp(bLast, bNow, down, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("b-25"))
                {
                    int bLast = bNow;
                    bNow = System.Math.Max(bNow - 25, 0);
                    Ramp(bLast, bNow, down, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v+1"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Min(vNow + 1, 100);
                    Ramp(vLast, vNow, up, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v+5"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Min(vNow + 5, 100);
                    Ramp(vLast, vNow, up, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v+10"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Min(vNow + 10, 100);
                    Ramp(vLast, vNow, up, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v+25"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Min(vNow + 25, 100);
                    Ramp(vLast, vNow, up, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v-1"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Max(vNow - 1, 0);
                    Ramp(vLast, vNow, down, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v-5"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Max(vNow - 5, 0);
                    Ramp(vLast, vNow, down, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v-10"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Max(vNow - 10, 0);
                    Ramp(vLast, vNow, down, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("v-25"))
                {
                    int vLast = vNow;
                    vNow = System.Math.Max(vNow - 25, 0);
                    Ramp(vLast, vNow, down, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("resetWhite"))
                {
                    int wLast = wNow;
                    wNow = 0;
                    Ramp(wLast, wNow, down, 0, white);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("resetBlue"))
                {
                    int bLast = bNow;
                    bNow = 0;
                    Ramp(bLast, bNow, down, 0, blue);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("resetViolet"))
                {
                    int vLast = vNow;
                    vNow = 0;
                    Ramp(vLast, vNow, down, 0, violet);
                    Debug.GC(true);
                    e.ResponseRedirectTo = constColorBalancePageFileName;
                    return;
                }
                if (e.PostParams.Contains("exit"))
                {
                    Ramp(0, 0, down, 0, white);
                    Ramp(0, 0, down, 0, blue);
                    Ramp(0, 0, down, 0, violet);
                    wNow = 0;
                    bNow = 0;
                    vNow = 0;
                    rTimer = true;
                    Debug.GC(true);
                    e.ResponseRedirectTo = constTankSettingsPageFileName;
                    return;
                }
            }
            // return page
            Hashtable tokens = new Hashtable()
			{
				{"appName", constAppName},
                {"whiteNow", wNow.ToString()},
                {"blueNow", bNow.ToString()},
                {"violetNow", vNow.ToString()},
			};
            e.ResponseHtml = new HtmlFileReader(SDCard.RootDirectory + "\\" + constColorBalancePageFileName, tokens);
            Debug.GC(true);
        }
        private static string GetSDCardErrorResponse()
        {
            return "<html><header><title>" + constAppName + " - Error</title></header><body><h3>" + constAppName + " encountered an error:</h3><p>SD card not found.</p></body></html>";
        }
        #endregion

        #region Event Handlers
        private static void httpServer_HttpStatusChanged(HTTPServer.HTTPStatus httpStatus, string message)
        {
            Debug.Print(constAppName + " : Http status : " + httpServer.StatusName + (message.Length > 0 ? " : " + message : ""));
            Debug.Print("Free mem : " + Debug.GC(false).ToString());
        }
        #endregion
    }
}

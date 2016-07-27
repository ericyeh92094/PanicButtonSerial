using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Device.Location;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.Threading;
using System.Management;


namespace PanicButtonSerial
{
    class Program
    {
        [DllImport("HIDCtrl.dll")]
        internal static extern int PowerOnEx(int i);
        [DllImport("HIDCtrl.dll")]
        internal static extern int PowerOffEx(int i);

        static string devPos = "";
        static string subKey = "SOFTWARE\\TVWS";
        static string KeyName = "PanicButtonID";
        static string monitorId = "1";

        static bool _continue;
        static SerialPort _serialPort;
        static Thread readThread = null;

        public static string ReadDeviceIDFromKey()
        {
            // Opening the registry key
            RegistryKey rk = Registry.CurrentUser;
            // Open a subKey as read-only
            RegistryKey sk1 = rk.OpenSubKey(subKey);
            // If the RegistrySubKey doesn't exist -> (null)
            if (sk1 == null)
            {
                return null;
            }
            else
            {
                try
                {
                    // If the RegistryKey exists I get its value
                    // or null is returned.
                    return (string)sk1.GetValue(KeyName);
                }
                catch (Exception e)
                {
                    return null;
                }
            }
        }

        public static bool WriteDeviceIDToKey(object Value)
        {
            try
            {
                // Setting
                RegistryKey rk = Registry.CurrentUser;
                // I have to use CreateSubKey 
                // (create or open it if already exits), 
                // 'cause OpenSubKey open a subKey as read-only
                RegistryKey sk1 = rk.CreateSubKey(subKey);
                // Save the value
                sk1.SetValue(KeyName, Value);

                return true;
            }
            catch (Exception e)
            {
                // AAAAAAAAAAARGH, an error!
                // ShowErrorMessage(e, "Writing registry " + KeyName.ToUpper());
                return false;
            }
        }

        public static void BuzzControl(bool on)
        {
            if (on)
                PowerOnEx(1);
            else
                PowerOffEx(1);
        }

        private static void PingOut()
        {
            string str = "SOS" + monitorId.ToString();
            _serialPort.WriteLine(str);

            /*
            var client = new RestClient(monitorUrl);
            var request = new RestRequest(Method.POST);

            request.AddParameter("error", monitorId.ToString());
            IRestResponse response = client.Execute(request);
            */
        }
        private static void GetConsoleKey()
        {
            while (true)
            {
                bool F4pressed = false;
                int presscount = 0;
                ConsoleKeyInfo key;

                // wait for initial keypress:
                while (!Console.KeyAvailable)
                {
                    System.Threading.Thread.Sleep(10);
                }

                key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.F4:
                        F4pressed = true;
                        Console.WriteLine("F4 pressed start.");
                        break;
                    case ConsoleKey.F1:
                    case ConsoleKey.Escape:
                        Console.WriteLine("Program Exit");
                        _continue = true;
                        BuzzControl(false);
                        return;
                    default:
                        F4pressed = false;
                        continue;
                }

                /*

                DateTime nextCheck = DateTime.Now.AddMilliseconds(1000);

                while ((nextCheck > DateTime.Now) && F4pressed)
                {
                    if (Console.KeyAvailable)
                    {
                        key = Console.ReadKey(true);
                        switch (key.Key)
                        {
                            case ConsoleKey.F4:
                                Console.WriteLine("F4 pressed.");
                                presscount++;
                                F4pressed = true;
                                break;
                            case ConsoleKey.F1:
                            case ConsoleKey.Escape:
                                Console.WriteLine("Program Exit");
                                BuzzControl(false);
                                return;
                            default:
                                F4pressed = false;
                                break;
                        }
                    }
                }
                */
                //if (F4pressed && (presscount > 3))
                if (F4pressed)
                {
                    Console.WriteLine("Alarm Fired");
                    PingOut();
                    BuzzControl(true);
                    Task.Delay(6000).Wait();
                    BuzzControl(false);
                }
            }
        }

        public static void SerialRead()
        {
            while (_continue)
            {
                try
                {
                    string message = _serialPort.ReadLine();

                    if (message.StartsWith("GPS"))
                    {
                        _serialPort.WriteLine(devPos);
                    }
                    if (message.StartsWith("SET ID"))
                    {
                        int idx = message.IndexOf("=");
                        monitorId = message.Substring(idx + 1);

                        _serialPort.WriteLine("OK");
                    }
                    if (message.StartsWith("BUZZ ON"))
                    {
                        BuzzControl(true);
                        _serialPort.WriteLine("OK");
                    }
                    if (message.StartsWith("BUZZ OFF"))
                    {
                        BuzzControl(false);
                        _serialPort.WriteLine("OK");
                    }
                }
                catch (TimeoutException) { }
            }
        }

        public static string EnumPorts()
        {
            string namePort = "";
            //Below is code pasted from WMICodeCreator
            try
            {
                ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM MSSerial_PortName");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("MSSerial_PortName instance");
                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("InstanceName: {0}", queryObj["InstanceName"]);

                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("MSSerial_PortName instance");
                    Console.WriteLine("-----------------------------------");
                    Console.WriteLine("PortName: {0}", queryObj["PortName"]);

                    //If the serial port's instance name contains USB 
                    //it must be a USB to serial device
                    if (queryObj["InstanceName"].ToString().Contains("USB"))
                    {
                        Console.WriteLine(queryObj["PortName"] + "is a USB to SERIAL adapter / converter");
                        namePort = (string)queryObj["PortName"];

                        return namePort;

                    }
                }
            }
            catch (ManagementException e)
            {
                //MessageBox.Show("An error occurred while querying for WMI data: " + e.Message);
                namePort = "";
            }

            return namePort;
        }

        public static void SetupSerial()
        {
            StringComparer stringComparer = StringComparer.OrdinalIgnoreCase;

            readThread = new Thread(SerialRead);

            // Create a new SerialPort object with default settings.
            _serialPort = new SerialPort();

            string PortName = SerialPort.GetPortNames()[0]; // get the first free com port
            PortName = EnumPorts();
            if (PortName == "") // no port exist, abort the program
                System.Environment.Exit(0);
 
            _serialPort.PortName = PortName;
            _serialPort.BaudRate = 9600;
            _serialPort.Parity = Parity.None;
            _serialPort.DataBits = 8;
            _serialPort.StopBits = StopBits.One;
            _serialPort.Handshake = Handshake.None;

            // Set the read/write timeouts
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;

            _serialPort.Open();
            _continue = true;
            readThread.Start();
        }

        public static void CloseSerial()
        {
            readThread.Join();
            _serialPort.Close();
        }


        private static void GeoPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            devPos = e.Position.Location.Latitude + "/" + e.Position.Location.Longitude;
        }
        static void Main(string[] args)
        {
            // Command line parsing
            Arguments CommandLine = new Arguments(args);

            if (CommandLine["deviceid"] != null)
            {
                Console.WriteLine("Device ID: " + CommandLine["deviceid"]);
                if (int.Parse(CommandLine["deviceid"]) > 0)
                    monitorId = CommandLine["deviceid"];
            }
         
            GeoCoordinateWatcher watcher = new GeoCoordinateWatcher();
            watcher.PositionChanged +=
                new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(GeoPositionChanged);

            watcher.Start();

            SetupSerial();
            GetConsoleKey();

            CloseSerial();

        }
    }
}

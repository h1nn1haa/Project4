/// University of Washington EEP 598/CSEP 590: Neural Devices, Systems, and Computation
/// Project 2 - DLL Checkpoint Code
/// Written by Jeffrey Herron and Cory Lam

using System;
using System.IO.Ports;
using DistributedDLL;

namespace APP
{
     internal class Program
    {
        public static void Main()
        {
            using DistributedAPI api = new();

            Console.WriteLine("Application Console Message - Note: due to continual watchdog and minute-interval resets on remote device, it may take several iterations to connect to the device.");

            // Repeatedly try to connect so that the delay period on the startup doesn't trip us up.
            do
            {
                foreach (string portName in SerialPort.GetPortNames())
                {
                    Console.WriteLine($"Application Console Message - Attempting to connect on {portName}");
                    if (api.Connect(portName) == ConnectStatus.CONN_SUCCESS)
                    {
                        Console.WriteLine($"Application Console Message - Distributed API connected on {portName}");
                        break;
                    }
                }
            } while (!api.IsConnected);


            if (!api.IsConnected)
            {
                Console.WriteLine("Application Console Message - Distributed API was unable to connect.");
                return;

            }

            // Subscribe the event handler to the streaming data events
            api.StreamDataEvent += Api_streamDataEvent;

            // System connected, start the stream!
            Console.WriteLine("\n\nApplication Message  - Connected to Port, will now run indefinitely\n");

            Console.WriteLine("Application Message - Press A to start therapy, B to stop therapy, Q to quit");
            while (api.IsAlive)
            {
                switch(Console.ReadKey().Key)
                {
                    case ConsoleKey.Q:
                        Console.WriteLine("Application Console Message - Exit key detected. Closing...");
                        return;
                    case ConsoleKey.A:
                        Console.WriteLine("\nStarting therapy...");
                        api.StartTherapy();
                        break;
                    case ConsoleKey.B:
                        Console.WriteLine("\nStopping thearpy...");
                        api.StopTherapy();
                        break;
                }
            }
        }

        private static void Api_streamDataEvent(object? sender, StreamDataEventArgs e)
        {
            //Console.Write("Application Message  - New Streamed Data Event: ");
            //Console.Write(e.Data);
            //Console.Write(" Timestamp: ");
            //Console.WriteLine(e.Timestamp.ToString("0.000"));
        }
    }
}
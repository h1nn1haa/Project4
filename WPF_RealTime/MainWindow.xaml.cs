using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO.Ports;
using DistributedDLL;
using System.Timers;

namespace WPF_RealTime
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<ushort> theData = new List<ushort>();
        private object theDataLock = new object();
        private System.Timers.Timer graphUpdateTimer = new Timer(200);
        private int graphLength = 200;
        DistributedAPI project2;

        public MainWindow()
        {
            InitializeComponent();
        }

        public void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Create DLL Object for application
            project2 = new DistributedAPI();

            // Request DLL to return available com ports
            string[] availableComPorts =  SerialPort.GetPortNames();

            // Attempt connection on all serial ports until a good one is found
            for (int i = 0; i < availableComPorts.Length; i++)
            {
                Console.WriteLine("Application Message - Attempting connection on: " + availableComPorts[i]);
                if (project2.Connect(availableComPorts[i]) == ConnectStatus.CONN_SUCCESS)
                {
                    // Success achieved, break out of connection test loop
                    Console.WriteLine("Application Message - Success connecting to: " + availableComPorts[i]);
                    break;
                }
            }

            // System connected, start the stream!
            Console.WriteLine("\n\nApplication Message  - Connected to Port, will now run indefinitely\n");
            project2.StartStreaming();
            project2.StreamDataEvent += Project2_NewDataReceivedEvent; ;
            Console.WriteLine("\n\nApplication Message - Streaming has begun\n");

            // Start the update timer
            graphUpdateTimer.Elapsed += graphUpdateTimer_Elapsed;
            graphUpdateTimer.Start();
        }

        public void MainWindow_Closed(object sender, EventArgs e)
        {
            graphUpdateTimer.Stop();
            project2.Dispose();
        }

        private void Project2_NewDataReceivedEvent(object? sender, StreamDataEventArgs e)
        {
            lock (theDataLock)
            {
                theData.Add(e.Data);
                int trimDelta = theData.Count - graphLength;
                if (trimDelta > 0)
                {
                    theData.RemoveRange(0, trimDelta);
                }
            }
        }

        private void graphUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Clear the chart to prep it for updating
            neuroDataChart.Invoke(new System.Windows.Forms.MethodInvoker(
                delegate {
                    List<ushort> tempBuffer;
                    lock (theDataLock)
                    {
                        tempBuffer = new List<ushort>(theData);
                    }
                    
                    neuroDataChart.Series[0].Points.DataBindY(tempBuffer);
                }));
        }
    }
}

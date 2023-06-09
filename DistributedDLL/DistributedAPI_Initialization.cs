/// University of Washington EEP 598/CSEP 590: Neural Devices, Systems, and Computation
/// Project 4
/// Han Diep

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Asn1;
using System.IO.Ports;
using System.Reflection.Emit;
using System.Timers;

namespace DistributedDLL
{
 
    public partial class DistributedAPI : IDisposable
    {

        /// <summary>
        /// Whether or not the API has been disposed. Objects can only be disposed once.
        /// </summary>
        /// <seealso cref="DistributedAPI.Dispose"/>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// <see langword="true"/> if we have given up on contact with the distributed system.
        /// </summary>
        /// <seealso cref="DistributedAPI.MAX_RECONNECT_ATTEMPTS"/>
        public bool IsAlive { get; private set; }

        /// <summary>
        /// <see langword="true"/> if we are connected to a distributed system over a SerialPort, <see langword="false"/> otherwise.
        /// </summary>
        public bool IsConnected { get; private set; }


        /// <summary>
        /// <see langword="true"/> if we are streaming data from the distributed system over a SerialPort, <see langword="false"/> otherwise.
        /// </summary>
        public bool IsStreaming { get; private set; }

        private const int BAUDRATE = 115_200;
        private const int READ_TIMEOUT = 500;
        private const int WRITE_TIMEOUT = 500;
        private const int WATCHDOG_POLL_MS = 4000;
        private const int MAX_WATCHDOG_RESET_ATTEMPTS = 3;
        private const int MAX_RECONNECT_ATTEMPTS = 3;

        private byte _packetID;

        private readonly Packet _watchdogResetReq = new(PacketType.TRANSACTION, 0, new byte[1] { (byte)OpCode.WATCHDOG_RESET });

        private readonly System.Timers.Timer _timer;

        private readonly SerialPort _serialPort;
        private readonly object _serialWriteLock;

        /// <summary>
        /// Create an instance of the <see cref="DistributedAPI"/>. This doesn't not connect to serial ports.
        /// </summary>
        /// <seealso cref="DistributedAPI.Connect(string)"/>
        public DistributedAPI()
        {
            this._serialWriteLock = new();

            this._serialPort = new()
            {
                BaudRate = BAUDRATE,
                ReadTimeout = READ_TIMEOUT,
                WriteTimeout = WRITE_TIMEOUT
            };

            this._timer = new(WATCHDOG_POLL_MS)
            {
                AutoReset = false
            };

            this._pendingRequests = new ConcurrentQueue<Request>();
            this._streamBuffer = new(new ConcurrentQueue<Packet>(), STREAM_BUFFER_CAPACITY);
            this._streamData = new ConcurrentQueue<double>();

            this._serialRxThread = new Thread(this.RxThreadHandler);
            this._streamLoggerThread = new Thread(this.StreamLogThreadHandler);
            this._classifyAndDeliverTherapyThread = new Thread(this.ClassifyAndDeliverTherapyHandler);
            this._therapyOn = false;

            this._timer.Elapsed += this.WatchdogElapsedEvent;
        }

        /// <summary>
        /// Open serial port associated with portName
        /// </summary>
        /// <param name="portName">Port to attempt an connection to</param>
        /// <returns><see cref="ConnectStatus"/> enum that describes success or failure of the connection</returns>
        public ConnectStatus Connect(string portName)
        {
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException(this.ToString());
            }

            if (this.IsConnected)
            {
                return ConnectStatus.ERR_AREADY_CONNECTED;
            }

            this._serialPort.PortName = portName;

            // Try to connect
            try
            {
                // Open the port
                this._serialPort.Open();

                // Clear the buffers
                this._serialPort.DiscardInBuffer();
                this._serialPort.DiscardOutBuffer();
            }
            catch (Exception ex)
            {

                // tmpPort.Open() failed.

                Console.WriteLine($"Distributed Interface DLL - Could not open {portName}: {ex.Message}");
                return ConnectStatus.CONN_FAILURE;
            }

            byte[]? recvBuffer;
            Packet commInitPacket = new(PacketType.TRANSACTION, this._packetID, new byte[1] { (byte)OpCode.CONNECT });

            try
            {
                // Attempt to handshake...
                this.SendPacket(commInitPacket);
                recvBuffer = this.RecvRawPacket();

                if (recvBuffer == null)
                {
                    Console.WriteLine($"Distributed Interface DLL - Invalid Firmware Response.");
                    this._serialPort.Close();
                    this._packetID = 0;
                    return ConnectStatus.CONN_FAILURE;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Distributed Interface DLL - Communications error: {ex.Message}");
                this._serialPort.Close();
                this._packetID = 0;
                return ConnectStatus.CONN_FAILURE;
            }

            if (!commInitPacket.bytes.SequenceEqual(recvBuffer))
            {
                // Unsuccessful handshake
                this._serialPort.Close();
                this._packetID = 0;
                return ConnectStatus.CONN_FAILURE;
            }

            // Successful handshake
            this._packetID++;
            this.IsConnected = true;
            this.IsAlive = true;

            this._serialRxThread = new Thread(this.RxThreadHandler);
            this._streamLoggerThread = new Thread(this.StreamLogThreadHandler);
            this._classifyAndDeliverTherapyThread = new Thread(this.ClassifyAndDeliverTherapyHandler);

            this._serialRxThread.Start();
            this._streamLoggerThread.Start();
            this._classifyAndDeliverTherapyThread.Start();

            this._timer.Start();

            Console.WriteLine("Distributed Interface DLL: Watchdog timer started successfully.");
            
            Console.WriteLine("Distributed Interface DLL: Starting streaming...");
            Thread.Sleep(500);
            this.StartStreaming();
            
            return ConnectStatus.CONN_SUCCESS;
        }

        /// <summary>
        /// Allows user to dispose the interface (releases resources held by the API).
        /// </summary>
        public void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            GC.SuppressFinalize(this);

            this.Disconnect();
            this._timer.Dispose();
            this.IsDisposed = true;
        }

        /// <summary>
        /// Disconnects the distributed API's connection to the distributed system over the SerialPort.
        /// </summary>
        private void Disconnect()
        {
            if (this.IsConnected)
            {
                this.IsConnected = false;

                this._serialRxThread.Join();
                this._streamLoggerThread.Join();

                this._classifyAndDeliverTherapyThread.Join();
                this._streamData.Clear();
                this.StopTherapy();

                this._pendingRequests.Clear();
                this._serialPort.Close();


            }
        }

        /// <summary>
        /// Watchdog event that is called periodically to reset the Arduino watchdog.
        /// </summary>
        /// <param name="sender">sender of the event (timer)
        /// <param name="e">Information about the event
        private void WatchdogElapsedEvent(object? sender, ElapsedEventArgs e)
        {
            Packet? response;

            if (!this.IsConnected)
            {
                return;
            }

            for (int watchdogResetAttempts = 0; watchdogResetAttempts < MAX_WATCHDOG_RESET_ATTEMPTS; watchdogResetAttempts++)
            {
                try
                {
                    this._watchdogResetReq.UpdatePacketID(this._packetID++);
                    response = this.SendPacket(this._watchdogResetReq);
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"Distributed Interface DLL - Watchdog Service Error: {ex.Message}");
                    continue;
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"Distributed Interface DLL - Watchdog Service Error: {ex.Message}. Was the cable disconnected?");
                    continue;
                }

                if (response == null)
                {
                    Console.WriteLine($"Distributed Interface DLL - Watchdog Service Error: Operation timed out.");
                    continue;
                }

                if (this._watchdogResetReq.bytes.SequenceEqual(response.bytes))
                {
                    Console.WriteLine("Distributed Interface DLL - Watchdog Service Notice: I am still connected!");
                    this._timer.Start();
                    return;
                }
                else
                {
                    if (response.Payload[0] == (byte)ErrCode.BAD_CHECKSUM)
                    {
                        Console.WriteLine("Distributed Interface DLL - Watchdog Service Error: bad checksum received by Arduino");
                    }
                    else if (response.Payload[0] == (byte)ErrCode.PAYLOAD_LENGTH_EXCEEDS_MAX)
                    {
                        Console.WriteLine("Distributed Interface DLL - Watchdog Service Error: payload too large.");
                    }
                }
            }

            // Arduino is not responding. Release resources associated with the connection.
            this.Disconnect();

            Console.WriteLine("Distributed Interface DLL - Watchdog Service Notice: Initiating Reconnection...");

            // Try to connect up to MAX_RECONNECT_ATTEMPTS and then give up.
            for (int i = 0; i < MAX_RECONNECT_ATTEMPTS; i++)
            {
                var status = this.Connect(this._serialPort.PortName);
                if (status == ConnectStatus.CONN_SUCCESS)
                {
                    // Automatically restart streaming if we were previously streaming.
                    if (this.IsStreaming)
                    {
                        this.IsStreaming = false;
                        this.StartStreaming();
                    }

                    this._timer.Start();
                    Console.WriteLine("Distributed Interface DLL - Watchdog Service Notice: I have reconnected!");
                    return;
                }
                Console.WriteLine("Distributed Interface DLL - Watchdog Service Notice: I have not reconnected yet...");
                Thread.Sleep(1500);
            }

            this.IsAlive = false;

            Console.WriteLine("Distributed Interface DLL - Max reconnect attempts reached. I guess I'll die now. ¯\\_(ツ)_/¯");
        }

    }
}
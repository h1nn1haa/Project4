using System.Collections;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;

namespace DistributedDLL
{
    public partial class DistributedAPI
    {

        private Thread _classifyAndDeliverTherapyThread;

        // true if therapy is being delivered, false otherwise
        private bool _therapyOn;

        public void StartTherapy()
        {
            Packet req = new(PacketType.TRANSACTION, this._packetID, new byte[1] { (byte)OpCode.START_STIMULATION });
            Packet? response = this.SendPacket(req);

            if (response == null)
            {
                Console.WriteLine("Distributed Interface DLL - Start Therapy: Operation timed out.");
                return;
            }

            if (!req.bytes.SequenceEqual(response.bytes))
            {
                if (response.Payload[0] == (byte)ErrCode.BAD_CHECKSUM)
                {
                    Console.WriteLine("Distributed Interface DLL - Start Therapy: bad checksum received by Arduino");
                }
                else if (response.Payload[0] == (byte)ErrCode.PAYLOAD_LENGTH_EXCEEDS_MAX)
                {
                    Console.WriteLine("Distributed Interface DLL - Start Therapy: payload too large.");
                }

                return;
            }

            this._therapyOn = true;

            Console.WriteLine("Distributed Interface DLL - Therapy Started");
            return;
        }

        public void StopTherapy() 
        {
            Packet req = new(PacketType.TRANSACTION, this._packetID, new byte[1] { (byte)OpCode.STOP_STIMULATION });
            Packet? response = this.SendPacket(req);

            if (response == null)
            {
                Console.WriteLine("Distributed Interface DLL - Stop Therapy: Operation timed out.");
                return;
            }

            if (!req.bytes.SequenceEqual(response.bytes))
            {
                if (response.Payload[0] == (byte)ErrCode.BAD_CHECKSUM)
                {
                    Console.WriteLine("Distributed Interface DLL - Stop Therapy: bad checksum received by Arduino");
                }
                else if (response.Payload[0] == (byte)ErrCode.PAYLOAD_LENGTH_EXCEEDS_MAX)
                {
                    Console.WriteLine("Distributed Interface DLL - Stop Therapy: payload too large.");
                }

                return;
            }

            this._therapyOn = false;
            Console.WriteLine("Distributed Interface DLL - Therapy Stopped");
            return;
        }

        private void ClassifyAndDeliverTherapyHandler()
        {
            while (this.IsConnected)
            {
                if (this._streamData.Count >= WINDOW_SIZE)
                {

                    double[] signal = new double[WINDOW_SIZE + 2]; // need 2 extra zeroes at end for FFT

                    IEnumerator enumerator = _streamData.GetEnumerator();

                    for (int i = 0; i < WINDOW_SIZE; i++)
                    {
                        enumerator.MoveNext();
                        signal[i] = (double)enumerator.Current;
                    }

                    if (this.classify(signal))
                    {
                        if (!this._therapyOn)
                        {
                            this.StartTherapy();
                        }
                    }
                    else
                    {
                        if (this._therapyOn)
                        {
                            this.StopTherapy();
                        }
                    }

                    // remove beginning of window
                    for (int i = 0; i < 10; i++)
                    {
                        this._streamData.TryDequeue(out double point);
                    }

                }
            }

        }
    }
}

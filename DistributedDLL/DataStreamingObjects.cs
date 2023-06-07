/// University of Washington EEP 598/CSEP 590: Neural Devices, Systems, and Computation
/// Project 2 - DLL Checkpoint Code
/// Written by Jeffrey Herron and Cory Lam

using System;
using System.IO;

namespace DistributedDLL
{
    // This file contains all of the Distributed Interface's functions for communicating to the Arduino
    public class StreamDataEventArgs : EventArgs
    {
        
        /// <summary>
        /// The packet ID of the stream packet recieved
        /// </summary>
        public byte PacketID { get; private set; }

        /// <summary>
        /// The EEG data of the stream packet recieved
        /// </summary>
        public double Data { get; private set; }

        /// <summary>
        /// The timestamp of the EEG data of the stream packet
        /// </summary>
        public float Timestamp { get; private set; }


        // Constructor
        public StreamDataEventArgs(byte packetID, float timestamp, double data)
        {
            // Assign internal variables
            this.PacketID = packetID;
            this.Timestamp = timestamp;
            this.Data = data;
        }
    }
}

using System;
using System.IO.Ports;

namespace New_NitrogenApp
{
    class ReedMcDonaldProgram : MainProgram
    {
        SerialPort _serialPort;
        int counter = 1;
        int ret;
        short m_dev;
        int quantity;
        int pauseTime = 1500;
        double[] weightval = new double[9500];
        double start_weight = 0, final_weight = 0.0, end_weight;
        string[] userdata = new string[2];

        // Specific
        double fillTime;
        uint int_value;
        DateTime start;
        DateTime end;
        TimeSpan duration;
        double actualFillTime = 0;

        public ReedMcDonaldProgram(int _counter, int _ret, short _m_dev, int _quantity, int _pauseTime, double[] _weightval, double _start_weight, double _final_weight, double _end_weight, SerialPort _port, string[] _userdata)
        {
            this.counter = _counter;
            this.ret = _ret;
            this.m_dev = _m_dev;
            this.quantity = _quantity;
            this.pauseTime = _pauseTime;
            this.weightval = _weightval;
            this.start_weight = _start_weight;
            this.final_weight = _final_weight;
            this.end_weight = _end_weight;
            this._serialPort = _port;
            this.userdata = _userdata;
        }

        public void Main_Program()
        {
            TransInfo trans = new TransInfo();
            trans.start_weight = 0.0;
            trans.end_weight = 0.0;
            string relayClosed;
            bool validEntry = true;

            if (validEntry)
            {
                quantity = Convert.ToInt32(userdata[0]);
                Console.WriteLine("Quantity = " + quantity.ToString());

                /*
                   using slope-intercept foumula for linear equation
                   y=mx+b
                   y=fillTime
                   x=quantity
                   m=slope-- determined through tests=4720
                   b=y-intercept-- determined through test data=51000
                */

                fillTime = (4720) * quantity + 51000;
                Console.WriteLine("FillTime = " + fillTime.ToString());

                // Turn on relay switch
                m_dev = DASK.Register_Card(DASK.PCI_7250, 0);
                if (m_dev < 0)
                {
                    WriteError("Register_Card Error");
                }
                ret = DASK.DO_WritePort((ushort)m_dev, 0, 15);
                if (ret < 0)
                {
                    WriteError("DO_WritePort Error");
                }

                Console.WriteLine("Current Time = " + DateTime.Now.ToString());
                start = DateTime.Now;
                Console.WriteLine("Current Minutes and Seconds in Milliseconds = " + start.ToString("fff"));

                // Initial reading of relay (will not be 0)
                ret = DASK.DI_ReadPort((ushort)m_dev, 0, out int_value);
                relayClosed = string.Format("{0}", int_value);

                // Keep relay turned on
                while ((actualFillTime <= fillTime) && (relayClosed != "0"))
                {
                    Console.WriteLine("Still Filling");
                    end = DateTime.Now;
                    Console.WriteLine("Inside While Loop -> Current Minutes and Seconds in Milliseconds = " + end.ToString());
                    duration = end - start;
                    actualFillTime = (duration.Minutes * 60000) + (duration.Seconds * 1000);
                    Console.WriteLine("Inside While Loop -> Actual Fill Time = " + actualFillTime.ToString());
                    ret = DASK.DI_ReadPort((ushort)m_dev, 0, out int_value);
                    relayClosed = string.Format("{0}", int_value);
                }

                if (relayClosed == "1")
                {
                    WriteError("Relay Closed Prematurely - Filling Stopped");
                }
                else
                {
                    WriteError("Required Time Elapsed - Filling Stopped");
                }

                // Closing relay if not already closed
                ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);
                if (ret < 0)
                {
                    WriteError("DO_WritePort error");
                }

                Console.WriteLine("Relay Closed");

                end = DateTime.Now;
                Console.WriteLine("Time relay was closed = " + end.ToString());
                duration = end - start;
                actualFillTime = (duration.Minutes * 60000) + (duration.Seconds * 1000);
                Console.WriteLine("Actual Fill Time = " + actualFillTime.ToString());

                if(actualFillTime == fillTime)
                {
                    trans.recieved_qty = quantity;
                }
                else if(actualFillTime < 60000)
                {
                    trans.recieved_qty = 0;
                }
                else
                {
                    // Using linear equation formula to find quantity given actual filling time
                    trans.recieved_qty = (actualFillTime - 51000) / 4720;
                }

                Console.WriteLine("Quantity Recieved = " + trans.recieved_qty.ToString());
                WriteSuccess("Complete!");

                trans.time = Math.Abs(double.Parse(end.ToString("fff")) - double.Parse(start.ToString("fff")));

                trans.refno = GetTransRefNo();
                GetTransInfo(ref trans);
                InsertTransInfo(ref trans);

                trans.refno = GetTransRefNo_New();
                GetTransInfo_New(ref trans);
                InsertTransInfo_New(ref trans);
                SendInfo(ref trans);
            }
        }
    }
}

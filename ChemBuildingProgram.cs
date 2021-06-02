using System;
using System.Threading;
using System.IO.Ports;
using System.Net;
using System.IO;
using System.Text;

namespace New_NitrogenApp
{
    class ChemBuildingProgram : MainProgram
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
        TransInfo trans;

        public ChemBuildingProgram(int _counter, int _ret, short _m_dev, int _quantity, int _pauseTime, double[] _weightval, double _start_weight, double _final_weight, double _end_weight, SerialPort _port, string[] _userdata)
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
            try
            {
                trans = new TransInfo();
                trans.quantity = 0.0;
                trans.recieved_qty = 0.0;
                trans.refno = 0;
                trans.start_weight = 0;
                trans.time = 0;
                trans.advisor = "";
                trans.end_weight = 0;
                trans.username = "";

                bool validEntry = true; // if user is a valid user
                double expect_weight = 0;
                string temp2 = "";
                double final_weight1 = 0.0;
                int index;
                bool decrease = true;
                bool fail = false;
                bool complete = false;
                int int_count = 1;
                double startTime, endTime;

                if (validEntry == true)
                {
                    // Initializes quantity variable
                    quantity = Int32.Parse(userdata[0]);
                    Console.WriteLine("Quantity: " + quantity.ToString());

                    // Sets the expected weight to a ratio value
                    expect_weight = quantity * 0.8083;
                    Console.WriteLine("Expected Weight: " + expect_weight.ToString());

                    // Close port if it is already open
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }

                    int PORT_OPEN_MAX_ATTEMPTS = 10;
                    bool portOpened = false;
                    int portOpenAttempts = 0;

                    // Open the port
                    if (!_serialPort.IsOpen)
                    {
                        while (!portOpened)
                        {
                            try
                            {
                                _serialPort.Open();
                                portOpened = true;
                            }
                            catch (UnauthorizedAccessException ex)
                            {
                                // In order to try to fix Error: 'Access to port COM1 is denied
                                if (portOpenAttempts++ < PORT_OPEN_MAX_ATTEMPTS)
                                {
                                    WriteError("Port is being used by another process, trying again:: Attempts -> " + portOpenAttempts);
                                    Thread.Sleep(100);
                                    _serialPort.Close();
                                }
                                else
                                {
                                    throw (ex);
                                }
                            }
                        }
                    }

                    // Pause the program to allow for a response
                    Thread.Sleep(pauseTime);

                    // Turn on the relay switch
                    m_dev = DASK.Register_Card(DASK.PCI_7250, 0);
                    if (m_dev < 0)
                    {
                        WriteError("Register_Card Error");
                    }
                    ret = DASK.DO_WritePort((ushort)m_dev, 0, 15);

                    Console.WriteLine("Beginning Time: " + DateTime.Now.ToString());

                    // Init all values in weightval
                    for (int k = 0; k < 9500; k++)
                    {
                        weightval[k] = 0.0;
                    }

                    // Placed to check beginning weight system stopping too quickly on smaller tanks
                    _serialPort.WriteLine("P");
                    Thread.Sleep(pauseTime);

                    try
                    {
                        // Formatting Input
                        temp2 = _serialPort.ReadExisting();
                        index = temp2.LastIndexOf(" ");
                        temp2 = temp2.Substring(index + 1);
                        Console.WriteLine("Weight = " + temp2);
                        weightval[1] = Convert.ToDouble(temp2);
                        start_weight = weightval[1];
                        Thread.Sleep(pauseTime);
                    }
                    catch (FormatException e)
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                        ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);
                        WriteError("Format Exception Occured - Please Check the Scales / Serial Port | Top Error -> " + e.Message);
                        fail = true;
                    }

                    if (fail == true)
                    {
                        WriteError("Failed!");
                        SendInfo(ref trans);
                        Console.WriteLine("Press [Enter] to continue...");
                        string exit = Console.ReadLine();
                        if (exit != "`")
                        {
                            Environment.Exit(0);
                        }
                    }

                    Console.WriteLine("Begin Wait for filling to pickup (2 minutes)");
                    // Allow filling to pickup
                    Thread.Sleep(120000);
                    WriteSuccess("Finished Waiting");
                    startTime = Double.Parse(DateTime.Now.ToString("fff"));

                    counter = 1;
                    while ((counter <= 9500) && (complete == false) && (fail == false))
                    {
                        Thread.Sleep(5000);
                        _serialPort.Write("P");
                        Thread.Sleep(pauseTime);

                        try
                        {
                            temp2 = _serialPort.ReadExisting();
                            temp2 = temp2.Substring(4, 6);

                            SendWeightHttp(temp2, false, (Math.Abs(double.Parse(DateTime.Now.ToString("fff")) - startTime)).ToString());
                            Console.WriteLine("Weight = " + temp2);

                            if (temp2 == "")
                            {
                                int_count++;
                                WriteError("No reading from scale 1");
                                if (int_count >= 5)
                                {
                                    // no reading from scale in 5 times, exit
                                    temp2 = _serialPort.ReadExisting();
                                    fail = true;
                                    continue;
                                }
                            }
                            else
                            {
                                weightval[counter] = Convert.ToDouble(temp2);
                            }
                            if (decrease == true)
                            {
                                if (counter > 3)
                                {
                                    if ((weightval[counter] <= weightval[counter - 1]) && (weightval[counter - 1] <= weightval[counter - 2]))
                                    {
                                        ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);
                                        if (ret < 0)
                                        {
                                            WriteError("DO_WritePort error");
                                        }

                                        WriteError("Inside Decrease = true -> counter 3 -> weightval decreasing System unable to continue filling, please start over 1");
                                        fail = true;
                                        continue;
                                    }
                                    else
                                    {
                                        start_weight = (weightval[counter] + weightval[counter - 1] + weightval[counter - 2]) / 3;

                                        if (start_weight > weightval[1])
                                        {
                                            start_weight = weightval[1];
                                        }
                                        decrease = false;
                                    }
                                }
                            }
                            else
                            {
                                if (counter >= 5)
                                {
                                    if (weightval[counter] <= weightval[counter - 1])
                                    {
                                        // counter > 5 and still weight is decreasing
                                        // tank has reached capacity and rate of inflow = rate of outflow

                                        // Stop filling relay switch
                                        ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);
                                        if (ret < 0)
                                        {
                                            WriteError("DO_WritePort error");
                                        }
                                        complete = true;
                                        continue;
                                    }
                                    else
                                    {
                                        if ((weightval[counter - 1] - start_weight) >= 0.94 * expect_weight)
                                        {
                                            complete = true;
                                            continue;
                                        }
                                    }
                                }
                            }
                            counter++;
                        }
                        catch (FormatException)
                        {
                            if (_serialPort.IsOpen)
                            {
                                _serialPort.Close();
                            }
                            ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);
                            WriteError("Format Exception - Please check the Scales / Serial Port");
                        }
                    }

                    // Turn off relay switch
                    ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);

                    Console.WriteLine("Turn Off Relay");
                    if (ret < 0)
                    {
                        WriteError("DO_WritePort error");
                    }
                    Console.WriteLine("Ending Time: " + DateTime.Now.ToString());
                    endTime = Double.Parse(DateTime.Now.ToString("fff"));
                    _serialPort.Write("P");
                    Thread.Sleep(pauseTime);

                    try
                    {
                        temp2 = _serialPort.ReadExisting();
                    }
                    catch (Exception)
                    {
                        WriteError("ex4");
                    }

                    temp2 = temp2.Substring(4, 6);
                    if (temp2 == "")
                    {
                        WriteError("No reading from scale 2");
                        final_weight = 0;
                    }
                    else
                    {
                        final_weight = Convert.ToDouble(temp2);
                        Console.WriteLine("FinalWeight: " + final_weight);
                        if (final_weight < final_weight1)
                        {
                            final_weight = final_weight1;
                        }

                        if (final_weight1 < weightval[counter - 1] - 10)
                        {
                            fail = true;
                        }
                    }

                    if (final_weight > weightval[counter - 1])
                    {
                        end_weight = final_weight;
                    }
                    else
                    {
                        end_weight = weightval[counter - 1];
                    }

                    trans.start_weight = start_weight;
                    trans.end_weight = end_weight;

                    trans.time = Math.Abs(endTime - startTime);

                    // 1Kg of Liquid Nitrogen = 1.237L of Liquid Nitrogen
                    trans.recieved_qty = (end_weight - start_weight) * 1.237;

                    Console.WriteLine("Approx Recieved:: " + trans.recieved_qty);

                    SendWeightHttp(temp2, true, trans.time.ToString());

                    if (complete == true)
                    {
                        WriteSuccess("Complete!");

                        trans.refno = GetTransRefNo();
                        GetTransInfo(ref trans);
                        InsertTransInfo(ref trans);

                        trans.refno = GetTransRefNo_New();
                        GetTransInfo_New(ref trans);
                        InsertTransInfo_New(ref trans);

                        Console.Write("Press [Enter] to continue...");
                        string exit = Console.ReadLine();
                        if (exit != "`")
                        {
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        if (fail == true)
                        {
                            SendWeightHttp(temp2, true, trans.time.ToString());
                            WriteError("Failed!");
                            trans.refno = GetTransRefNo();
                            GetTransInfo(ref trans);
                            InsertTransInfo(ref trans);

                            trans.refno = GetTransRefNo_New();
                            GetTransInfo_New(ref trans);
                            InsertTransInfo_New(ref trans);

                            Console.Write("Press [Enter] to continue...");
                            string exit = Console.ReadLine();
                            if (exit != "`")
                            {
                                Environment.Exit(0);
                            }
                        }
                    }

                    SendInfo(ref trans);
                }
            }
            finally
            {
                // Close serial port if opened successfully
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
                ret = DASK.DO_WritePort((ushort)m_dev, 0, 0);
            }
        }

        public void SendWeightHttp(string weight, bool finished, string time)
        {
            try
            {
                string data = "weight=" + weight + "&finished=" + finished.ToString() + "&time=" + time;
                WebRequest request = WebRequest.Create("https://apps.chem.tamu.edu/Nitrogen/WeightRecieved?" + data);
                request.Method = "POST";
                byte[] byteArray = Encoding.UTF8.GetBytes(data);
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;

                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Send Request Exception: " + e.Message);
            }
        }
    }
}

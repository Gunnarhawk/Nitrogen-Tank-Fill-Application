using System;
using System.IO.Ports;
using Oracle.ManagedDataAccess.Client;
using System.Threading;
using System.Net;
using System.Text;
using System.IO;

/*
  ALL SQL QUERIES HAVE BEEN MODIFIED FROM THE ORIGINAL PROGRAM, AS WELL AS ANY OTHER PRIVATE INFORMATION
*/

namespace New_NitrogenApp
{
    class MainProgram : DASK
    {
        static SerialPort _serialPort; // Main PCI
        static SerialPort _serialPortCom1 = new SerialPort("COM1", 9600); // Com1
        static SerialPort _serialPortCom2 = new SerialPort("COM2", 9600); // Com2

        static OracleConnection conn;

        static string[] userdata = { };

        static int counter = 1;
        static int ret = 0;
        static short m_dev = 0;
        static int quantity = 0;
        static int pauseTime = 1500;
        static double[] weightval = new double[9500];
        static double start_weight = 0, final_weight = 0.0, end_weight = 0.0;
        static string token;

        public struct TransInfo
        {
            public int refno;
            public string account;
            public string username;
            public string advisor;
            public double start_weight;
            public double end_weight;
            public double quantity;
            public double recieved_qty;
            public double time;
        }

        ~MainProgram()
        {
            Console.WriteLine("Destructor Called");
            try
            {
                if (_serialPort == null)
                {
                    throw new Exception("Port is null");
                }

                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                    Console.WriteLine("Serial Port Closed");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Cleaning up! Error code -> " + e);
            }

            Console.WriteLine("Closing Program...");
            Thread.Sleep(1000);
        }

        public static void InitPort(SerialPort s_port)
        {
            s_port.Handshake = Handshake.None;
            s_port.Parity = (Parity)Enum.Parse(typeof(Parity), "None");
            s_port.StopBits = (StopBits)Enum.Parse(typeof(StopBits), "One");
            s_port.DataBits = 8;
            s_port.ReadTimeout = 500;
            s_port.WriteTimeout = 500;
        }

        public static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static bool TryPort(SerialPort s_port)
        {
            int PORT_OPEN_MAX_ATTEMPTS = 10;
            bool portOpened = false;
            int portOpenAttempts = 0;

            try
            {
                // Open the port
                while (!portOpened && portOpenAttempts < PORT_OPEN_MAX_ATTEMPTS)
                {
                    try
                    {
                        s_port.Open();
                        portOpened = true;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // In order to try to fix Error: 'Access to port COM1 is denied'
                        if (portOpenAttempts++ < PORT_OPEN_MAX_ATTEMPTS)
                        {
                            Console.WriteLine("Port is being used by another process, trying again:: Attempts -> " + portOpenAttempts);
                            Thread.Sleep(100);
                            s_port.Close();
                        }
                        else
                        {
                            Console.WriteLine("Exception 1");
                            throw (ex);
                        }
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Format Exception");
                        portOpenAttempts++;
                        portOpened = false;
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Port not found -> IO Exception");
                        portOpenAttempts++;
                        portOpened = false;
                    }
                }

            }
            catch (Exception)
            {
                portOpened = false;
            }

            return portOpened;
        }

        static void Main(string[] args)
        {
            string url = "";
            if (args != null && args.Length > 0)
            {
                url = args[0];

                string[] parameters = url.Substring(url.IndexOf(":") + 1).Split('&');
                if (parameters != null && parameters.Length > 0)
                {
                    userdata[0] = parameters[0];
                    userdata[1] = parameters[1];
                    userdata[2] = parameters[2];
                    token = parameters[3];
                }
            }
            else
            {
                WriteError("Ran by Console, no URL Detected\nPlease enter password::");
                string pass = Console.ReadLine();
                if (pass != "temp_pass4")
                {
                    WriteError("Not Allowed");
                    Environment.Exit(0);
                }
            }

            if (userdata != null && userdata.Length > 0)
            {
                WriteSuccess("User Data:: " + userdata[0] + " | " + userdata[1] + " | " + userdata[2] + " | " + token);
            }
            else
            {
                WriteError("No user data found, try again!");
                return;
            }

            try
            {
                Console.WriteLine("Trying COM1");
                InitPort(_serialPortCom1);
                bool success = TryPort(_serialPortCom1);
                if (success)
                {
                    _serialPort = _serialPortCom1;
                }
                else
                {
                    WriteError("COM1 -> FAILED");
                    throw new FormatException();
                }
                WriteSuccess("COM1 -> ACTIVE");
            }
            catch (FormatException)
            {
                Console.WriteLine("Trying COM2");
                InitPort(_serialPortCom2);
                bool success = TryPort(_serialPortCom2);
                if (success)
                {
                    _serialPort = _serialPortCom2;
                }
                else
                {
                    WriteError("COM2 -> Failed");
                    WriteError("Cannot find a suitable COM port, please contact an administrator");
                    Console.WriteLine("Press [Enter] to continue...");
                    string press = Console.ReadLine();
                    if (press != "`")
                    {
                        Environment.Exit(0);
                    }
                }
                WriteSuccess("COM2 -> ACTIVE");
            }

            if (userdata[2] == "Chem")
            {
                Console.WriteLine("Chem Building Program Selected");
                ChemBuildingProgram c_program = new ChemBuildingProgram(counter, ret, m_dev, quantity, pauseTime, weightval, start_weight, final_weight, end_weight, _serialPort, userdata);
                c_program.Main_Program();
            }
            else if (userdata[2] == "Reed McDonald")
            {
                Console.WriteLine("Reed McDonald Building Program Selected");
                ReedMcDonaldProgram r_program = new ReedMcDonaldProgram(counter, ret, m_dev, quantity, pauseTime, weightval, start_weight, final_weight, end_weight, _serialPort, userdata);
                r_program.Main_Program();
            }
            else
            {
                WriteError("No Program Selected");
            }
        }

        public static int GetTransRefNo()
        {
            Connect("normal");

            string sql = "SELECT a FROM test_table WHERE rownum=1 ORDER BY a DESC";

            OracleCommand command = new OracleCommand(sql, conn);
            OracleDataReader reader = command.ExecuteReader();
            int res = 0;
            while (reader.Read())
            {
                res = Convert.ToInt32(reader["a"]);
            }
            res++;

            Disconnect();

            return res;
        }

        public static void GetTransInfo(ref TransInfo x)
        {
            Connect("normal");

            x.account = "";
            x.username = "";

            string sql = "SELECT a FROM test_table WHERE a = :a";

            OracleCommand command = new OracleCommand(sql, conn);
            command.Parameters.Add("a", OracleDbType.Varchar2).Value = userdata[1];
            OracleDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                x.username = userdata[1];
                x.account = ((string)reader["a"]);
            }

            command.Parameters.Clear();

            command.CommandText = "SELECT a FROM test_table WHERE a=:a";
            command.Parameters.Add("a", OracleDbType.Varchar2).Value = x.account;
            reader = command.ExecuteReader();
            while (reader.Read())
            {
                x.advisor = ((string)reader["a"]);
                x.quantity = Convert.ToDouble(userdata[1]);
            }

            Disconnect();
        }

        public static void InsertTransInfo(ref TransInfo x)
        {
            Connect("normal");

            string sql = "INSERT INTO test_table VALUES (:a, :b, :c, :d, :e, TO_DATE(:f, \'MM/DD/YYYY:hh:mi:ssam\'), :g, :h, :i)";

            OracleCommand command = new OracleCommand(sql, conn);
            command.Parameters.Add("a", OracleDbType.Varchar2).Value = x.refno;
            command.Parameters.Add("b", OracleDbType.Varchar2).Value = x.username;
            command.Parameters.Add("c", OracleDbType.Varchar2).Value = x.advisor;
            command.Parameters.Add("d", OracleDbType.Varchar2).Value = x.account;
            command.Parameters.Add("e", OracleDbType.Varchar2).Value = x.quantity;
            command.Parameters.Add("f", OracleDbType.Varchar2).Value = Convert.ToString(DateTime.Now);
            command.Parameters.Add("g", OracleDbType.Varchar2).Value = (x.end_weight - x.start_weight);
            command.Parameters.Add("h", OracleDbType.Varchar2).Value = x.start_weight;
            command.Parameters.Add("i", OracleDbType.Varchar2).Value = x.end_weight;
            command.ExecuteNonQuery();
            command.CommandText = "commit";
            command.ExecuteNonQuery();

            Disconnect();
        }

        public static int GetTransRefNo_New()
        {
            Connect("key");

            string sql = "SELECT a FROM test_table WHERE rownum=1 ORDER BY a DESC";

            OracleCommand command = new OracleCommand(sql, conn);
            OracleDataReader reader = command.ExecuteReader();
            int res = 0;
            while (reader.Read())
            {
                res = Convert.ToInt32(reader["a"]);
            }
            res++;

            Disconnect();

            return res;
        }

        public static void GetTransInfo_New(ref TransInfo x)
        {
            string group = string.Empty;
            x.account = "";
            x.username = "";

            Connect("key");
            string sql = "SELECT a, b FROM test_table WHERE a = \'" + userdata[1] + "\'";
            OracleCommand command = new OracleCommand(sql, conn);
            OracleDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                group = reader["a"].ToString();
            }
            Disconnect();

            Connect("web");
            sql = "SELECT a, b FROM test.test_table WHERE a = \'" + group + "\'";
            command = new OracleCommand(sql, conn);
            reader = command.ExecuteReader();
            while (reader.Read())
            {
                x.account = reader["a"].ToString();
                x.advisor = reader["b"].ToString();
            }
            Disconnect();

            x.quantity = Convert.ToDouble(userdata[0]);
            x.username = userdata[1];
        }

        public static void InsertTransInfo_New(ref TransInfo x)
        {
            Connect("key");
            string sql = "INSERT INTO empty_table_name (a, b, c, d, e, f, g, h, i, j) VALUES (:a, :b, :c, :d, TO_DATE(:e, \'MM/DD/YYYY:hh:mi:ssam\'), :f, :g, :h, :i)";
            OracleCommand command = new OracleCommand(sql, conn);
            command.Parameters.Add("a", OracleDbType.Int32).Value = x.refno;
            command.Parameters.Add("b", OracleDbType.Varchar2).Value = x.username;
            command.Parameters.Add("c", OracleDbType.Varchar2).Value = x.advisor;
            command.Parameters.Add("d", OracleDbType.Varchar2).Value = x.quantity;
            command.Parameters.Add("e", OracleDbType.Varchar2).Value = Convert.ToString(DateTime.Now);
            command.Parameters.Add("f", OracleDbType.Varchar2).Value = (x.end_weight - x.start_weight);
            command.Parameters.Add("g", OracleDbType.Varchar2).Value = x.start_weight;
            command.Parameters.Add("h", OracleDbType.Varchar2).Value = x.end_weight;
            command.Parameters.Add("i", OracleDbType.Varchar2).Value = userdata[2];
            command.ExecuteNonQuery();

            Disconnect();
        }

        public static void SendInfo(ref TransInfo x)
        {
            Console.WriteLine("Sending Request to server");
            try
            {
                string data = "RequestAmmount=" + x.quantity + "&AmmountRecieved=" + x.recieved_qty + "&Time=" + x.time + "&Token=" + token;
                WebRequest request = WebRequest.Create("https://apps.chem.tamu.edu/Nitrogen/RecieveData?" + data);
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
                Console.WriteLine("Send Request Exception:: " + e.Message);
            }
        }

        public static void Connect(string type)
        {
            // Connection strings are empty, I am not able to share this information
            string connectionString = string.Empty;
            if(type == "web")
            {
                connectionString = $"";
            }
            else if(type == "normal")
            {
                connectionString = $"";
            } else if(type == "key")
            {
                connectionString = $"";
            }

            try
            {
                // Open the connection
                conn = new OracleConnection();
                conn.ConnectionString = connectionString;
                conn.Open();
            }
            catch (Exception)
            {
                Console.WriteLine("error");
            }
        }

        public static void Disconnect()
        {
            // Close the connection
            conn.Close();
            conn.Dispose();
        }
    }
}

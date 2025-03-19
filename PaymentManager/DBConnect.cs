using System;
using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;
using System.Configuration;

namespace PaymentManager
{
    internal class DBConnect
    {
        private MySqlConnection connection;
        private string database;
        private string password;
        private string server;
        private string uid;

        //Constructor
        public DBConnect()
        {
            Initialize();
        }

        //Initialize values
        private void Initialize()
        {
            server = "localhost";
            database = ConfigurationManager.AppSettings["DB_NAME"];
            uid = ConfigurationManager.AppSettings["DB_USER"];
            password = "";
            string connectionString;
            connectionString = "SERVER=" + server + ";" + "DATABASE=" +
                               database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

            connection = new MySqlConnection(connectionString);
        }

        //open connection to database
        public bool OpenConnection()
        {
            try
            {
                connection.Open();
                return true;
            }
            catch (MySqlException ex)
            {
                switch (ex.Number)
                {
                    case 0:
                        
                        Console.WriteLine("Cannot connect Database. Please Check Configuration");
                        break;

                    case 1045:
                        
                        Console.WriteLine("Invalid username/password, please try again");
                        break;
                }

                return false;
            }
        }

        //Close connection
        public bool CloseConnection()
        {
            try
            {
                connection.Close();
                return true;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        //Insert statement
        public void Insert()
        {
            var query = "INSERT INTO tableinfo (name, age) VALUES('John Smith', '33')";

            if (OpenConnection())
            {
                var cmd = new MySqlCommand(query, connection);

                //Execute command
                cmd.ExecuteNonQuery();

                //close connection
                CloseConnection();
            }
        }

        public void InsertPayment(String session, String terminal, decimal amount, string req, string res, string res_text)
        {
            var query = "INSERT INTO payment_edc (session_id,terminal_id,amount,request_in_byte,response_in_byte,response_text,is_settled) VALUES('" + session + "','" + terminal + "','" + amount + "','" + req + "','" + res + "','" + res_text + "',1)";

            if (OpenConnection())
            {

                var cmd = new MySqlCommand(query, connection);

                try
                {
                    //Execute command
                    cmd.ExecuteNonQuery();

                }
                catch (Exception r)
                {
                    Console.WriteLine(r.ToString());
                }

                //close connection
                CloseConnection();
            }
        }

        //Update statement
        public void Update()
        {
            var query = "UPDATE tableinfo SET name='Joe', age='22' WHERE name='John Smith'";

            //Open connection
            if (OpenConnection())
            {
                //create mysql command
                var cmd = new MySqlCommand();
                //Assign the query using CommandText
                cmd.CommandText = query;
                //Assign the connection using Connection
                cmd.Connection = connection;

                //Execute query
                cmd.ExecuteNonQuery();

                //close connection
                CloseConnection();
            }
        }

        //Delete statement
        public void Delete()
        {
            var query = "DELETE FROM tableinfo WHERE name='John Smith'";

            if (OpenConnection())
            {
                var cmd = new MySqlCommand(query, connection);
                cmd.ExecuteNonQuery();
                CloseConnection();
            }
        }


        //Select statement
        public List<string>[] Select()
        {
            var query = "SELECT * FROM tableinfo";

            //Create a list to store the result
            var list = new List<string>[3];
            list[0] = new List<string>();
            list[1] = new List<string>();
            list[2] = new List<string>();

            //Open connection
            if (OpenConnection())
            {
                //Create Command
                var cmd = new MySqlCommand(query, connection);
                //Create a data reader and Execute the command
                var dataReader = cmd.ExecuteReader();

                //Read the data and store them in the list
                while (dataReader.Read())
                {
                    list[0].Add(dataReader["id"] + "");
                    list[1].Add(dataReader["name"] + "");
                    list[2].Add(dataReader["age"] + "");
                }

                //close Data Reader
                dataReader.Close();

                //close Connection
                CloseConnection();

                //return list to be displayed
                return list;
            }

            return list;
        }

        //Count statement
        public int Count()
        {
            var query = "SELECT Count(*) FROM tableinfo";
            var Count = -1;

            //Open Connection
            if (OpenConnection())
            {
                //Create Mysql Command
                var cmd = new MySqlCommand(query, connection);

                //ExecuteScalar will return one value
                Count = int.Parse(cmd.ExecuteScalar() + "");

                //close Connection
                CloseConnection();

                return Count;
            }

            return Count;
        }

        public int CountPaymentBySession(String session)
        {
            var query = "SELECT Count(*) FROM payment_edc WHERE session_id='" + session + "' ";
            var Count = -1;

            //Open Connection
            if (OpenConnection())
            {
                //Create Mysql Command
                var cmd = new MySqlCommand(query, connection);

                //ExecuteScalar will return one value
                Count = int.Parse(cmd.ExecuteScalar() + "");

                //close Connection
                CloseConnection();

                return Count;
            }

            return Count;
        }

        //Backup
        public void Backup()
        {
        }

        //Restore
        public void Restore()
        {
        }

        public List<string>[] getMerchantPrinter(string merchantKey)
        {
            var list = new List<string>[3];

            try
            {
                var query = "SELECT ip,name,type FROM outlet_printer WHERE merchant_key='" + merchantKey +
                            "' AND  status=1 AND is_deleted=0";

                //Create a list to store the result

                list[0] = new List<string>();
                list[1] = new List<string>();
                list[2] = new List<string>();

                var cmd = new MySqlCommand(query, connection);
                var dataReader = cmd.ExecuteReader();
                var dataTable = new DataTable();

                while (dataReader.Read())
                {
                    list[0].Add(dataReader["ip"] + "");
                    list[1].Add(dataReader["name"] + "");
                    list[2].Add(dataReader["type"] + "");
                }

                dataReader.Close();

                using (var da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dataTable);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                CloseConnection();
            }

            return list;
        }

        public List<string>[] getPaymentBySession(string session)
        {
            var list = new List<string>[1];

            try
            {
                var query = "SELECT response_text FROM payment_edc WHERE session_id='" + session + "' ";

                list[0] = new List<string>();

                var cmd = new MySqlCommand(query, connection);
                var dataReader = cmd.ExecuteReader();
                var dataTable = new DataTable();

                while (dataReader.Read())
                {
                    list[0].Add(dataReader["response_text"] + "");

                }


                dataReader.Close();

                using (var da = new MySqlDataAdapter(cmd))
                {
                    da.Fill(dataTable);
                }

                //MessageBox.Show(list[0].ToString());
            }
            catch (Exception r)
            {
                //MessageBox.Show(r.Message);
            }
            finally
            {
                CloseConnection();
            }

            return list;
        }
    }
}
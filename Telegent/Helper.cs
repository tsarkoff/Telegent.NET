using System;
using System.IO;
using System.Threading;
using TLSharp.Core.Network;

namespace Telehelp
{
    // types of log messages and copnsole color to display
    public enum LogType
    {
        MSG = ConsoleColor.White,
        LOG = ConsoleColor.Gray,
        WRN = ConsoleColor.DarkYellow,
        ERR = ConsoleColor.Red,
        SCC = ConsoleColor.Green
    }
    
    // types of Chat objects received from Telegram (Chat, Channel, ChatForbidden)
    public enum ChatType
    {
        CHAT_FORBIDN,
        CHAT_REGULAR,
        CHAT_CHANNEL
    }

    // types of Chat users receiving (all, active, inavtive)
    public enum UsersActivityType
    {
        USERS_ALL,
        USERS_ACTIVE,
        USERS_INACTIVE
    }

    // triple key value holder (for ChatType chatId, channelHash struct list)
    public class Triple<T1, T2, T3>
    {
        public T1 type { get; set; }
        public T2 id { get; set; }
        public T3 hash { get; set; }
        public Triple(T1 type, T2 id, T3 hash)
        {
            this.type = type;
            this.id = id;
            this.hash = hash;
        }
    }

    // generate long Id for messages, files
    public class LongRandom
    {
        public static long Get
        {
            get
            {
                long min = 100000000000000000L;
                long max = 100000000000000050L;
                Random rand = new Random();
                byte[] buf = new byte[8];
                rand.NextBytes(buf);
                long longRand = BitConverter.ToInt64(buf, 0);
                return (Math.Abs(longRand % (max - min)) + min);
            }
        }
    }

    // logging Class funcs
    public static class Logger
    {
        public static bool Msg(string msg, string[] vars = null) { WriteLog(msg, vars, LogType.MSG); return true; }
        public static bool Log(string msg, string[] vars = null) { WriteLog(msg, vars, LogType.LOG); return true; }
        public static bool Warn(string msg, string[] vars = null) { WriteLog(msg, vars, LogType.WRN); return true; }
        public static bool Err(string msg, string[] vars = null) { WriteLog(msg, vars, LogType.ERR); return true; }
        public static bool Succ(string msg, string[] vars = null) { WriteLog(msg, vars, LogType.SCC); return true; }

        public static bool WriteLog(string msg, string[] vars = null, LogType type = LogType.LOG)
        {
            Console.ForegroundColor = (ConsoleColor)type;
            string logMsg = (0 != msg.IndexOf(Environment.NewLine)) ? type.ToString().Substring(type.ToString().Length - 3) + ": " + msg : msg;
            Console.WriteLine(logMsg, vars);
            return true;
        }
        public static void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Telegent usage:");
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine("Telegent [ gm | ga | gi | am | sg | sm | sa | si | su | pg | pm | ...]");
            Console.WriteLine("\t[ \"group_name\" \"members.txt\" | \"members.txt\" \"message.txt\"] ");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("where");
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine("gm (get members)\t export members of group_name to members.txt");
            Console.WriteLine("ga (get active members)\t export active members of group_name to members.txt");
            Console.WriteLine("gi (get inactive members)\t export active members of group_name to members.txt");

            Console.WriteLine("am (add members)\t add members from members.txt to a group_name");

            Console.WriteLine("sg (send broadcast)\t public message.txt to group channel");
            Console.WriteLine("sm (send multicast)\t private message.txt to group members");
            Console.WriteLine("sa (send multicast)\t private message.txt to group active members");
            Console.WriteLine("si (send multicast)\t private message.txt to inactive group members");
            Console.WriteLine("su (send unicast)\t send private message.txt to members.txt");

            Console.WriteLine("pg (send broadcast)\t sent picture equael to sg");
            Console.WriteLine("pm (send multicast)\t sent picture equael to sm");
            Console.WriteLine("pa (send multicast)\t sent picture equael to sa");
            Console.WriteLine("pi (send multicast)\t sent picture equael to si");
            Console.WriteLine("pu (send multicast)\t sent picture equael to su");

            Console.WriteLine("group_name\t name of a group to take into processing");
            Console.WriteLine("members.txt\t file with list of members collected or to be processed");
            Console.WriteLine("message.txt\t file with plain text to be sent to members.txt");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nDescription:");
            Console.WriteLine(
                "First you have to loging at My Telegram here: https://my.telegram.orgregister,\r\n" +
                "then to get API configuration here: https://my.telegram.org/apps. App api_id\r\n" +
                "and App hash_id provided by Telegram have to be added to telegent.conf file,\r\n" +
                "located in Telegent application folder with file format:\n");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(
                "\tapi_id=123456" +
                "\n\tapi_hash=abcdef123456abcdef123456abcdef12" +
                "\n\tphone=1234567890" +
                "\n\tusername=Telegent" +
                "\n\tpassw=secret" +
                "\n\tconfig=telegent.conf" +
                "\n\tattachmentsFolder=attachments");

            Console.WriteLine("\tparameters \"username\" and \"passw\" are optional so far\n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "Members managment is based on \"membes.txt\" file while receiving them\r\n" +
                "from specified group. As well, format of this is used to read and add\r\n" +
                "members to another group:\n");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(
                "\tId=123456;Phone=71234567890;First=Alex;Last=Smith;Username=alexsmith");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nNote:" +
                "Agent is able to send documents together with \"message.txt\" text message\r\n," +
                "if there are files (attachments) are found in a folder \"attachments\" located\r\n" +
                "in the same folder where Agent is located. Be aware, all files are present in\r\n" +
                "this folder will be sent to user together (before) text message.\n\n");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Examples:\r\n\r\n" +
                "telegent gm \"My Group\" \"C:\\Telegent\\>members.txt\"");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" - collect currect members from MyGroup to memebers.txt file\r\n");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("telegent am \"My Group\" \"C:\\Telegent\\>new_members.txt\"");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" - collect members from new_memebers.txt file and add them to MyGroup\r\n");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("telegent sg \"My Group\" \"C:\\Telegent\\>message.txt\"");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" - send global message from message.txt file to MyGroup channel\r\n");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("telegent su \"C:\\Telegent\\>some_members.txt\" C:\\Telegent\\>message.txt");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" - send text of message.txt to members from some_memebers.txt file\r\n");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("telegent pa \"My Group\" C:\\Telegent\\>message.txt");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(" - send all pictures from /attacments folder to active memebrs of MyGroup\r\n");
        }

        public static void cli()
        {
            Console.Clear();
            Console.WriteLine("\r\nEntering Telegent intercative command line...");
            Console.WriteLine("For exit enter \"q\".");
            Console.WriteLine("Enter a command");
            Console.WriteLine("[ gm | ga | gi | am | sg | sm | sa | si | su | pg | pm | pa | pi | pu ]");
            Console.Write("/> ");
            string cmd = Console.ReadLine();

            switch (cmd)
            {
                case "q":
                    Console.Write("\r\nExit Telegent intercative command line.");
                    break;
            }
        }
    }

    // config file and Telegram API login data class
    public class TLAPIData
    {
        public static void SetConsole()
        {
            TextWriter tmp = Console.Out;
            StreamWriter sw = new StreamWriter(new MemoryStream());
            Console.SetOut(sw);
            Console.BufferHeight = 5100;
            Console.WindowHeight = Console.LargestWindowHeight - 10;
            Console.WindowWidth = 100;
            Console.WindowTop = 0;
            Console.SetOut(tmp);
            sw.Close();
        }

        private static void SetAll(int _apiId = 123456, string _apiHash = "abcdef123456abcdef123456abcdef12", string _phoneNo = "71234567890", string _nick = "Telegent", string _passw = "secret")
        {
            apiId = _apiId; apiHash = _apiHash; phoneNo = _phoneNo; nick = _nick; passw = _passw; configFile = @"telegent.conf"; attachmentsFolder = @"attachments";
        }
        public static int apiId { get; private set; }
        public static string apiHash { get; private set; }
        public static string phoneNo { get; private set; }
        public static string nick { get; private set; }
        public static string passw { get; private set; }
        public static string configFile { get; private set; }
        public static string attachmentsFolder { get; private set; }

        public static bool ReadConfig()
        {
            SetAll();
            string[] loginData = null;
            try { loginData = File.ReadAllLines(configFile); } catch (Exception ex) { Logger.Err(ex.Message); }

            if (null == loginData || 0 == loginData.Length)
            {
                string[] ld = new string[7] { "api_id=" + apiId.ToString(), "api_hash=" + apiHash, "phone=" + phoneNo, "username=" + nick, "passw=" + passw, "config=" + configFile, "attachmentsFolder=" + attachmentsFolder };
                File.WriteAllLines(configFile, ld);
                loginData = File.ReadAllLines(configFile);
            }

            string[] keyval = new string[2];
            foreach (string str in loginData)
            {
                keyval = str.Split('=');
                if ("api_id" == keyval[0]) apiId = int.Parse(keyval[1]);
                else if ("api_hash" == keyval[0]) apiHash = keyval[1];
                else if ("phone" == keyval[0]) phoneNo = keyval[1];
                else if ("username" == keyval[0]) nick = keyval[1];
                else if ("passw" == keyval[0]) passw = keyval[1];
                else if ("config" == keyval[0]) passw = keyval[1];
                else if ("attachmentsFolder" == keyval[0]) passw = keyval[1];
                else return !Logger.Err("Wrong configuration file data." +
                    "Please use next format" +
                    "\n\tapi_id=123456" +
                    "\n\tapi_hash=abcdef123456abcdef123456abcdef12" +
                    "\n\tphone=71234567890" +
                    "\n\tusername=telegent" +
                    "\n\tpassw=secret" +
                    "\n\tconfig=telegent.conf" +
                    "\n\tattachmentsFolder=attachments");
            }

            return true;
        }
    }

    public static class NoFlood
    {
        public static void CheckFlood(Exception ex)
        {
            if (typeof(FloodException) == ex.GetType())
            {
                TimeSpan timeToWait = (ex as FloodException).TimeToWait;
                Thread.Sleep((int)(timeToWait.Milliseconds * 1.2));
                Logger.WriteLog("==> Telegram flood detected, sleeping for {0} ms.", new string[1] { timeToWait.ToString() }, (LogType)ConsoleColor.DarkGray);
            }
        }
    }
}

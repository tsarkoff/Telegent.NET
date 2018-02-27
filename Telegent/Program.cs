using System;
using System.IO;
using System.Linq;

using TLSharp.Core;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TeleSharp.TL.Contacts;
using TeleSharp.TL.Channels;
using TLSharp.Core.Utils;

using Telehelp;
using System.Threading;

namespace Telegent
{
    class Program
    {

        // Telegram Client from TLSharp API
        private static TelegramClient client = null;

        // Main
        static void Main(string[] args)
        {
            bool isException = false;
            bool isCommandDone = false;

            try
            {
                TLAPIData.SetConsole();
                Logger.Msg("TL Agent started.\n");

                TLAPIData.ReadConfig();
                client = new TelegramClient(TLAPIData.apiId, TLAPIData.apiHash);

                if (3 == args.Length)
                {
                        TLClientOpen();

                        switch (args[0])
                        {
                            case "cli":
                                Logger.PrintHelp();
                                Logger.cli();
                                break;
                            case "gm":
                                Logger.Msg("Get members = group:\"{0}\" out:{1}", new string[] { args[1], args[2] });
                                GetGroupMembers(args[1], args[2]);
                                break;
                            case "ga":
                                Logger.Msg("Get active members = group:\"{0}\" out:{1}", new string[] { args[1], args[2] });
                                GetGroupActiveMembers(args[1], true, args[2]);
                                break;
                            case "gi":
                                Logger.Msg("Get inactive members = group:\"{0}\" out:{1}", new string[] { args[1], args[2] });
                                GetGroupActiveMembers(args[1], false, args[2]);
                                break;
                            case "am":
                                Logger.Msg("Add members = group:\"{0}\" in:{1}", new string[] { args[1], args[2] });
                                AddMembersToGroup(args[1], args[2]);
                                break;
                            case "sg":
                                Logger.Msg("Send broadcast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupMessage(args[1], args[2], false);
                                break;
                            case "sm":
                                Logger.Msg("Send multicast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupMessage(args[1], args[2], true);
                                break;
                            case "sa":
                                Logger.Msg("Send active multicast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupMessageActiveMembers(args[1], args[2], true);
                                break;
                            case "si":
                                Logger.Msg("Send inactive multicast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupMessageActiveMembers(args[1], args[2], false);
                                break;
                            case "su":
                                Logger.Msg("Send unicast = members.txt:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendUserListMessage(args[1], args[2]);
                                break;
                            case "pg":
                                Logger.Msg("Send pictures broadcast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupPicture(args[1], false, args[2]);
                                break;
                            case "pm":
                                Logger.Msg("Send pictures multicast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupPicture(args[1], true, args[2]);
                                break;
                            case "pa":
                                Logger.Msg("Send pictures multicast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupPictureActiveMembers(args[1], true, args[2]);
                                break;
                            case "pi":
                                Logger.Msg("Send pictures multicast = group:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendGroupPictureActiveMembers(args[1], false, args[2]);
                                break;
                            case "pu":
                                Logger.Msg("Send pictures unicast = members.txt:\"{0}\" message.txt:{1}", new string[] { args[1], args[2] });
                                SendUserListPicture(args[1], args[2]);
                                break;
                            default:
                                Logger.PrintHelp();
                                break;
                        } 

                    isCommandDone = true;
                }

            } catch (Exception ex)
            {
                NoFlood.CheckFlood(ex);
                Logger.Err("Telegent stopped with error: " + ex.Message);
                Logger.Succ("Run program w/o parameters to read help");
                Console.ForegroundColor = ConsoleColor.White;
                isException = true;
            }

            if (!isException && !isCommandDone)
                Logger.PrintHelp();

            Logger.Msg("Press any key to exit...");
            Console.ReadKey();
        }


        //**********************************************************//
        //**                 Below is Members routines            **//
        //**********************************************************//

        // Return members list from specified group Name 
        private static TLVector<TLAbsUser> GetGroupMembers(string gname, string ufile = null)
        {
            long hash = 0;
            int gid = GetGroupIdByName(gname, out hash);
            TeleSharp.TL.Messages.TLChatFull ch = GetGroupFullById(gid, hash);

            if (0 == hash && MembersLogAndSave(ch.Users, ufile, gname)) return ch.Users;

            int offset = 0;
            TLChannelParticipants ps = null;
            TLVector<TLAbsUser> users = new TLVector<TLAbsUser>();

            do
            {
                ps = client.GetChannelParticipants(gid, hash, offset, 5000).GetAwaiter().GetResult();
                foreach (TLUser u in ps.Users)
                    users.Add(u);
                offset += ps.Users.Count;
            } while (users.Count < ps.Count && 0 != ps.Users.Count);

            MembersLogAndSave(users, ufile, gname);
            return users;
        }
        // Return active / inactive members list from specified group Name 
        private static TLVector<TLAbsUser> GetGroupActiveMembers(string gname, bool active, string ufile = null)
        {
            long hash = 0;
            int gid = GetGroupIdByName(gname, out hash);
            TLVector<TLAbsUser> au = new TLVector<TLAbsUser>();
            TLVector<TLAbsUser> users = GetGroupMembers(gname);

            foreach (TLUser u in users)
            {
                bool a = IsMemberActive(u, gid);
                if (active && a || !active && !a)
                    au.Add(u);
            }

            MembersLogAndSave(au, ufile, gname);
            return au;
        }

        // Verifies if member sent posts in a Group
        // TODO: Check activity for Chat also (!!!)
        private static bool IsMemberActive(TLUser user, int gid = 0)
        {
            try
            {
                var msg = client.GetUserHistoryAsync(user.Id).GetAwaiter().GetResult();
                if (typeof(TLMessagesSlice) == msg.GetType())
                {
                    msg = (TLMessagesSlice)msg;
                    if (null == msg || 0 == (msg as TLMessagesSlice).Messages.Count) return false;
                } else if (typeof(TLMessages) == msg.GetType())
                {
                    msg = (TLMessages)msg;
                    if (null == msg || 0 == (msg as TLMessages).Messages.Count) return false;
                }
                else return false;
               
                Logger.Succ("Active user:{0} found.", new string[] { userTitle(user) });
            } catch (Exception ex)
            {
                return !Logger.Warn("Active user:{0} check failed: {1}.", new string[] { userTitle(user), ex.Message });
            }

            return true;
        }

        // Add memebers from members.txt file to specified group Name
        private static bool AddMembersToGroup(string gname, string ufile)
        {
            long hash = 0;
            int count = 0;
            TLVector<TLAbsUser> users = GetMembersFromFile(ufile);
            Logger.Msg("Total Members: {0} in Group:\"{1}\".", new string[] { users.Count.ToString(), gname });

            int gid = GetGroupIdByName(gname, out hash);
            foreach (TLUser u in users)
                count += AddMemberToGroup(u, gid, hash);

            Logger.Succ("Processed Members: {0} to Group:\"{1}\".", new string[] { count.ToString(), gname });
            return true;
        }

        // Add 1 memeber to specified group Name from members.txt file
        private static int AddMemberToGroup(TLUser user, int gid, long hash)
        {
            int ret = 0;
            TLAbsUpdates up = null;
            try
            {
                user.Id = GetMemberIdByNickname(user);
                if (0 == hash)
                    up = client.AddChatUserAsync(user.Id, gid).GetAwaiter().GetResult();
                else
                    up = client.InviteToChannelAsync(user.Id, gid, hash).GetAwaiter().GetResult();

                if (0 != ((TLUpdates)up).Updates.Count)
                {
                    Logger.Succ("Adding user:{0} to group successful.", new string[] { userTitle(user) });
                    ret++;
                } else
                    Logger.Warn("Adding user:{0} to group:{1} failed (USER_ALREADY_PARTICIPANT).", new string[] { userTitle(user), gid.ToString() });
                    
            } catch (Exception ex)
            {
                Logger.Warn("Adding user:{0} to group failed: {1}", new string[] { userTitle(user), ex.Message });
            }

            return ret;
        }

        // Find member in Telegram by Nick name (in case if Id  Phone is unknown, etc)
        private static int GetMemberIdByNickname(TLUser user)
        {
            TLResolvedPeer peer = null;
            if (0 == user.Id && !String.IsNullOrWhiteSpace(user.Username))
                peer = (client.ResolveUserNameAsync(user.Username).GetAwaiter().GetResult() as TLResolvedPeer);

            return (null == peer) ? user.Id : (peer.Users[0] as TLUser).Id;
        }

        //**********************************************************//
        //**                 Below is Messages routines           **//
        //**********************************************************//

        // (bool !personal) Send single message to a specified group
        // or alone personal messages to members of this Group (from message.txt file)
        private static bool SendGroupMessage(string gname, string mfile, bool personal)
        {
            if (personal)
            {
                // MULTICAST ALL = no attachments, to group, from message.txt, no members.txt, to all, no active, no picture
                SendItem(false, gname, mfile, null, true, false, null);
            } else
            {
                long hash = 0;
                string message = null;
                int gid = GetGroupIdByName(gname, out hash);
                if (null == (message = GetTextFileOneLine(mfile))) return false;
                client.SendMessageAsync(newInput(gid, hash), message).GetAwaiter().GetResult();
                Logger.Succ("Group BROADCAST message sent to group:{0}", new string[] { gname });
            }

            return true;
        }

        // Send to active / inactive memebers of a group Name - a personal message from message.txt file
        private static bool SendGroupMessageActiveMembers(string gname, string mfile, bool active)
        {
            // MULTICAST ACTIVE = no attachments, for group, from message.txt, no members.txt, noall, active, no picture
            return SendItem(false, gname, mfile, null, false, active, null);
        }

        // send unicast message - take user list from members.txt file and send message to them personally
        private static bool SendUserListMessage(string ufile, string mfile)
        {
            // MULTICAST ACTIVE = no attachments, for group, from message.txt, no members.txt, noall, active, no picture
            return SendItem(false, null, mfile, ufile, false, false, null);
        }

        //**********************************************************//
        //**                 Below is Group routines              **//
        //**********************************************************//

        // Find a specified group Name and return ots ID
        private static int GetGroupIdByName(string gname, out long hash)
        {
            hash = 0;
            TLAbsChat ch = null;
            TLDialogs dlgs = (TLDialogs)client.GetUserDialogsAsync().GetAwaiter().GetResult();

            GroupsLogAndSave(dlgs.Chats, @"groups.txt", gname);

            ch = dlgs.Chats
                .Where(c => c.GetType() == typeof(TLChannel))
                .Cast<TLChannel>()
                .FirstOrDefault(c => c.Title == gname);

            if (null != ch)
            {
                hash = (long)((TLChannel)ch).AccessHash;
                return ((TLChannel)ch).Id;
            }

            ch = dlgs.Chats
                .Where(c => c.GetType() == typeof(TLChat))
                .Cast<TLChat>()
                .FirstOrDefault(c => c.Title == gname);

            return (null != ch) ? ((TLChat)ch).Id : 0;
        }

        // Get extended group info (yo collect chat users, channel participants, access hash)
        private static TeleSharp.TL.Messages.TLChatFull GetGroupFullById(int id, long hash)
        {
            TeleSharp.TL.Messages.TLChatFull ch = null;

            if (0 == hash)
                ch = client.GetFullChatAsync(id).GetAwaiter().GetResult();
            if (null != ch) return ch;

            return client.GetFullChannelAsync(id, hash).GetAwaiter().GetResult();
        }

        // verify if currect API ID & HASH are linked to a Agent, that has Admin or Creator access to Chat; also
        // verify if chat specified is Super Group or not, to check if Message History is available from such a chat
        private static bool CheckGroupPermissions(TLChat chat, bool accessWrite = false, bool checkHistory = false)
        {
            if (null != chat && null == chat.MigratedTo)
            {
                Logger.Warn("Trivial Group:\"{0}\" chats history not available", new string[] { chat.Title });
                if (checkHistory)
                    return false;
            }

            if (null != chat && !chat.Admin && !chat.Creator)
            {
                Logger.Warn("TL Agent is not Creator or Admin of Group:\"{0}\"", new string[] { chat.Title });
                if (accessWrite)
                    return false;
            }

            return true;
        }

        //************************************************************//
        //**                 Below is File routines                 **//
        //**  Full path to files:                                   **//
        //**    gfile - list of groups (groups.txt)                 **//
        //**    ufile - list of members (members.txt)               **//
        //**    mfile - text file with message text (message.txt)   **//
        //**    pfile - path to foledr with pictures (attachments)  **//
        //**    dfile - path to foledr with documents (attachmenst) **//
        //**    TLAPIDAta.configFile - path to telegent.cong        **//
        //************************************************************//

        // Sent pfile image to a Group Name = single Group message or personal to each group member (with caption from mfile message.txt file)
        private static void SendGroupPicture(string gname, bool personal, string mfile = null, string pfile = null)
        {
            if (personal)
                // MULTICAST ALL = with attachments, to group, message.txt, no members.txt, toall, notactive, folder
                SendItem(true, gname, mfile, null, true, false, pfile);
            else
            {
                long hash = 0;
                int gid = GetGroupIdByName(gname, out hash);

                foreach (string path in GetPicturesPaths(pfile))
                {
                    client.SendUploadedPhoto(newInput(gid, hash), newInputFile(path), GetTextFileOneLine(mfile)).GetAwaiter().GetResult();
                    Logger.Succ("Picture BROADCAST sent to group:{0}.", new string[] { gname });
                }
            }
        }

        // Send picture personally to in/avtive users for a Group specified
        private static void SendGroupPictureActiveMembers(string gname, bool active, string mfile = null, string pfile = null)
        {
            // MULTICAST IN/ACTIVE = with attachments, to group, message.txt, no members.txt, noall, active, folder
            SendItem(true, gname, mfile, null, false, active, pfile);
        }

        // Send picture to a users are taken from members.txt file (or command line file ponted)
        private static void SendUserListPicture(string ufile, string mfile, string pfile = null)
        {
            // UNICAST = with attachments, no group, message.txt, members.txt, noall, notactive, folder
            SendItem(true, null, mfile, ufile, false, false, pfile);
        }

        // Send ITEM (file / message) UNICAST / MULTICAST to users (from group: in/active or members.txt file)
        private static bool SendItem(bool attachments, string gname, string mfile, string ufile, bool toall, bool active, string pfile = null)
        {
            string sendType = null;
            string message = GetTextFileOneLine(mfile);
            TLVector<TLAbsUser> users = GetActualGroupmembersToSend(attachments, gname, toall, active, ufile, out sendType);

            if (!attachments)
                SendItemItemCycle(users, attachments, message, null, sendType);
            else
                foreach (string path in GetPicturesPaths(pfile))
                    SendItemItemCycle(users, attachments, message, path, sendType);

            return true;
        }

        // Send ITEM (file / message) cycle for users determinated by source (directly to single group, to group members personally, to in/ active members, members.txt)
        private static void SendItemItemCycle(TLVector<TLAbsUser> users, bool attachments, string message, string path, string sendType, string pfile = null)
        {
            foreach (TLUser u in users)
                try
                {
                    u.Id = GetMemberIdByNickname(u);
                    if (attachments)
                        client.SendUploadedPhoto(newInput(u.Id, -1), newInputFile(path), message).GetAwaiter().GetResult();
                    else
                        client.SendMessageAsync(newInput(u.Id, -1), message).GetAwaiter().GetResult();

                    Logger.Succ(sendType + "sent directly to user:{0}", new string[] { userTitle(u) });

                }
                catch (Exception ex)
                {
                    Logger.Warn(sendType + "sent to user:{0} failed:{1}.", new string[] { userTitle(u), ex.Message });
                }
        }

        // Get Members List by conditions (group, membets.txt, toall, in/active)
        private static TLVector<TLAbsUser> GetActualGroupmembersToSend(bool attachments, string gname, bool toall, bool active, string ufile, out string sendType)
        {
            sendType = null;
            TLVector<TLAbsUser> users = null;

            if (!String.IsNullOrWhiteSpace(gname))
            {
                if (toall)
                {
                    users = GetGroupMembers(gname);
                    sendType = (attachments ? "File" : "Message") + " MULTICAST ALL ";
                }
                else
                {
                    users = GetGroupActiveMembers(gname, active);
                    sendType = (attachments ? "File" : "Message") + " MULTICAST " + (active ? "ACTIVE " : "INACTIVE ");
                }
            }
            else if (!String.IsNullOrWhiteSpace(ufile))
            {
                users = GetMembersFromFile(ufile);
                sendType = (attachments ? "File" : "Message") + " UNICAST ";
            }

            return users;
        }

        // get list of strings with paths to pictures files in folder pfile
        private static TLVector<string> GetPicturesPaths(string pfile = null)
        {
            string folder = null;
            if (null != pfile && Directory.Exists(pfile))
                folder = pfile;
            else
                folder = TLAPIData.attachmentsFolder;

            string[] paths = null;
            TLVector<string> filePaths = new TLVector<string>();
            string[] searchPattern = new string[] { "*.jpg", "*.jpeg", "*.gif", "*.png", "*.bmp" };

            foreach (string pattenr in searchPattern)
            {
                paths = Directory.GetFiles(folder, pattenr, SearchOption.TopDirectoryOnly);
                foreach (string path in paths)
                    filePaths.Add(path);
            }

            return filePaths;
        }
        
        // Get members list from a members.txt file (yo use for spam or assignment to an another Group)
        private static TLVector<TLAbsUser> GetMembersFromFile(string ufile)
        {
            TLVector<TLAbsUser> users = new TLVector<TLAbsUser>();
            string[] members = File.ReadAllLines(ufile);

            foreach (string m in members)
            {
                if (m == members[0]) continue;
                TLUser user = null;
                string[] props = m.Split(';');
                if (!String.IsNullOrWhiteSpace(m) && 5 == props.Length)
                {
                    int userId = 0;
                    user = new TLUser();
                    foreach (string p in props)
                    {
                        string[] keyval = p.Split('=');
                        if ("Id" == keyval[0]) user.Id = (int.TryParse(keyval[1], out userId)) ? userId : 0;
                        else if ("Phone" == keyval[0]) user.Phone = keyval[1];
                        else if ("First" == keyval[0]) user.FirstName = keyval[1];
                        else if ("Last" == keyval[0]) user.LastName = keyval[1];
                        else if ("Nick" == keyval[0]) user.Username = keyval[1];
                    }
                }

                if (null != user)
                {
                    user.Id = GetMemberIdByNickname(user);
                    users.Add(user);
                }
            }

            MembersLogAndSave(users, ufile, "READ MEMEBERS FILE");
            return users;
        }

        // If Text Message File consists of number of lines = concatenate them to comply TeleAPI
        private static string GetTextFileOneLine(string fname)
        {
            string msg = null;
            string[] lines = File.ReadAllLines(fname);
            if (null == lines || 0 == lines.Length) return null;
            foreach (string s in lines) msg += (s + Environment.NewLine);
            return msg;
        }

        //**********************************************************//
        //**             Below is Telegram Auth routines          **//
        //**********************************************************//

        // Open connection to Telegram API using TAPIdata (from telegent.conf)
        private static bool TLClientOpen()
        {
            Logger.Log("Connecting to Telegram...");
            client.ConnectAsync().GetAwaiter().GetResult();

            Logger.Log("Telegram Сonnected.");
            if (client.IsUserAuthorized()) return Logger.Log("Telegram Authorized.\n");
            if (!IsAgentPhoneRegistered(TLAPIData.phoneNo)) return false;

            string hash;
            int intAuthCode;
            if (!RequestAuthCode(TLAPIData.phoneNo, out intAuthCode, out hash)) return false;
            if (!Authorize(TLAPIData.phoneNo, hash, intAuthCode.ToString())) return false;

            return true;
        }

        // verify if API User Phone (actually - any phone specified) is registered in Telegram Service
        private static bool IsAgentPhoneRegistered(string phone)
        {
            try
            {
                client.IsPhoneRegisteredAsync(phone).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                NoFlood.CheckFlood(ex);
                return !Logger.Err("Telegram Phone:{0} is not registered:" + ex.Message, new string[] { phone });
            }

            return Logger.Log("Telegram Phone:{0} registered.", new string[] { phone });
        }

        // Request Auth Code from Telegram because access to Telegram API has not been provided yet (or Access Timeout appeared)        
        private static bool RequestAuthCode(string phone, out int intAuthCode, out string hash)
        {
            hash = "";
            intAuthCode = 0;

            try
            {
                hash = client.SendCodeRequestAsync(phone).GetAwaiter().GetResult();
                if (String.IsNullOrWhiteSpace(hash))
                    return !Logger.Err("Telegram Auth Hash receiving failed.");
            }
            catch (Exception ex)
            {
                NoFlood.CheckFlood(ex);
                return !Logger.Err("Telegram Auth Hash receiving failed: " + ex.Message);
            }

            Logger.Log("Telegram Auth Hash received:{0}", new string[] { hash });

            string strAuthCode = "";
            Logger.Warn("Auth Code required, please check your Telegram and enter 5 digits: ");

            do
            {
                strAuthCode = Console.ReadLine();
            } while (String.IsNullOrWhiteSpace(strAuthCode) || !int.TryParse(strAuthCode, out intAuthCode));

            return true;
        }

        // Rerform authorizing process using Agent phone and early received Auth Hash and Code
        private static bool Authorize(string phone, string hash, string authCode)
        {
            Logger.Log("Authorizing on Telegram...");
            try
            {
                TLUser myself = null;
                if (null == (myself = client.MakeAuthAsync(TLAPIData.phoneNo, hash, authCode).GetAwaiter().GetResult()))
                    return !Logger.Err("Authorization failed.");
                Logger.Log("Telegram Authorized:\"{0}\".\n", new string[] { myself.FirstName });
            }
            catch (CloudPasswordNeededException ex)
            {
                NoFlood.CheckFlood(ex);
                return !Logger.Err("Telegram Cloud Passord authorization required:" + ex.Message);
            }
            catch (InvalidPhoneCodeException ex)
            {
                NoFlood.CheckFlood(ex);
                return !Logger.Err("Telegram Auth Code is invalid:" + ex.Message);
            }

            return true;
        }

        //**********************************************************//
        //**             Below is Helpers routines                **//
        //**********************************************************//

        // create new TLInput to use in methods, where Input is various (e.g. send to Chat & Channel inputs peer);
        private static TLAbsInputPeer newInput(int id, long hash)
        {
            if (-1 == hash)
                return new TLInputPeerUser() { UserId = id };
            else if (0 == hash)
                return new TLInputPeerChat() { ChatId = id };
            else
                return new TLInputPeerChannel() { ChannelId = id, AccessHash = hash };
        }

        // create input file 
        private static TLInputFile newInputFile(string path)
        {
            InputFile inf = UploadHelper.UploadFile(client, path, new StreamReader(path));
            return new TLInputFile() { Id = inf.Id, Name = inf.Name, Parts = inf.Parts, Md5Checksum = inf.Md5Checksum };
        }

        private static bool GroupsLogAndSave(TLVector<TLAbsChat> chats, string gfile = null, string source = null)
        {
            Logger.Log(Environment.NewLine + "Groups is being processed:" + chats.Count.ToString());
            string id = "", type = "", title = "", groups = "";

            foreach (TLAbsChat ch in chats)
            {
                if (typeof(TLChat) == ch.GetType())
                {
                    id = ((TLChat)ch).Id.ToString();
                    type = "REGULAR";
                    title = ((TLChat)ch).Title;
                } else if (typeof(TLChannel) == ch.GetType())
                {
                    id = ((TLChannel)ch).Id.ToString();
                    type = "CHANNEL";
                    title = ((TLChannel)ch).Title;
                }

                
                groups = CollectLogLines(groups, "Id:" + id + " Type:" + type + " Title: " + title, source, title);
            }

            Logger.Succ("Groups processed:" + chats.Count.ToString());
            return SaveLogToFile(gfile, groups, "Groups", source);
        }

        private static bool MembersLogAndSave(TLVector<TLAbsUser> users, string ufile = null, string source = null)
        {
            Logger.Log(Environment.NewLine + "Members is being processed:" + users.Count.ToString());
            string members = "";

            foreach (TLUser u in users)
                members = CollectLogLines(members, "Id=" + u.Id + ";Phone=" + u.Phone + ";First=" + u.FirstName + ";Last=" + u.LastName + ";Nick=" + u.Username);

            Logger.Succ("Members processed:" + users.Count.ToString());
            return SaveLogToFile(ufile, members, "Members", source);
        }

        private static string CollectLogLines(string prev, string curr, string source = null, string title = null)
        {
            if (null != source && source == title) Logger.Succ(curr); else Logger.Warn(curr);
            return prev + curr + Environment.NewLine;
        }

        private static bool SaveLogToFile(string file, string lines, string entity, string source)
        {
            if (null != file)
            {
                File.WriteAllLines(file, lines
                    .Insert(0, "[Title: " + entity + " | Source: " + source + " | Count: " + (lines.Split(Environment.NewLine.ToCharArray()).Count()-1).ToString() + "]" + Environment.NewLine)
                    .Split(Environment.NewLine.ToCharArray()));

                Logger.Succ(entity + " saved in file: " + file + Environment.NewLine);
            }

            return true;
        }

        private static string userTitle(TLUser user)
        {
            string title = null;
            if (!String.IsNullOrWhiteSpace(user.Username))
                title = user.Username;
            else if (!String.IsNullOrWhiteSpace(user.LastName))
                title = user.LastName;
            else if (!String.IsNullOrWhiteSpace(user.FirstName))
                title = user.FirstName;
            else title = user.Id.ToString();
            return "\"" + title + "\"";
        }

    }
}


using System;
using System.IO;
using System.Text;
using FileSysTemp.FSBase;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileSysConsole
{
    public class Execute
    {
        public MemoryUser sys_current_user = new MemoryUser(0, 0);//当前登录用户，登录写好要后修改current_user！！！！！！！！！TODO
        public SuperBlock sys_sb = new SuperBlock();//超级块
        public iNodeTT sys_inode_tt = new iNodeTT();
        const uint MAX_USERNUM = 10; //内存中允许的最大用户数（同时在线）
        uint cur_usernum = 0;        //当前内存中驻留的用户数量
        /// <summary>
        /// 回收站文件地址映射（用于还原）List<Dictionary<inode_id, fore_addr_id>>
        /// </summary>
        Dictionary<uint, uint> recyclebinMap = new Dictionary<uint, uint>();

        /// <summary>
        /// 输出所有i节点表
        /// </summary>
        public void OutputTT()
        {
            for (int i = 0; i < 128; i++)
            {
                Console.Write(i);
                Console.Write(":");
                for (int j = 0; sys_inode_tt.tt[i] != null && j < sys_inode_tt.tt[i].di_table.Count(); j++)
                {
                    Console.Write(sys_inode_tt.tt[i].di_table[j].id);
                }
                Console.WriteLine("");
            }
        }
        /// <summary>
        /// 用户登录文件系统
        /// </summary>
        /// <param name="uid">用户id</param>
        /// <param name="password">用户密码</param>
        /// <returns>登录是否成功</returns>
        public bool LoginSys(uint uid, string password)
        {
            List<User> users = LoadUsersInfofromDisk();
            bool isExist = false;
            User curUser = new User();
            foreach (User user in users)
            {
                if (user.uid == uid)
                {
                    isExist = true;
                    curUser.uid = user.uid;
                    curUser.password = user.password;
                    break;
                }
            }
            if (!isExist)
            {
                //用户不存在
                Console.WriteLine("This account is not available, please check whether user " + uid.ToString() + " exists or not!");
                return false;
            }
            if (curUser.password == password)
            {
                //密码输入正确
                if (cur_usernum < MAX_USERNUM)
                {
                    //内存中同时在线用户数少于最大用户数限制，用户可以正常登录
                    MemoryUser user = new MemoryUser(curUser.uid, curUser.current_folder);
                }
                else
                {
                    Console.WriteLine("Too much users in the system, waited to login!");
                    return false;
                }
            }
            else
            {
                //密码输入错误
                Console.WriteLine("Incorrect password, Login Failure!");
                return false;
            }
            //正常退出，用户登录成功！
            return true;
        }

        /// <summary>
        /// 退出文件系统
        /// </summary>
        /// <param name="uid">用户id</param>
        /// <returns>退出成功与否</returns>
        public bool LogoutSys()
        {
            bool issucceed = StoreUserInfotoDisk(sys_current_user.uid, sys_current_user.current_folder);
            sys_current_user.Destructor(); //释放资源
            UpdateDiskSFi();
            Console.WriteLine("You have been logout successfully!");
            return issucceed;
        }
        //用户登录，暂时为了测程序好测，这里要改！！！！！！！！TODO
        public bool Login()
        {
            //TODO:用户登录模块，登录成功则修改current_user并返回true，否则返回false
            return true;
        }
        /// <summary>
        /// 将用户信息写回到磁盘
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="curfolder"></param>
        /// <returns></returns>
        public bool StoreUserInfotoDisk(uint uid, uint curfolder)
        {
            List<User> userlist = LoadUsersInfofromDisk();
            for(int i=0;i<userlist.Count();i++)
            {
                if(uid==userlist[i].uid)
                {
                    //修改当前用户的当前工作文件夹
                    userlist[i].current_folder = curfolder;
                    //更新密码
                    userlist[i].password = sys_current_user.newpassword;
                    //写回磁盘
                    FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    BinaryFormatter binFormat = new BinaryFormatter();
                    fs.Position = SuperBlock.USER_DISK_START * SuperBlock.BLOCK_SIZE;
                    binFormat.Serialize(fs, userlist);
                    fs.Close();
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 从磁盘用户区加载所有用户信息到内存
        /// </summary>
        /// <returns></returns>
        public List<User> LoadUsersInfofromDisk()
        {
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            fs.Position = SuperBlock.USER_DISK_START * SuperBlock.BLOCK_SIZE;
            List<User> userslist = (List<User>)binFormat.Deserialize(fs);
            fs.Close();
            return userslist;
        }
        /// <summary>
        /// 启动文件系统，读取超级块和i节点
        /// </summary>
        /// <returns></returns>
        public bool Start()
        {
            //1，登录
            if (Login() == true)
            {
                //2，读取必要块区到内存
                FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                BinaryFormatter binf = new BinaryFormatter();
                //读取超级块
                fs.Position = SuperBlock.SB_DISK_START * SuperBlock.BLOCK_SIZE;
                sys_sb = (SuperBlock)binf.Deserialize(fs);
                //读取i节点
                fs.Position = SuperBlock.iNODE_DISK_START * SuperBlock.BLOCK_SIZE;
                sys_inode_tt = (iNodeTT)binf.Deserialize(fs);
                fs.Close();
                //3，校验所读数据
                if (sys_sb.check_byte == 707197)
                {
                    Console.WriteLine("Boot FileSystem Successfully!");
                    return true;
                }
                else
                {
                    Console.WriteLine("Boot FileSystem Failed: Check Failed!");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Boot FileSystem Failed: Login Failed!");
                return false;
            }
        }
        /// <summary>
        /// 更新磁盘的超级块或i节点，默认全写回(true,true)，第一个参数决定超级块是否写回，第二个是i节点
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="inode"></param>
        /// <returns></returns>
        public bool UpdateDiskSFi(bool sb = true, bool inode = true)
        {
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            if (sb)
            {
                fs.Position = SuperBlock.SB_DISK_START * SuperBlock.BLOCK_SIZE;//超级块区
                binFormat.Serialize(fs, sys_sb);
            }
            if (inode)
            {
                fs.Position = SuperBlock.iNODE_DISK_START * SuperBlock.BLOCK_SIZE;//i节点区
                binFormat.Serialize(fs, sys_inode_tt);
            }
            fs.Close();
            return true;
        }
        /// <summary>
        /// 分配i节点ID，正常则返回i节点ID，错误则返回0
        /// </summary>
        /// <returns></returns>
        public uint AllocAiNodeID()
        {
            uint inode_id = 0;
            if (sys_sb.max_inode_id < uint.MaxValue - 1 && sys_sb.max_inode_id >= 100)
            {
                inode_id = sys_sb.max_inode_id;
                sys_sb.max_inode_id++;
                UpdateDiskSFi(true, false);//立即更新超级块磁盘数据
            }
            return inode_id;
        }

        /// <summary>
        /// 输入ID，返回i节点结构，错误则i节点name为.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DiskiNode GetiNode(uint id)
        {
            uint temp_id = id % 128;
            DiskiNode dn = new DiskiNode(0,".",0,0);
            iNodeTable it = sys_inode_tt.tt[temp_id];
            for (int i = 0; i < it.di_table.Count(); i++)
            {
                if (it.di_table[i].id == id)
                {
                    dn = it.di_table[i];
                    return dn;
                }
            }
            return dn;
        }
        /// <summary>
        /// DirectOp里的副本，重构时可以删掉
        /// </summary>
        /// <param name="src"></param>
        /// <param name="tar"></param>
        /// <returns></returns>
        public bool MatchString(string src, string tar)
        {
            string temp1 = tar.Replace(".", @"\.");
            string temp = "^" + temp1.Replace("~", ".+") + "$";
            Regex reg = new Regex(@temp);
            if (reg.IsMatch(src))
                return true;
            else
                return false;
        }

        /// <summary>
        /// 输入路径，返回i节点结构
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public List<DiskiNode> GetiNodeByPath(string path)
        {
            uint temp_id = sys_current_user.current_folder;
            List<DiskiNode> dn_head = new List<DiskiNode>();
            List<DiskiNode> dn_tail = new List<DiskiNode>();
            DiskiNode err_dn = new DiskiNode(0, ".", 0, 0);
            string[] paths0;
            List<string> paths = new List<string>();
            //若为绝对路径
            if (path[0] == '/')
            {
                temp_id = 0;
                paths0 = path[1..].Split(new char[] { '/' });
            }
            //若为相对路径
            else { paths0 = path.Split(new char[] { '/' }); }
            DiskiNode temp_dn = GetiNode(temp_id);
            dn_head.Add(temp_dn);
            dn_tail.Add(temp_dn);
            //去空，如/usr//ui/
            for(int i=0;i<paths0.Length;i++)
            {
                if(paths0[i].Length!=0)
                {
                    paths.Add(paths0[i]);
                }
            }
            //对每一级名字解析
            for (int i = 0; i < paths.Count(); i++)
            {
                //Console.WriteLine("GetiNodeByPath1:" + paths[i]);
                //本级不动
                if (paths[i] == ".") { }
                //返回上一级
                else if(paths[i] == "..")
                {
                    dn_head.Clear();
                    //把当前级的结果遍历
                    for(int j=0;j<dn_tail.Count();j++)
                    {
                        temp_dn = GetiNode(dn_tail[j].fore_addr);
                        bool has_exist = false;
                        //当前级的每个i节点的上一级是否已经加到了新结果里
                        for (int k = 0; k < dn_head.Count(); k++)
                        {
                            if (temp_dn.id == dn_head[k].id)
                            {
                                has_exist = true;
                                break;
                            }
                        }
                        if (!has_exist)
                            dn_head.Add(temp_dn);
                    }
                }
                //正常的符合正则表达式的名字
                else
                {
                    dn_head.Clear();
                    //遍历上一级每一条路径
                    for (int j=0;j<dn_tail.Count();j++)
                    {
                        //Console.WriteLine("dn_tail[j].type/name:" + dn_tail[j].type + dn_tail[j].name);
                        //还没到最后就匹配了文件，忽略这一条路
                        if (dn_tail[j].type==ItemType.FILE && i != paths.Count() - 1)
                        {
                            //Console.WriteLine("IGNORE");
                        }
                        //重大错误，根本不应该出现，要是遇到直接返回错误
                        else if (dn_tail[j].next_addr == null)
                        {
                            Console.WriteLine("ERROR AT GetiNodeByPath: NO THIS FILE/FOLDER");
                            dn_head.Clear();
                            dn_head.Add(err_dn);
                            return dn_head;
                        }
                        //正常地匹配到了文件夹
                        else
                        {
                            for(int k=0;k<dn_tail[j].next_addr.Count();k++)
                            {
                                temp_dn = GetiNode(dn_tail[j].next_addr[k]);
                                //Console.WriteLine("GetiNodeByPath2:" + temp_dn.name);
                                if (MatchString(temp_dn.name, paths[i]))
                                    dn_head.Add(temp_dn);
                            }
                        }
                    }
                }
                //更新结果
                dn_tail.Clear();
                for(int m=0;m<dn_head.Count();m++)
                {
                    dn_tail.Add(dn_head[m]);
                }
            }
            return dn_head;
        }

        /// <summary>
        /// 判断命名是否冲突，冲突返回true
        /// </summary>
        /// <param name="current_folder"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsNameConflict(DiskiNode fold_node,string name,ItemType type)
        {
            //判断新文件(夹)名是否为空
            if (name.Length == 0) { Console.WriteLine("File/Folder's name is empty!"); return false; }
            //判断是否有同名文件(夹)，注意没有文件的可能
            for (int i = 0; fold_node.next_addr != null && i < fold_node.next_addr.Count(); i++)
            {
                DiskiNode temp_node = GetiNode(fold_node.next_addr[i]);
                if (temp_node.name == name && temp_node.type == type)
                    return true;
            }
            return false;
        }
        /// <summary>
        /// 创建文件(夹)：分配i节点
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fname"></param>
        /// <returns></returns>
        public DiskiNode Create(ItemType type, string fname)
        {
            uint curfolder = sys_current_user.current_folder;
            DiskiNode fold_node = GetiNode(curfolder);
            //1,支持在指定的文件路径下创建文件(夹). [revise by Lau Xueyuan, 2019-06-24 01:33]
            //此处为相对路径
            if (fname.Contains("/"))
            {
                string[] filepath = fname.Split("/");
                string newpath = "";
                for (int i = 0; i < filepath.Length - 1; newpath += (filepath[i]+"/"), i++) ;
                fname = filepath[filepath.Count() - 1];
                //Console.WriteLine(newpath);
                List<DiskiNode> fold_node_tmp = GetiNodeByPath(newpath);
                //Console.WriteLine(fold_node_tmp.Count());
                if (fold_node_tmp.Count > 1) { return new DiskiNode(0, ".", 0, 0); }
                else
                {
                    fold_node = fold_node_tmp[0];
                }
                if (fold_node.name == ".") return fold_node;
            }
            if (IsNameConflict(fold_node, fname, type)) //出现同名冲突
            {
                Console.WriteLine("Name Conflict!");
                return new DiskiNode(0, ".", 0, 0);
            }
            //2,分配i节点,分配磁盘块,上级i节点更新,写回磁盘
            uint id = AllocAiNodeID();
            DiskiNode ndn;
            if (type == ItemType.FOLDER)
            {
                ndn = new DiskiNode(id, fname, 0, sys_current_user.uid)
                {
                    type = ItemType.FOLDER
                };
            }
            else
            {
                uint block_addr = AllocADiskBlock();
                ndn = new DiskiNode(id, fname, 1, sys_current_user.uid)
                {
                    type = ItemType.FILE
                };
                ndn.next_addr.Add(block_addr);
            }
            ndn.fore_addr = fold_node.id;
            fold_node.next_addr.Add(id);
            if (sys_inode_tt.tt[id % 128] == null)
                sys_inode_tt.tt[id % 128] = new iNodeTable();
            sys_inode_tt.tt[id % 128].di_table.Add(ndn);
            UpdateDiskSFi(false, true);
            return ndn;
        }
        /// <summary>
        /// 分配磁盘块,正常则返回块地址,错误则返回0,未写回超级块
        /// </summary>
        /// <returns></returns>
        public uint AllocADiskBlock()
        {
            uint block_addr = 0;
            //若最后一组块数大于1
            if (sys_sb.last_group_block_num > 1)
            {
                block_addr = sys_sb.last_group_addr[0];
                sys_sb.last_group_block_num--;
                sys_sb.last_group_addr.RemoveAt(0);
            }
            //若最后一组块数=1，使用组长块，并把倒数第二组加到超级块
            else
            {
                block_addr = sys_sb.last_group_addr[0];
                FileStream fs_alloc = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                BinaryFormatter bin_alloc = new BinaryFormatter();
                fs_alloc.Position = block_addr;
                BlockLeader bl_alloc = (BlockLeader)bin_alloc.Deserialize(fs_alloc);
                fs_alloc.Close();
                sys_sb.last_group_addr.RemoveAt(0);
                sys_sb.last_group_addr = bl_alloc.block_addr;
                sys_sb.last_group_block_num = SuperBlock.BLOCK_IN_GROUP;
            }
            return block_addr;
        }
        /// <summary>
        /// 首次安装文件系统，分配超级块、用户、i节点、组长块，返回true(成功)或false(失败)
        /// </summary>
        /// <returns></returns>
        public bool Install()
        {
            //设置超级管理员和普通用户
            User root = new User();
            User user1 = new User(1001, "123");
            User user2 = new User(1002, "123");
            User user3 = new User(2001, "abc123");
            List<User> ut = new List<User>
            {
                root,
                user1,
                user2,
                user3,
            };
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            fs.Position = SuperBlock.USER_DISK_START * SuperBlock.BLOCK_SIZE;//用户信息，1~9块，9KB
            binFormat.Serialize(fs, ut);
            fs.Close();
            Format();//格式化
            return true;
        }
        /// <summary>
        /// 格式化文件系统，自动检测用户并根据级别格式化不同大小的区域，返回true(成功)false(失败)
        /// </summary>
        /// <returns></returns>
        public bool Format()
        {
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            //若是超级管理员格式化磁盘
            if (sys_current_user.uid == 0)
            {
                //重置超级块
                SuperBlock sb = new SuperBlock();
                //创建root文件夹
                DiskiNode root_inode = new DiskiNode(0, "root", 0, 0){fore_addr = 0, type = ItemType.FOLDER };
                //创建回收站
                DiskiNode recycle_inode = new DiskiNode(1, "recyclebin", 0, 0) { fore_addr = 0,type=ItemType.FOLDER };
                Dictionary<uint, uint> recyclebinMap = new Dictionary<uint, uint>();
                //把root和回收站添加到i节点列表里
                iNodeTT ins_tt = new iNodeTT();
                ins_tt.tt[0] = new iNodeTable();
                ins_tt.tt[0].di_table.Add(root_inode);
                ins_tt.tt[1] = new iNodeTable();
                ins_tt.tt[1].di_table.Add(recycle_inode);
                root_inode.next_addr.Add(recycle_inode.id);
                //初始化用户文件夹
                DiskiNode usr1 = new DiskiNode(2, "usr1001", 0, 1001) { type = ItemType.FOLDER };
                ins_tt.tt[2] = new iNodeTable();
                ins_tt.tt[2].di_table.Add(usr1);
                DiskiNode usr2 = new DiskiNode(3, "usr1002", 0, 1002) { type = ItemType.FOLDER };
                ins_tt.tt[3] = new iNodeTable();
                ins_tt.tt[3].di_table.Add(usr2);
                DiskiNode usr3 = new DiskiNode(4, "usr2001", 0, 2001) { type = ItemType.FOLDER };
                ins_tt.tt[4] = new iNodeTable();
                ins_tt.tt[4].di_table.Add(usr3);
                root_inode.next_addr.Add(usr1.id);
                root_inode.next_addr.Add(usr2.id);
                root_inode.next_addr.Add(usr3.id);
                //重置超级栈
                sb.last_group_addr = new List<uint>();
                for (uint i = 0; i < SuperBlock.BLOCK_IN_GROUP; i++) { sb.last_group_addr.Add((4000+i)* SuperBlock.BLOCK_SIZE); }
                //组长块格式化，这里的32仅仅是前期为了快速建系统，之后要改成数据区组数，即4092*1024/128=32736
                for (uint i = 0; i < 32; i++)
                {
                    BlockLeader bl = new BlockLeader
                    {
                        next_blocks_num = SuperBlock.BLOCK_IN_GROUP
                    };
                    for (uint j = 0; j < 128; j++)
                    {
                        bl.block_addr.Add((4000 + (i+1) * SuperBlock.BLOCK_IN_GROUP + j)*SuperBlock.BLOCK_SIZE);
                    }
                    fs.Position = (4000 + i * SuperBlock.BLOCK_IN_GROUP + 127) * SuperBlock.BLOCK_SIZE;
                    binFormat.Serialize(fs, bl);
                }
                //超级块区写磁盘，占0~2号块
                fs.Position = SuperBlock.SB_DISK_START * SuperBlock.BLOCK_SIZE;
                binFormat.Serialize(fs, sb);
                //回收站空map表写磁盘，占10~99号块
                fs.Position = SuperBlock.RECYCLEBINMAP_DISK_START * SuperBlock.BLOCK_SIZE;
                binFormat.Serialize(fs, recyclebinMap);
                //i节点区写磁盘，占100~3999号块
                fs.Position = SuperBlock.iNODE_DISK_START * SuperBlock.BLOCK_SIZE;
                binFormat.Serialize(fs, ins_tt);
            }
            //TODO：普通用户格式化自己的文件(夹)，即删除自己的全部文件并设置当前文件夹为用户根目录
            else
            {
                //调用函数删除当前用户根目录下所有文件和文件夹
            }
            fs.Close();
            return true;
        }
        /// <summary>
        /// 清除一个磁盘块，写满\0
        /// </summary>
        /// <param name="block_order"></param>
        /// <returns></returns>
        public bool EraseBlock(uint block_order)
        {
            string str = "";
            for (int i = 0; i < 1024; str += "\0", i++) ;
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fs.Seek(block_order * 1024, SeekOrigin.Begin);
            byte[] byteArray = Encoding.Default.GetBytes(str);
            fs.Write(byteArray, 0, byteArray.Length);
            fs.Close();
            return true;
        }
        /// <summary>
        /// 回收磁盘块
        /// </summary>
        /// <param name="block_addr"></param>
        /// <returns></returns>
        public bool RecycleDiskBlock(uint block_addr)
        { 
            EraseBlock(block_addr);
            //若最后一组未满
            if (sys_sb.last_group_block_num < SuperBlock.BLOCK_IN_GROUP)
            {
                sys_sb.last_group_addr.Insert(0, block_addr);
                sys_sb.last_group_block_num++;
            }
            //最后一组满了，新增一组
            else
            {
                BlockLeader newBL = new BlockLeader
                {
                    next_blocks_num = SuperBlock.BLOCK_IN_GROUP,
                    block_addr = sys_sb.last_group_addr
                };
                sys_sb.last_group_block_num = 1;
                sys_sb.last_group_addr.Clear();
                sys_sb.last_group_addr.Add(block_addr);
                //写回组长块
                FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                BinaryFormatter binFormat = new BinaryFormatter();
                fs.Position = block_addr;
                binFormat.Serialize(fs, newBL);
                fs.Close();
            }
            //写回超级块
            UpdateDiskSFi(true, false);
            return true;
        }
        /// <summary>
        /// 通过i节点ID来回收文件(夹)的i节点，删除文件（删除文件夹需要级联，不在这里）
        /// </summary>
        /// <param name="iNodeId"></param>
        /// <returns></returns>
        public bool RecycleiNode(uint iNodeId)
        {
            uint temp_id = iNodeId % SuperBlock.BLOCK_IN_GROUP;
            for (int i = 0; i < sys_inode_tt.tt[temp_id].di_table.Count(); i++)
            {
                if (sys_inode_tt.tt[temp_id].di_table[i].id == iNodeId)
                {
                    DiskiNode rdn = sys_inode_tt.tt[temp_id].di_table[i];
                    //如果是文件夹
                    if (rdn.type == ItemType.FOLDER) { }
                    //如果是文件，要回收所有磁盘块
                    else if(rdn.type==ItemType.FILE)
                    {
                        for (int j = 0; j < rdn.next_addr.Count(); j++)
                        {
                            Console.WriteLine("Recycle a Disk Block.");
                            RecycleDiskBlock(rdn.next_addr[j]);
                        }
                    }
                    sys_inode_tt.tt[temp_id].di_table.RemoveAt(i);
                    //把其上级i节点中的next_addr中的它的ID删掉
                    DiskiNode fore_dn = GetiNode(rdn.fore_addr);
                    fore_dn.next_addr.Remove(iNodeId);
                    break;
                }
            }
            UpdateDiskSFi(false, true);
            return true;
        }

        /// <summary>
        /// 通过路径删除文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public bool DeleteFile(string path)
        {
            DiskiNode temp_dn = GetiNodeByPath(path)[0];
            if (temp_dn.name == ".") { Console.WriteLine("No Such File."); return false; }
            Console.WriteLine(temp_dn.id);
            if (temp_dn.type == ItemType.FOLDER) { Console.WriteLine("This is a folder!"); return false; }
            else if(temp_dn.type == ItemType.FILE)
            {
                RecycleiNode(temp_dn.id);
                Console.WriteLine("Successfully to Delete File.");
                //OutputTT();
                return true;
            }
            return false;
        }
        /// <summary>
        /// 递归删除一个文件夹
        /// </summary>
        /// <param name="inode"></param>
        private void DeleteAFolder(DiskiNode inode)
        {
            if (inode.type == ItemType.FOLDER)
            {
                List<DiskiNode> delList = new List<DiskiNode>();
                delList = (from item in inode.next_addr
                           select GetiNode(item)).ToList();
                foreach (DiskiNode item in delList)
                {
                    DeleteAFolder(item);
                }
                //删除自身（当前文件夹）
                RecycleiNode(inode.id);
            }
            else
            {
                //type == ItemType.FILE
                RecycleiNode(inode.id);
            }
        }
        /// <summary>
        /// 删除文件夹
        /// </summary>
        /// <param name="path">文件夹路径</param>
        public void DeleteFolder(string path)
        {
            List<DiskiNode> dellist = GetiNodeByPath(path);
            foreach (DiskiNode item in dellist)
            {
                DeleteAFolder(item);
            }
        }
        /// <summary>
        /// 复制一个文件夹
        /// </summary>
        /// <param name="src">原文件(夹)i结点</param>
        /// <param name="tar">目的路径</param>
        private void CopyAFolder(DiskiNode src, string tar)
        {
            string newName = tar + "/" + src.name;
            if (src.type == ItemType.FOLDER)
            {
                //文件夹
                List<DiskiNode> itemlist = new List<DiskiNode>();
                itemlist = (from itemid in src.next_addr
                            select GetiNode(itemid)).ToList();

                DiskiNode newfolder = Create(ItemType.FOLDER, newName);
                if (newfolder.name != ".") //成功创建文件夹
                {
                    foreach (DiskiNode item in itemlist)
                    {
                        CopyAFolder(item, tar + "/" + newfolder);
                    }
                }
            }
            else
            {
                //文件
                CopyFile(newName, tar);
            }
        }
        /// <summary>
        /// 复制文件夹
        /// </summary>
        /// <param name="fname">源文件名</param>
        /// <param name="tarpath">目的目录</param>
        public void CopyFolder(string fname, string tarpath)
        {
            List<DiskiNode> fromlist = GetiNodeByPath(fname);
            foreach (DiskiNode folder in fromlist)
            {
                CopyAFolder(folder, tarpath);
            }
        }
        /// <summary>
        /// 通过路径读取文件
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string ReadFile(string path)
        {
            DiskiNode read_dn = GetiNodeByPath(path)[0];
            if (read_dn.name == ".") { Console.WriteLine("No Such File.");return ""; }
            string file_content = "";
            if (read_dn.type != ItemType.FILE) { return "This is a folder!"; }
            else
            {
                FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                for (int i=0;i<read_dn.size;i++)
                {
                    byte[] byData = new byte[1024];
                    fs.Position = read_dn.next_addr[i];
                    fs.Read(byData, 0, byData.Length);
                    file_content += System.Text.Encoding.Default.GetString(byData);
                }
                fs.Close();
                return file_content;
            }
        }
        /// <summary>
        /// 通过路径写文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="file_content"></param>
        /// <returns></returns>
        public bool WriteFile(string path, string file_content)
        {
            DiskiNode wdn = GetiNodeByPath(path)[0];
            if (wdn.name == ".") { Console.WriteLine("No Such File.");return false; }
            int len = (int) wdn.size;
            //截取字符串
            int num = (file_content.Length / (int)SuperBlock.BLOCK_SIZE) + 1;
            //Console.WriteLine("num:" + num.ToString());
            //Console.WriteLine("len:" + len.ToString());
            Console.WriteLine("addr"+wdn.next_addr[0]);
            //若写入字节大于原有磁盘块，分配新盘快
            if (num > len) { for (int i = 0; i < num - len;wdn.next_addr.Add(AllocADiskBlock()), i++) ; }
            //若写入字节小于原有磁盘块，回收旧盘块
            else if (num < len)
            {
                for (int i = 0; i < len - num; i++)
                {
                    int addr_len = wdn.next_addr.Count();
                    RecycleDiskBlock(wdn.next_addr[addr_len - 1]);
                    wdn.next_addr.RemoveAt(addr_len - 1);
                }
            }
            //逐块写入
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            for (int i = 0; i < num; i++)
            {
                int leng = (file_content.Length - i * 1024 > 1024) ? 1024 : file_content.Length - i * 1024;
                string file_block_temp = file_content.Substring(i * 1024, leng);
                byte[] byte_block = System.Text.Encoding.Default.GetBytes(file_block_temp);
                fs.Position = wdn.next_addr[i];
                fs.Write(byte_block, 0, byte_block.Length);
            }
            fs.Close();
            //更新i节点，超级块，文件目录
            UpdateDiskSFi();
            return true;
        }
        /// <summary>
        /// 通过路径重命名文件(夹)
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool Rename(string path, string name,ItemType type)
        {
            DiskiNode rdn = GetiNodeByPath(path)[0];
            if(rdn.name == ".") { Console.WriteLine("No Such File/Folder.");return false; }
            if (IsNameConflict(rdn, name, type)) { return false; }
            else
            {
                rdn.name = name;
                return true;
            }
        }
        /// <summary>
        /// 显示某路径下的文件
        /// </summary>
        /// <param name="path"></param>
        public void ShowFile(string path)
        {
            DiskiNode dn = GetiNodeByPath(path)[0];
            if (dn.name == ".") { Console.WriteLine("No Such Path."); }
            for(int i=0;i<dn.next_addr.Count();i++)
            {
                DiskiNode dn_temp = GetiNode(dn.next_addr[i]);
                Console.WriteLine(i + ":|" + dn_temp.name + "|(ID)" + dn_temp.id + "|"+dn_temp.type);
            }
        }
        /// <summary>
        /// 复制旧i节点物理盘块到新i节点物理盘块，正常返回true
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public bool CopyiNodeDisk(DiskiNode from, DiskiNode to)
        {
            if (from.next_addr.Count() != to.next_addr.Count()) { Console.WriteLine("From's block != To's block"); return false; }
            else
            {
                int block_num = from.next_addr.Count();
                FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                //一块一块地读，因为可能不连续
                for(int i=0;i<block_num;i++)
                {
                    uint b_from = from.next_addr[i] * SuperBlock.BLOCK_SIZE;
                    uint b_to = to.next_addr[i] * SuperBlock.BLOCK_SIZE;
                    Byte[] b_content = new byte[SuperBlock.BLOCK_SIZE];
                    fs.Position = b_from;
                    fs.Read(b_content, 0, (int)SuperBlock.BLOCK_SIZE);
                    fs.Position = b_to;
                    fs.Write(b_content, 0, b_content.Length);
                }
                fs.Close();
                return true;
            }
        }
        public void testReg()
        {
            string[] bs = { "c.txt","pc.txt","cvtxt"};
            for(int i=0;i<bs.Length;i++)
            {
                if (MatchString(bs[i],"c.t~"))
                {
                    Console.WriteLine(bs[i]);
                }
            }

        }
        public void InitializationForTest()
        {
            Create(ItemType.FILE, "log.txt");
            Create(ItemType.FOLDER, "usr1001/Software");
            Create(ItemType.FILE, "usr1001/main.cpp");
            Create(ItemType.FILE, "usr1001/Software/ss.txt");
            Create(ItemType.FILE, "usr1002/1.cpp");
            Create(ItemType.FILE, "usr2001/2.cpp");
            Create(ItemType.FILE, "usr2001/main.cpp");
        }
        /// <summary>
        /// 复制一个文件到另一个目录下（不支持复制文件夹！）
        /// </summary>
        /// <param name="filename">源文件名(或带路径的文件名)，不能是一个文件夹！</param>
        /// <param name="tarpath">目的路径</param>
        public bool CopyFile(string filename, string tarpath)
        {
            List<DiskiNode> from = GetiNodeByPath(filename);
            DiskiNode to = GetiNodeByPath(tarpath).First();
            foreach (DiskiNode inode in from)
            {
                bool collision = false;
                //冲突检查
                foreach (uint id in to.next_addr)
                {
                    //发生同名同类型冲突
                    if (inode.name == GetiNode(id).name &&
                        inode.type == GetiNode(id).type)
                    {
                        collision = true;
                        Console.WriteLine("cannot overwrite directory '" + tarpath + "/" + inode.name + "' with non-directory");
                        break;
                    }
                }
                if (collision == true) continue;
                if (inode.type == ItemType.FOLDER) return false; //排除文件夹
                DiskiNode newiNode = new DiskiNode(AllocAiNodeID(), inode.name, inode.size, sys_current_user.uid)
                {
                    fore_addr = to.id
                };
                for (int i = 0; i < inode.size; i++)
                {
                    newiNode.next_addr.Add(AllocADiskBlock());
                }
                newiNode.t_create = DateTime.Now;
                newiNode.t_revise = DateTime.Now;
                if (sys_inode_tt.tt[newiNode.id % 128] == null)
                    sys_inode_tt.tt[newiNode.id % 128] = new iNodeTable();
                sys_inode_tt.tt[newiNode.id % 128].di_table.Add(newiNode);
                to.next_addr.Add(newiNode.id);
                CopyiNodeDisk(inode, newiNode);
            }
            UpdateDiskSFi(true, true);
            return true;
        }
        /// <summary>
        /// 移动一个文件filename或文件夹到另一个目录下tarpath
        /// </summary>
        /// <param name="filename">文件(夹)名(当前目录下的文件)或带相对路径的文件</param>
        /// <param name="tarpath">移动到的目的地址</param>
        public void Move(string filename, string tarpath)
        {
            //若为模糊输入，返回所有匹配结果的i结点
            List<DiskiNode> fromlist = GetiNodeByPath(filename);
            DiskiNode to = GetiNodeByPath(tarpath).First();
            foreach (DiskiNode inode in fromlist)
            {
                //把原地址上一级（父级）i结点中存的下一级信息中有关该节点的id删除
                GetiNode(inode.fore_addr).next_addr.Remove(inode.id);
                //再检查目标文件夹中是否有同名同类型文件冲突，有则直接覆盖
                IEnumerable<uint> collision = from id in to.next_addr
                                              where GetiNode(id).name == inode.name &&
                                                    GetiNode(id).type == inode.type
                                              select id;
                //每次最多只有一个文件(夹)出现冲突，解决冲突的办法是直接覆盖
                if (collision.Count() > 0)
                {
                    to.next_addr.Remove(collision.First());
                }
                inode.fore_addr = to.id;  //修改该文件(夹)父级指针
                to.next_addr.Add(inode.id);
            }
            UpdateDiskSFi(false, true); //将变更写回磁盘
        }
        /// <summary>
        /// 进入某一文件夹
        /// </summary>
        /// <param name="foldername"></param>
        public void ChangeCurrentDirectory(string foldername)
        {
            List<DiskiNode> inode = GetiNodeByPath(foldername);
            if (inode.Count() > 1)
            {
                if(!(inode.Count()==2 && inode[0].name == inode[1].name))
                {
                    Console.WriteLine("cd: too many arguments");
                    return;
                }
            }
            if (inode[0].type == ItemType.FILE)
            {
                Console.WriteLine("This is a FILE.");
            }
            else sys_current_user.current_folder = inode.First().id;
        }
        /// <summary>
        /// 将一个文件移入回收站
        /// </summary>
        /// <param name="path">文件的路径</param>
        public void MoveToRecycleBin(string path)
        {
            List<DiskiNode> delitem = GetiNodeByPath(path);
            DiskiNode recyclebin = GetiNode(1);   //获取回收站i结点
            foreach (DiskiNode item in delitem)
            {
                recyclebinMap.Add(item.id, item.fore_addr);
                GetiNode(item.fore_addr).next_addr.Remove(item.id);
                item.fore_addr = 1;
                recyclebin.next_addr.Add(item.id);
            }
        }
        /// <summary>
        /// 全盘搜索一个文件（支持模糊查找）
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>返回所有同名文件的i节点</returns>
        public List<DiskiNode> SearchInAllDisk(string filename)
        {
            List<DiskiNode> reslist = new List<DiskiNode>();
            //未建立索引，需要遍历全树   
            Stack<DiskiNode> stack = new Stack<DiskiNode>();
            stack.Push(GetiNode(0));
            while (stack.Count != 0)
            {
                DiskiNode visit = stack.Peek();
                stack.Pop();
                if (MatchString(visit.name, filename))
                {
                    //找到一个与filename名字相同的文件(夹)
                    reslist.Add(visit);
                }
                for (int i = visit.next_addr.Count() - 1; i >= 0; i--)
                {
                    stack.Push(GetiNode(visit.next_addr[i]));
                }
            }
            if (reslist.Count == 0)
            {
                Console.WriteLine("No file or folder named " + filename + " was found!");
            }
            else
            {
                //输出
                foreach (DiskiNode item in reslist)
                {
                    Console.WriteLine(item.name);
                    if (item.type == ItemType.FOLDER)
                    {
                        foreach (uint subid in item.next_addr)
                        {
                            Console.WriteLine(GetiNode(subid).name);
                        }
                    }
                }
            }
            return reslist;
        }
        /// <summary>
        /// 在当前目录下搜索（支持模糊查找）
        /// </summary>
        /// <param name="path">指定从那个目录开始搜索</param>
        /// <param name="filename">要搜索的文件名</param>
        /// <returns>返回当前目录下所有符合的文件i结点</returns>
        public List<DiskiNode> SearchFromSpecificFolder(string path, string filename)
        {
            List<DiskiNode> reslist = new List<DiskiNode>();
            Stack<DiskiNode> stack = new Stack<DiskiNode>();
            uint curfolder = sys_current_user.current_folder;
            if (path != "")
            {
                //从path指定的目录开始搜索
                string[] filepath = path.Split("/");
                foreach (string folder in filepath)
                {
                    //返回当前目录下名字为folder的文件(夹)
                    foreach (uint itemid in GetiNode(curfolder).next_addr)
                    {
                        if (GetiNode(itemid).name == folder)
                        {
                            curfolder = itemid; //改变当前目录（但不改变用户项中当前目录）
                            break;
                        }
                    }
                }
                //curfolder即为搜索的根目录
            }

            //从当前目录下开始搜索
            stack.Push(GetiNode(curfolder));
            while (stack.Count != 0)
            {
                DiskiNode visit = stack.Peek();
                stack.Pop();
                if (MatchString(visit.name, filename))
                {
                    reslist.Add(visit);
                    Console.WriteLine(visit.name);
                }
                for (int i = visit.next_addr.Count - 1; i >= 0; i--)
                {
                    stack.Push(GetiNode(visit.next_addr[i]));
                }
            }
            return reslist;
        }
        /// <summary>
        /// 恢复回收站中的文件或文件夹
        /// </summary>
        /// <param name="name">文件名</param>
        /// <returns>返回还原文件的i结点</returns>
        public DiskiNode RestoreFromRecycleBin(string name)
        {
            DiskiNode recyclebin = GetiNode(1);
            List<uint> restore = (from item in recyclebin.next_addr
                                  where GetiNode(item).name == name
                                  select item).ToList();
            uint removeid = restore.First();
            if (restore.Count() > 1)
            {
                for (int i = 0; i < restore.Count(); i++)
                {
                    DiskiNode inode = GetiNode(restore[i]);
                    Console.WriteLine("id: " + inode.id + ", name: " + inode.name + ", type: " + inode.type +
                        ", size: " + inode.size + ", revise time: " + inode.t_revise);
                }
                Console.Write("Please select a file or folder to restore: ");
                uint id = Convert.ToUInt32(Console.ReadLine());
                IEnumerable<uint> tmpid = from c in restore
                                          where c == id
                                          select c;
                //用户输入的文件不存在
                if (tmpid.Count() == 0)
                {
                    Console.WriteLine("File or Directory does not exists!");
                    return new DiskiNode(0, ".", 0, 0);
                }
                removeid = id;
            }
            DiskiNode node = GetiNode(removeid);
            node.fore_addr = recyclebinMap[removeid];
            GetiNode(node.fore_addr).next_addr.Add(removeid);
            GetiNode(1).next_addr.Remove(removeid);
            return node;
        }
        /// <summary>
        /// 显示回收站内容
        /// </summary>
        public void ShowRecycleBin()
        {
            DiskiNode recyclebin = GetiNode(1);
            foreach (uint id in recyclebin.next_addr)
            {
                DiskiNode inode = GetiNode(id);
                Console.WriteLine("name: " + inode.name + ", type: " + inode.type +
                        ", size: " + inode.size + ", revise time: " + inode.t_revise);
            }
        }
        /// <summary>
        /// 清空回收站
        /// </summary>
        public void ClearRecycleBin()
        {
            DiskiNode recyclebin = GetiNode(1);
            foreach (uint item in recyclebin.next_addr)
            {
                DiskiNode inode = GetiNode(item);
                DeleteAFolder(inode);
            }
        }
        /// <summary>
        /// 计算文件（夹）大小
        /// </summary>
        /// <param name="inode">文件(夹)的i结点</param>
        /// <returns>返回文件(夹)大小</returns>
        public uint CalFileOrFolderSize(DiskiNode inode)
        {
            uint size = 0;
            if (inode.type == ItemType.FOLDER)
            {
                foreach (uint subid in inode.next_addr)
                {
                    size += CalFileOrFolderSize(GetiNode(subid));
                }
            }
            else
            {
                size = inode.size;
            }
            return size;
        }
        /// <summary>
        /// 展示当前文件夹的情况
        /// </summary>
        public void ShowCurrentDirectory()
        {
            DiskiNode diriNode = GetiNode(sys_current_user.current_folder);
            if (diriNode.next_addr.Count() == 0)
            {
                Console.WriteLine("There is no file/folder.");
            }
            else
            {
                Console.WriteLine("|Type\t\t|Size\t\t|Owner\t\t|ID\t\t|Name\t\t");
                Console.WriteLine("|---------------|---------------|---------------|---------------|---------------");
            }
            foreach (uint itemid in diriNode.next_addr)
            {
                DiskiNode ndn = GetiNode(itemid);
                uint ssize = (ndn.type==ItemType.FILE)?(ndn.size):(CalFileOrFolderSize(ndn));
                string strsize;
                if (ssize >= 1024) { strsize = (ssize/1024).ToString() + " MB"; }
                else { strsize = ssize.ToString() + " KB"; }
                Console.WriteLine("|" + ndn.type + "\t\t|" + strsize + "\t\t|" + ndn.uid + "\t\t|" + ndn.id+ "\t\t|" + ndn.name + "\t\t");
            }
        }
        /// <summary>
        /// 运行测试
        /// </summary>
        public void exeall()
        {
            //Install();//安装文件系统，会创建root,回收站,usr1001,usr1002,usr2001.!!!仅在首次运行时需要!!!
            Start();//启动文件系统
            //InitializationForTest();//批处理，创建一些文件和文件夹.!!!首次运行时需要，之后注释掉!!!

            
            Console.WriteLine("-----------------");
            Console.WriteLine("root:");
            ShowFile("/");
            Console.WriteLine("-----------------");
            Console.WriteLine("root/usr1001:");
            ShowFile("/usr1001");
            Console.WriteLine("-----------------");
            Console.WriteLine("root/usr1001/Software:");
            ShowFile("/usr1001/Software");
            Console.WriteLine("-----------------");
            Console.WriteLine("root/usr1002:");
            ShowFile("/usr1002");
            Console.WriteLine("-----------------");
            Console.WriteLine("root/usr2001:");
            ShowFile("/usr2001");
            Console.WriteLine("-----------------");


            //DirectOp op = new DirectOp();
            //op.ShowCurrentDirectory();
            //Console.WriteLine("curFolder: " + GetiNode(sys_current_user.current_folder).name);
            //Move("usr1001/Software/ss.txt", "usr1002");


            //CopyFile("usr2001/2.cpp", "usr1001/Software");
            ChangeCurrentDirectory("usr1001");
            ShowCurrentDirectory();


            //Console.WriteLine("CurFolder: " + GetiNode(sys_current_user.current_folder).name);
            //ForwardtoADirectory("../..");
            //Console.WriteLine("CurFolder: " + GetiNode(sys_current_user.current_folder).name);
            //ShowCurrentDirectory();
            //ForwardtoADirectory("usr1002");
            //Console.WriteLine("CurFolder: " + GetiNode(sys_current_user.current_folder).name);
            //ShowCurrentDirectory();

            //Console.WriteLine(list.Count());

        }
    }
}

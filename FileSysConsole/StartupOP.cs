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
        public MemoryUser sys_current_user = new MemoryUser(0, 0);//当前登录用户，登录写好要后修改current_user！！！！！！！！！
        public SuperBlock sys_sb = new SuperBlock();//超级块
        public FileTable sys_file_table = new FileTable();//目录表目，精简的内存i节点，包含全部i节点
        public iNodeTT sys_inode_tt = new iNodeTT();


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

        //用户登录
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
                    userlist[i].current_folder = curfolder;
                    FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    BinaryFormatter binFormat = new BinaryFormatter();
                    fs.Position = 3 * SuperBlock.BLOCK_SIZE;//用户信息，从1024到2048，占第2个块
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
            fs.Position = 3 * SuperBlock.BLOCK_SIZE;//用户信息，从1024到2048，占第2个块
            List<User> userslist = (List<User>)binFormat.Deserialize(fs);
            fs.Close();
            //For Test
            //Console.WriteLine(userslist[2].password);
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
                //2，读取超级块、目录表目到内存
                FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                BinaryFormatter binf = new BinaryFormatter();
                fs.Position = 0 * SuperBlock.BLOCK_SIZE;
                sys_sb = (SuperBlock)binf.Deserialize(fs);
                fs.Position = 10 * SuperBlock.BLOCK_SIZE;
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
                fs.Position = 0 * SuperBlock.BLOCK_SIZE;//超级块区，从0到1024，占第1个块
                binFormat.Serialize(fs, sys_sb);
            }
            if (inode)
            {
                fs.Position = 10 * SuperBlock.BLOCK_SIZE;//i节点区，估计最大大小：1024*50*64/1024=3200块，预分配3910块，数据区起始块为4000
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
        /// 输入ID，返回i节点结构
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DiskiNode GetiNode(uint id)
        {
            uint temp_id = id % 128;
            DiskiNode dn = new DiskiNode();
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
        /// 输入路径，返回i节点结构
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public List<DiskiNode> GetiNodeByPath(string path)
        {
            uint temp_id = sys_current_user.current_folder;
            List<DiskiNode> tdn = new List<DiskiNode>();
            DiskiNode temp_dn = new DiskiNode(0,".",0,0), temp_dn2;
            string[] paths0;
            List<string> paths = new List<string>();
            //若为绝对路径
            if (path[0] == '/')
            {
                temp_id = 0;
                paths0 = path[1..].Split(new char[] { '/' });
            }
            //相对路径
            else { paths0 = path.Split(new char[] { '/' }); }
            temp_dn = GetiNode(temp_id);
            //去空，如/usr//ui/
            for(int i=0;i<paths0.Length;i++)
            {
                if(paths0[i].Length!=0)
                {
                    paths.Add(paths0[i]);
                }
            }
            for (int i = 0; i < paths.Count(); i++)
            {
                if (paths[i] == ".") { }
                else if(paths[i] == "..")
                {
                    temp_dn = GetiNode(temp_dn.fore_addr);
                }
                else
                {
                    if (temp_dn.next_addr == null) { Console.WriteLine("ERROR AT GetiNodeByPath: NO THIS FILE/FOLDER");temp_dn.name = "."; return tdn; }
                    bool have_found = false;
                    for (int j = 0; j < temp_dn.next_addr.Count(); j++)
                    {
                        temp_dn2 = GetiNode(temp_dn.next_addr[j]);
                        Regex reg = new Regex(paths[i]);
                        if (temp_dn2.name == paths[i])
                        {
                            temp_dn = temp_dn2;
                            have_found = true;
                            break;
                        }
                    }
                    if (!have_found) { Console.WriteLine("ERROR AT GetiNodeByPath: NO WAY"); temp_dn.name = "."; return tdn; }
                }
            }
            return tdn;
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
        public bool Create(ItemType type, string fname)
        {
            uint curfolder = sys_current_user.current_folder;
            //1,支持在指定的文件路径下创建文件(夹). [revise by Lau Xueyuan, 2019-06-24 01:33]
            //此处为相对路径
            if (fname.Contains("/")) {
                string[] filepath = fname.Split("/");
                fname = filepath[filepath.Count() - 1];
                List<string> tmp = filepath.ToList();
                tmp.RemoveAt(filepath.Count() - 1);
                filepath = tmp.ToArray();
                foreach (string folder in filepath)
                {
                    //返回当前目录下名字为folder的文件(夹)
                    IEnumerable<DiskiNode> inode =
                        from subid in GetiNode(curfolder).next_addr
                        where GetiNode(subid).name == folder
                        select GetiNode(subid);
                    curfolder = inode.First().id;  //更改当前文件夹为folder，不改变用户项记录的当前文件夹
                }
            }
            DiskiNode fold_node = GetiNode(curfolder);
            if (IsNameConflict(fold_node, fname, type)) { Console.WriteLine("Name Conflict!"); return false; } ;
            //2,分配i节点,分配磁盘块,上级i节点更新,写回磁盘
            uint id = AllocAiNodeID();
            DiskiNode ndn;
            if (type == ItemType.FOLDER)
            {
                ndn = new DiskiNode(id, fname, 0, sys_current_user.uid);
                ndn.type = ItemType.FOLDER;
            }
            else
            {
                uint block_addr = AllocADiskBlock();
                ndn = new DiskiNode(id, fname, 1, sys_current_user.uid);
                ndn.type = ItemType.FILE;
                ndn.next_addr.Add(block_addr);
            }
            ndn.fore_addr = fold_node.id;
            fold_node.next_addr.Add(id);
            if (sys_inode_tt.tt[id % 128] == null)
                sys_inode_tt.tt[id % 128] = new iNodeTable();
            sys_inode_tt.tt[id % 128].di_table.Add(ndn);
            UpdateDiskSFi(false, true);
            return true;
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
            //设置超级管理员和普通管理员
            User root = new User();
            User user1 = new User(1001, "123");
            User user2 = new User(1002, "123");
            User user3 = new User(2001, "abc");
            User user4 = new User(2002, "abc");
            User user5 = new User(3001, "abc123");
            List<User> ut = new List<User>
            {
                root,
                user1,
                user2,
                user3,
                user4,
                user5
            };
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            fs.Position = 3 * SuperBlock.BLOCK_SIZE;//用户信息，从1024到2048，占第2个块
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
                //格式化超级块区、i节点区，重建根目录
                SuperBlock sb = new SuperBlock();//重置超级块
                DiskiNode root_inode = new DiskiNode(0, "root", 0, 0);//i节点区只保留root文件夹节点
                root_inode.fore_addr = 0;
                iNodeTT root_tt = new iNodeTT();
                root_tt.tt[0] = new iNodeTable();
                root_tt.tt[0].di_table.Add(root_inode);
                //磁盘数据区格式化
                sb.last_group_addr = new List<uint>();
                for (uint i = 0; i < SuperBlock.BLOCK_IN_GROUP; i++) { sb.last_group_addr.Add((4000+i)* SuperBlock.BLOCK_SIZE); }//重置超级栈
                //组长块格式化
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
                fs.Position = 0 * SuperBlock.BLOCK_SIZE;//超级块区，从0到1024，占第1个块
                binFormat.Serialize(fs, sb);
                fs.Position = 10 * SuperBlock.BLOCK_SIZE;//i节点区，估计最大大小：1024*50*64/1024=3200块，预分配3910块，数据区起始块为4000
                binFormat.Serialize(fs, root_tt);
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
            uint temp_id = iNodeId % 128;
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
                OutputTT();
                return true;
            }
            return false;
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
                for (int i=0;i<read_dn.block_num;i++)
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
            int len = (int) wdn.block_num;
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
                Console.Write(i);
                Console.Write(" F:");
                Console.Write(dn_temp.id);
                Console.WriteLine(dn_temp.name);
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

        public bool MatchString(string src, string tar)
        {
            string temp = "^" + tar + "$";
            Regex reg = new Regex(@temp);
            if (reg.IsMatch(src))
                return true;
            else
                return false;
        }

        public void testReg()
        {
            string[] bs = { "c.txt","pc.txt","cvtxt"};
            for(int i=0;i<bs.Length;i++)
            {
                if (MatchString(bs[i],"c.txt"))
                {
                    Console.WriteLine(bs[i]);
                }
            }

        }
        /// <summary>
        /// 运行测试
        /// </summary>
        public void exeall()
        {
            //Install();//安装文件系统，仅在首次运行时需要
            ///Start();//启动文件系统
            //Creat(ItemType.FILE, "test2.txt");//在当前目录创建文件
            //Creat(ItemType.FOLDER, "usr1");
            //OutputTT();
            //ShowFile("/");
            //DeleteFile("test2.txt");
            //Console.WriteLine("-----------------");
            //ShowFile("/");
            //WriteFile("test2.txt", "123456789");
            //Console.WriteLine(ReadFile("test2.txt"));
            testReg();
        }
    }
}

using System;
using System.IO;
using System.Text;
using FileSysTemp.FSBase;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;

namespace FileSysTemp
{
    public class Execute
    {
        public MemoryUser sys_current_user = new MemoryUser(0, 0);//当前登录用户，登录后修改current_user
        public SuperBlock sys_sb = new SuperBlock();//超级块
        public FileTable sys_file_table = new FileTable();//目录表目，精简的内存i节点，包含全部i节点
        public iNodeTT sys_inode_tt = new iNodeTT();

        public Execute()
        {

        }

        //用户登录
        public bool Login()
        {
            //TODO:用户登录模块，登录成功则修改current_user并返回true，否则返回false
            return true;
        }

        //启动文件系统
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
                fs.Position = 4 * SuperBlock.BLOCK_SIZE;
                sys_file_table = (FileTable)binf.Deserialize(fs);
                fs.Position = 600 * SuperBlock.BLOCK_SIZE;
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
        //更新磁盘的超级块/目录列表/i节点
        public bool UpdateDiskSFi(bool sb = true, bool ft = true, bool inode = true)
        {
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            if (sb)
            {
                fs.Position = 0 * SuperBlock.BLOCK_SIZE;//超级块区，从0到1024，占第1个块
                binFormat.Serialize(fs, sys_sb);
            }
            if (ft)
            {
                fs.Position = 4 * SuperBlock.BLOCK_SIZE;//（用户信息区占3个块）目录表目区，估计大小：i节点数*10B=(1024*50)*10/1024=500块(实际大小与文件名长度有关)，预分配600块
                binFormat.Serialize(fs, sys_file_table);
            }
            if (inode)
            {
                fs.Position = 600 * SuperBlock.BLOCK_SIZE;//i节点区，估计最大大小：1024*50*64/1024=3200块，预分配3400块，数据区起始块为4000
                binFormat.Serialize(fs, sys_inode_tt);
            }
            fs.Close();
            return true;
        }
        //分配i节点ID,正常则返回i节点ID,错误则返回0
        public uint AllocAiNodeID()
        {
            uint inode_id = 0;
            if (sys_sb.max_inode_id < uint.MaxValue - 1 && sys_sb.max_inode_id >= 100)
            {
                inode_id = sys_sb.max_inode_id;
                sys_sb.max_inode_id++;
                UpdateDiskSFi(true, false, false);//立即更新超级块磁盘数据
            }
            return inode_id;
        }

        //通过id找i节点
        public DiskiNode GetiNode(uint id)
        {
            uint temp_id = id % 128;
            DiskiNode dn;
            DiskiNode dn2 = new DiskiNode();
            for (int i = 0; i < sys_inode_tt.tt[temp_id].di_table.Count(); i++)
            {
                if (sys_inode_tt.tt[temp_id].di_table[i].id == id)
                {
                    dn = sys_inode_tt.tt[temp_id].di_table[i];
                    return dn;
                }
            }
            return dn2;
        }
        //通过文件名和当前目录找i节点
        public DiskiNode GetiNodeByPath(string path)
        {
            uint temp_id = sys_current_user.current_folder;
            DiskiNode temp_dn = new DiskiNode();
            string[] paths;
            //若为绝对路径
            if (path[0] == '/')
            {
                temp_id = 0;
                paths = path[1..].Split(new char[] { '/' });
            }
            else { paths = path.Split(new char[] { '/' }); }
            temp_dn = GetiNode(temp_id);

            for (int i = 0; i < paths.Length; i++)
            {
                if (temp_dn.next_addr == null) { Console.WriteLine("ERROR AT GetiNodeByPath: NO THIS FILE/FOLDER"); return temp_dn; }
                bool have_found = false;
                for (int j = 0; j < temp_dn.next_addr.Count(); j++)
                {
                    DiskiNode temp_dn2 = GetiNode(temp_dn.next_addr[j]);
                    if (temp_dn2.name == paths[i])
                    {
                        temp_dn = temp_dn2;
                        have_found = true;
                        break;
                    }
                }
                if (!have_found) { Console.WriteLine("ERROR AT GetiNodeByPath: "); return temp_dn; }
            }
            return temp_dn;
        }
        //分配i节点,更新磁盘的目录表目和i节点列表;mode=0为文件夹,=1为文件
        //需要注意重名,注意文件夹/文件区别
        public bool Creat(uint mode, string fname)
        {
            //1,确保名字不冲突
            DiskiNode fold_node = GetiNode(sys_current_user.current_folder);
            for (int i = 0; fold_node.next_addr != null && i < fold_node.next_addr.Count(); i++)
            {
                DiskiNode temp_node = GetiNode(fold_node.next_addr[i]);
                if (temp_node.name == fname)
                {
                    if (temp_node.block_num == 0 && mode == 0) { Console.WriteLine("Name Conflict"); return false; }
                    else if (temp_node.block_num != 0 && mode != 0) { Console.WriteLine("Name Conflict"); return false; }
                }
            }
            //2,分配i节点,分配磁盘块,上级i节点更新,写回磁盘
            uint id = AllocAiNodeID();
            DiskiNode dn;
            if (mode == 0)
            {
                dn = new DiskiNode(id, fname, 0, sys_current_user.uid);
            }
            else
            {
                uint block_addr = AllocADiskBlock();
                dn = new DiskiNode(id, fname, 1, sys_current_user.uid);
                dn.next_addr.Add(block_addr);
            }
            dn.fore_addr = fold_node.id;
            fold_node.next_addr.Add(id);
            FileItem temp_fi = new FileItem(dn);
            sys_file_table.table.Add(temp_fi);
            if (sys_inode_tt.tt[id % 128] == null)
                sys_inode_tt.tt[id % 128] = new iNodeTable();
            sys_inode_tt.tt[id % 128].di_table.Add(dn);
            UpdateDiskSFi(false, true, true);
            return true;
        }
        //分配磁盘块,正常则返回块地址,错误则返回0
        public uint AllocADiskBlock()
        {
            uint block_addr = 0;
            if (sys_sb.last_group_block_num > 1)
            {
                block_addr = sys_sb.last_group_addr[0];
                sys_sb.last_group_block_num--;
                sys_sb.last_group_addr.RemoveAt(0);
            }
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


        //首次安装文件系统
        public bool Install()
        {
            //设置超级管理员
            User root = new User();
            UserTable ut = new UserTable();
            ut.utable.Add(root);
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            fs.Position = 3 * SuperBlock.BLOCK_SIZE;//用户信息，从1024到2048，占第2个块
            binFormat.Serialize(fs, ut);
            fs.Close();
            Format();//格式化
            return true;
        }
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
                //iNodeTable node_table = new iNodeTable();//重置磁盘i节点
                iNodeTT root_tt = new iNodeTT();
                root_tt.tt[0] = new iNodeTable();
                //node_table.di_table.Add(root_inode);
                FileItem root_fi = new FileItem(root_inode);
                FileTable root_ft = new FileTable();//重置文件表
                root_ft.table.Add(root_fi);
                root_tt.tt[0].di_table.Add(root_inode);
                //磁盘数据区格式化
                sb.last_group_addr = new List<uint>();
                for (uint i = 0; i < SuperBlock.BLOCK_IN_GROUP; i++) { sb.last_group_addr.Add(4000 + i); }//重置超级栈
                //组长块格式化
                for (uint i = 0; i < 32000; i++)
                {
                    BlockLeader bl = new BlockLeader();
                    bl.next_blocks_num = SuperBlock.BLOCK_IN_GROUP;
                    for (uint j = 0; j < 128; j++)
                    { bl.block_addr.Add(4000 + i * SuperBlock.BLOCK_IN_GROUP * SuperBlock.BLOCK_SIZE + j); }
                    fs.Position = 4000 + i * SuperBlock.BLOCK_IN_GROUP * SuperBlock.BLOCK_SIZE + 127;
                    binFormat.Serialize(fs, bl);
                }
                fs.Position = 0 * SuperBlock.BLOCK_SIZE;//超级块区，从0到1024，占第1个块
                binFormat.Serialize(fs, sb);
                fs.Position = 4 * SuperBlock.BLOCK_SIZE;//（用户信息区占3个块）目录表目区，估计大小：i节点数*10B=(1024*50)*10/1024=500块(实际大小与文件名长度有关)，预分配600块
                binFormat.Serialize(fs, root_ft);
                fs.Position = 600 * SuperBlock.BLOCK_SIZE;//i节点区，估计最大大小：1024*50*64/1024=3200块，预分配3400块，数据区起始块为4000
                binFormat.Serialize(fs, root_tt);
            }
            //TODO：普通用户格式化自己的文件(夹)，即删除自己的全部文件并设置当前文件夹为用户根目录
            else
            {
                //...
            }
            fs.Close();
            return true;
        }

        //清除一个磁盘块
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

        //回收磁盘块
        public bool RecycleDiskBlock(uint block_addr)
        {
            EraseBlock(block_addr);
            if (sys_sb.last_group_block_num < SuperBlock.BLOCK_IN_GROUP)
            {
                sys_sb.last_group_addr.Insert(0, block_addr);
                sys_sb.last_group_block_num++;
            }
            else
            {
                BlockLeader newBL = new BlockLeader();
                newBL.next_blocks_num = SuperBlock.BLOCK_IN_GROUP;
                newBL.block_addr = sys_sb.last_group_addr;
                sys_sb.last_group_block_num = 1;
                sys_sb.last_group_addr.Clear();
                sys_sb.last_group_addr.Add(block_addr);
                FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                BinaryFormatter binFormat = new BinaryFormatter();
                //写回组长块
                fs.Position = block_addr;
                binFormat.Serialize(fs, newBL);
                fs.Close();
            }
            //写回超级块
            UpdateDiskSFi(true, false, false);
            return true;
        }
        //回收i节点，writenow=1把i节点立即写回磁盘(适用于删文件),=0不写回磁盘(适用于删文件夹)需要之后手动把i节点写回磁盘
        public bool RecycleiNode(uint iNodeId, bool writenow)
        {
            uint temp_id = iNodeId % 128;
            for (int i = 0; i < sys_inode_tt.tt[temp_id].di_table.Count(); i++)
            {
                if (sys_inode_tt.tt[temp_id].di_table[i].id == iNodeId)
                {
                    DiskiNode rdn = sys_inode_tt.tt[temp_id].di_table[i];
                    //如果是文件夹
                    if (rdn.block_num == 0) { }
                    //如果是文件，要回收所有磁盘块
                    else
                    {
                        for (int j = 0; j < rdn.next_addr.Count(); j++)
                        {
                            RecycleDiskBlock(rdn.next_addr[j]);
                        }
                    }
                    sys_inode_tt.tt[temp_id].di_table.RemoveAt(i);
                    break;
                }
            }
            if (writenow) { UpdateDiskSFi(false, false, true); }
            return true;
        }

        //删除文件
        public bool DeleteFile(string path)
        {
            string[] paths = path.Split(new char[] { '/' });
            uint temp_id = sys_current_user.current_folder;
            DiskiNode temp_dn = new DiskiNode();
            for (int i = 0; i < paths.Length; i++)
            {
                //temp_dn = GetiNodeByName(paths[i], temp_id);
                if (temp_dn.next_addr == null)
                {
                    return false;
                }
                temp_id = temp_dn.id;
            }
            if (temp_dn.block_num == 0) { Console.WriteLine("This is a folder!"); return false; }
            else
            {
                RecycleiNode(temp_id, true);
                return true;
            }
        }

        //读取文件
        public string ReadFile(string path)
        {
            return "test";
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Execute exe = new Execute();
            //exe.Install();
            //exe.Start();
            //exe.Creat(1, "new1.cpp");
            //Console.WriteLine(exe.sys_inode_tt.tt[0].di_table[0].name);
        }
    }
}

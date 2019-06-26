using System;
using System.Collections.Generic;
using System.Text;

namespace FileSysTemp.FSBase
{
    /// <summary>
    /// 超级块
    /// </summary>
    [Serializable]
    public class SuperBlock
    {
        //uint.MAX = 1024*1024*1024*4 = 4,294,967,295
        public const uint BLOCK_SIZE = 1024;              //块大小
        public const uint BLOCK_SUM_NUM = 1024 * 1024 * 4;//块的总数量，4GB
        public const uint iNODE_SUM_NUM = 1024 * 50;      //i节点的总数量
        public const uint BLOCK_IN_GROUP = 128;           //每一组有128块
        public const uint SB_DISK_START = 0;              //超级块区磁盘起始块号
        public const uint USER_DISK_START = 3;            //用户信息区磁盘起始块号
        public const uint RECYCLEBINMAP_DISK_START = 10;  //回收站Map表区磁盘起始块号
        public const uint iNODE_DISK_START = 100;         //i节点区磁盘起始块号
        public const uint DATA_DISK_START = 4000;         //数据区磁盘起始块号
        public uint CURRENT_INODE_NUM = 0;                //当前i结点数量
        public uint free_block_num = 1023 * 1024 * 4;     //空闲块的数量,4GB-4MB
        public uint free_inode_num = 1024 * 50;           //空闲i节点的数量
        public uint last_group_block_num = BLOCK_IN_GROUP;//最后一组的块的数量
        public List<uint> last_group_addr;                //最后一组的块的地址(保留区大小为4000块)
        public bool change_flag = true;                   //超级块修改标志
        public uint max_inode_id = 100;                   //当前分配的最大i节点ID,因为有默认文件夹,所以第一次取个大一点的数值
        public uint check_byte = 707197;                  //校验位
    }

    /// <summary>
    /// 项目类型：文件，文件夹
    /// </summary>
    public enum ItemType
    {
        FILE,
        FOLDER
    }

    /// <summary>
    /// 磁盘i节点项
    /// </summary>
    [Serializable]
    public class DiskiNode
    {
        public uint id;                                //磁盘i节点ID
        public string name;                            //文件(夹)名
        public uint block_num;                         //关联的块数，也就是文件大小，若为0则为文件夹
        public uint size;                              //文件(夹)大小
        public uint uid;                               //用户ID，1~1000用户组1，2~2000用户组2...
        public List<uint> next_addr = new List<uint>();//文件的磁盘块地址或者文件夹下的文件(夹)的i节点ID
        public uint fore_addr;                         //上层目录的i的ID
        public DateTime t_create;                      //文件(夹)创建时间
        public DateTime t_revise;                      //文件(夹)修改时间
        public ItemType type;                          //类型：文件/文件夹

        public DiskiNode(uint id, string name, uint block_num, uint uid)
        {
            this.id = id;
            this.name = name;
            this.block_num = block_num;
            this.uid = uid;
        }
        public DiskiNode() { }
    }

    /// <summary>
    /// i节点列表
    /// </summary>
    [Serializable]
    public class iNodeTable
    {
        public List<DiskiNode> di_table = new List<DiskiNode>();
    }

    /// <summary>
    /// i节点区域
    /// </summary>
    [Serializable]
    public class iNodeTT
    {
        public iNodeTable[] tt = new iNodeTable[128];
    }

    /// <summary>
    /// 目录表目项
    /// </summary>
    [Serializable]
    public class FileItem
    {
        public uint itemId;           //目录表项id
        public List<uint> sub_ItemId; //当前目录下包含文件(夹)的目录表项id
        public uint parent_itemId;    //当前目录的上一级目录的目录表项id
        public uint inode_id;         //该目录表项对应的i节点id
        public string name;           //文件(夹)名，用于搜索
        public FileItem(uint curid, uint parentid, List<uint> subid, DiskiNode dn)
        {
            itemId = curid;
            parent_itemId = parentid;
            sub_ItemId = subid;
            this.inode_id = dn.id;
            this.name = dn.name;
        }
        public FileItem(DiskiNode dn)
        {
            this.inode_id = dn.id;
            this.name = dn.name;
        }
    }

    /// <summary>
    /// 目录表目区
    /// </summary>
    [Serializable]
    public class FileTable
    {
        public List<FileItem> table = new List<FileItem>();

        /// <summary>
        /// 搜索同名文件(夹)
        /// </summary>
        /// <param name="fname">文件(夹)名</param>
        /// <returns>所有同名文件(夹)的集合</returns>
        public List<uint> getiNodeId(string fname)
        {
            List<uint> res = new List<uint>();
            foreach (FileItem item in table)
            {
                if (item.name == fname)
                {
                    res.Add(item.inode_id);
                }
            }
            return res;
        }
    }

    /// <summary>
    /// 当前内存里的用户项
    /// </summary>
    public class MemoryUser
    {
        //建议：用户数小于10，每个用户打开文件数小于40
        public uint uid;                               //用户uid
        public uint current_folder;                    //当前所在文件夹的i节点ID
        public List<uint> open_file = new List<uint>();//用户打开文件表
        public MemoryUser(uint uid, uint cf)
        {
            this.uid = uid;
            this.current_folder = cf;
            this.open_file.Add(cf);
        }
        /// <summary>
        /// 可供显式调用的析构函数
        /// </summary>
        /// 
        public void Destructor()
        {
            open_file.Clear();
            uid = 0;
            current_folder = 0;
        }
    }

    /// <summary>
    /// 磁盘用户项(需要往磁盘存放的用户信息)
    /// </summary>
    [Serializable]
    public class User
    {
        public uint uid;           //用户uid
        public string password;
        public uint current_folder;//上次退出时所在文件夹的i节点ID(可以加一个“回到上次工作区”功能)

        public User(uint uid = 0, string pwd = "")
        {
            this.uid = uid;
            this.password = pwd;
        }
    }

    /// <summary>
    /// 用户信息区
    /// </summary>
    [Serializable]
    public class UserTable
    {
        public List<User> utable = new List<User>();
    }

    /// <summary>
    /// 组长块项
    /// </summary>
    [Serializable]
    public class BlockLeader
    {
        public uint next_blocks_num;                    //下一组的磁盘块数量
        public List<uint> block_addr = new List<uint>();//下一组的每一块的地址
    }
}

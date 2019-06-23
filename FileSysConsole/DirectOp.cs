using FileSysTemp.FSBase;
using System;
using System.Collections.Generic;
using System.Linq;


namespace FileSysConsole
{
    public class Execute2
    {
        public void exeall()
        {
            //...
        }
    }
    /// <summary>
    /// 针对目录表项和内存i节点的操作集合
    /// </summary>
    public class DirectOp
    {
        /// <summary>
        /// 该部分与Program有重复，建议代码重构时删去！
        /// </summary>
        public iNodeTT sys_inode_tt = new iNodeTT();

        public const uint BLOCK_SIZE = 1024;

        /// <summary>
        /// 通过i结点id返回i节点结构体（该部分与Program有重复，建议代码重构时删去！）
        /// </summary>
        /// <param name="id">内存i节点Id</param>
        /// <returns>i结点结构体</returns>
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

        ///简化的i节点，只用于显示目录时使用
        public class SimplifiediNode
        {
            public string name;          //文件(夹)名
            public uint size;            //文件大小，单位KB/MB/GB
            public DateTime reviseDate;  //文件(夹)修改日期
            public ItemType type;        //类型(文件/文件夹)
        }

        /// <summary>
        /// 显示用户当前所在文件夹的内容
        /// </summary>
        /// <param name="user">当前内存中的用户项</param>
        /// <returns>精简的i节点链表</returns>
        public List<SimplifiediNode> ShowCurrentDirectory(MemoryUser user)
        {
            DiskiNode diriNode = GetiNode(user.current_folder);
            List<SimplifiediNode> content = new List<SimplifiediNode>();
            foreach (uint itemid in diriNode.next_addr)
            {
                DiskiNode subiNode = GetiNode(itemid);
                SimplifiediNode fileorfolder = new SimplifiediNode();
                fileorfolder.name = subiNode.name;
                fileorfolder.size = subiNode.block_num * BLOCK_SIZE;
                fileorfolder.reviseDate = subiNode.t_revise;
                fileorfolder.type = subiNode.type;
                content.Add(fileorfolder);
            }
            return content;
        }
    }
}

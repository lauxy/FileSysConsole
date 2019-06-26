using FileSysTemp.FSBase;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileSysConsole
{
    /// <summary>
    /// 针对目录表项和内存i节点的操作集合
    /// </summary>
    public class DirectOp
    {
        const uint BLOCK_SIZE = 1024;

        MemoryUser user = new MemoryUser(1,0);

        Execute startup = new Execute();

        bool isCreateIndex = false;

        //FileTable filetable = new FileTable();

        SuperBlock superblk = new SuperBlock();

        ///简化的i节点，只用于显示目录时使用
        ///简化的i节点，只用于显示目录时使用
        public class SimplifiediNode
        {
            public string name;          //文件(夹)名
            public uint size;            //文件大小，单位KB/MB/GB
            public DateTime reviseDate;  //文件(夹)修改日期
            public ItemType type;        //类型(文件/文件夹)
        }



        /// <summary>
        /// 修改用户密码
        /// </summary>
        public void RevisePassword()
        {

        }
    }
   
    public class Execute2
    {
        public void exeall()
        {
            //DirectOp op = new DirectOp();
            //op.ShowCurrentDirectory();
            //MemoryUser user = new MemoryUser();
            //List<SimplifiediNode> reslist = op.ShowCurrentDirectory(user);
        }
    }
}

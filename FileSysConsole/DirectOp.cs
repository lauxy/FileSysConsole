﻿using FileSysTemp.FSBase;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

        FileTable filetable = new FileTable();

        SuperBlock superblk = new SuperBlock();

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
        public List<SimplifiediNode> ShowCurrentDirectory()
        {
            DiskiNode diriNode = startup.GetiNode(user.current_folder);
            List<SimplifiediNode> content = new List<SimplifiediNode>();
            foreach (uint itemid in diriNode.next_addr)
            {
                DiskiNode subiNode = startup.GetiNode(itemid);
                SimplifiediNode fileorfolder = new SimplifiediNode();
                fileorfolder.name = subiNode.name;
                fileorfolder.size = subiNode.block_num * BLOCK_SIZE;
                fileorfolder.reviseDate = subiNode.t_revise;
                fileorfolder.type = subiNode.type;
                content.Add(fileorfolder);
            }
            return content;
        }

        /// <summary>
        /// 返回上一级目录，并修改用户当前所在文件夹的i结点
        /// </summary>
        /// <param name="user">当前内存中的用户项</param>
        public void BacktoPreviousDirect()
        {
            DiskiNode curiNode = startup.GetiNode(user.current_folder);
            uint preid = curiNode.fore_addr;
            if (preid != curiNode.id)
            {
                //非根目录
                user.current_folder = preid;
            }
            //preid等于本身i结点的id值，则说明当前目录是根目录，无法回退
            return;
        }

        const uint MAX_USERNUM = 10; //内存中允许的最大用户数（同时在线）
        uint cur_usernum = 0;        //当前内存中驻留的用户数量

        /// <summary>
        /// 用户登录文件系统
        /// </summary>
        /// <param name="uid">用户id</param>
        /// <param name="password">用户密码</param>
        /// <returns>登录是否成功</returns>
        public bool LoginSys(uint uid, string password)
        {
            List<User> users = startup.LoadUsersInfofromDisk();
            bool isExist = false;
            User curUser = new User();
            foreach(User user in users)
            {
                if(user.uid == uid)
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
            if(curUser.password == password)
            {
                //密码输入正确
                if(cur_usernum < MAX_USERNUM)
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
            bool issucceed = startup.StoreUserInfotoDisk(user.uid, user.current_folder);
            user.Destructor(); //释放资源
            Console.WriteLine("You have been logout successfully!");
            return issucceed;
        }

        /// <summary>
        /// 判断字符串是否匹配（为解决模糊查找而设计）
        /// </summary>
        /// <param name="src">原字符串</param>
        /// <param name="tar">待查字符串</param>
        /// <returns>两字符串是否匹配（或相等）</returns>
        public bool MatchString(string src, string tar)
        {
            if (tar.Contains("*"))
            {
                string[] filter = tar.Split("*");
                for(int i = 0; i < filter.Count(); i++)
                {
                    if (!src.Contains(filter[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            return src == tar;
        }

        /// <summary>
        /// 全盘搜索一个文件（支持模糊查找）
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>返回所有同名文件的i节点</returns>
        public List<DiskiNode> SearchInAllDisk(string filename)
        {
            List<DiskiNode> reslist = new List<DiskiNode>();
            if (!isCreateIndex)
            {
                //未建立索引，需要遍历全树   
                Stack<DiskiNode> stack = new Stack<DiskiNode>();
                bool[] flag = new bool[superblk.CURRENT_INODE_NUM]; //标记结点是否进过栈
                for (int i = 0; i < superblk.CURRENT_INODE_NUM; i++)
                    flag[i] = false;
                stack.Push(startup.GetiNode(0));
                flag[0] = true;
                while (stack.Count != 0)
                {
                    DiskiNode visit = stack.Peek();
                    stack.Pop();
                    if(MatchString(visit.name, filename))
                    {
                        //找到一个与filename名字相同的文件(夹)
                        reslist.Add(visit);
                    }
                    for(int i = visit.next_addr.Count - 1; i >= 0; i--)
                    {
                        if (!flag[visit.next_addr[i]])
                        {
                            stack.Push(startup.GetiNode(visit.next_addr[i]));
                            flag[visit.next_addr[i]] = true;
                        }
                    }
                }
            }
            else
            {
                //已建立索引，全盘搜索只需要遍历目录表, 时间复杂度O(n)
                foreach(FileItem item in filetable.table)
                {
                    if(item.name == filename)
                    {
                        reslist.Add(startup.GetiNode(item.inode_id));
                    }
                }
            }
            if(reslist.Count == 0)
            {
                Console.WriteLine("No file or folder named " + filename + " was found!");
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
            bool[] flag = new bool[superblk.CURRENT_INODE_NUM]; //标记结点是否进过栈
            for (int i = 0; i < superblk.CURRENT_INODE_NUM; i++)
                flag[i] = false;
            uint curfolder = user.current_folder;
            if (path != null)
            {
                //从path指定的目录开始搜索
                string[] filepath = path.Split("/");

                foreach (string folder in filepath)
                {
                    //返回当前目录下名字为folder的文件(夹)
                    IEnumerable<DiskiNode> inode =
                        from subid in startup.GetiNode(curfolder).next_addr
                        where startup.GetiNode(subid).name == folder
                        select startup.GetiNode(subid);
                    curfolder = inode.First().id;  //更改当前文件夹为folder，不改变用户项记录的当前文件夹
                }
                //curfolder即为搜索的根目录
            }

            //从当前目录下开始搜索
            stack.Push(startup.GetiNode(curfolder));
            flag[user.current_folder] = true;
            while (stack.Count != 0)
            {
                DiskiNode visit = stack.Peek();
                stack.Pop();
                if (MatchString(visit.name, filename))
                {
                    reslist.Add(visit);
                }
                for (int i = visit.next_addr.Count - 1; i >= 0; i--)
                {
                    if (flag[visit.next_addr[i]] == false)
                    {
                        stack.Push(startup.GetiNode(visit.next_addr[i]));
                        flag[visit.next_addr[i]] = true;
                    }
                }
            }
            return reslist;
        }
    }

    public class Execute2
    {
        public void exeall()
        {
            //DirectOp op = new DirectOp();
            //MemoryUser user = new MemoryUser();
            //List<SimplifiediNode> reslist = op.ShowCurrentDirectory(user);
        }
    }
}

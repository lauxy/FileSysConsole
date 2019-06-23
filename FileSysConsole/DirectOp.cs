using FileSysTemp.FSBase;
using System;
using System.Collections.Generic;

namespace FileSysConsole
{
    /// <summary>
    /// 针对目录表项和内存i节点的操作集合
    /// </summary>
    public class DirectOp
    {
        const uint BLOCK_SIZE = 1024;

        const uint iNODE_SUM_NUM = 1024 * 50;      //i节点的总数量

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

        /// <summary>
        /// 从磁盘用户区加载所有用户信息到内存
        /// </summary>
        /// <returns></returns>
        public List<User> LoadUsersInfofromDisk()
        {
            List<User> userslist = new List<User>();

            //Todo: 从磁盘用户区加载所有用户信息到内存.
            return userslist;
        }

        /// <summary>
        /// 将用户信息写回到磁盘
        /// </summary>
        public bool StoreUserInfotoDisk(uint uid, uint curfolder)
        {
            //Todo: 将MemoryUser写回到磁盘
            return true;
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
            List<User> users = LoadUsersInfofromDisk();
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
            bool issucceed = StoreUserInfotoDisk(user.uid, user.current_folder);
            user.Destructor(); //释放资源
            Console.WriteLine("You have been logout successfully!");
            return issucceed;
        }


        /// <summary>
        /// 全盘搜索一个文件，返回所有同名文件的i节点。
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public List<DiskiNode> SearchFile(string filename)
        {
            List<DiskiNode> reslist = new List<DiskiNode>();
            if (!isCreateIndex)
            {
                //未建立索引，需要遍历全树   
                Stack<DiskiNode> stack = new Stack<DiskiNode>();
                bool[] flag = new bool[superblk.CURRENT_INODE_NUM]; //标记结点是否进过栈
                for (int i = 0; i < superblk.CURRENT_INODE_NUM; i++)
                    flag[i] = false;
                DiskiNode root = new DiskiNode();
                stack.Push(startup.GetiNode(0));
                flag[0] = true;
                while (stack.Count != 0)
                {
                    DiskiNode visit = stack.Peek();
                    stack.Pop();
                    if(visit.name == filename)
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

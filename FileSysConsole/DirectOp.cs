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
            string temp1 = tar.Replace(".", @"\.");
            string temp = "^" + temp1.Replace("~", ".+") + "$";
            Regex reg = new Regex(@temp);
            if (reg.IsMatch(src))
                return true;
            else
                return false;
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
                    if(MatchString(item.name, filename))
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

        /// <summary>
        /// 移动一个文件filename或文件夹到另一个目录下tarpath
        /// </summary>
        /// <param name="filename">文件(夹)名(当前目录下的文件)或带相对路径的文件</param>
        /// <param name="tarpath">移动到的目的地址</param>
        public void Move(string filename, string tarpath)
        {
            //若为模糊输入，返回所有匹配结果的i结点
            List<DiskiNode> fromlist = startup.GetiNodeByPath(filename);
            DiskiNode to = startup.GetiNodeByPath(tarpath).First();
            //string[] fname = (from item in fromlist
            //                  select item.name).ToArray();  //获取所有匹配项的文件名
            ////把原地址上一级（父级）i结点中存的下一级信息中有关该节点的id删除
            //foreach (uint id in startup.GetiNode(fromlist.First().fore_addr).next_addr)
            //{
            //    IEnumerable<uint> removei = from name in fname
            //                                where startup.GetiNode(id).name == name
            //                                select id;
            //    startup.GetiNode(fromlist.First().fore_addr).next_addr.Remove(removei.First());
            //}
            
            foreach (DiskiNode inode in fromlist)
            {
                //把原地址上一级（父级）i结点中存的下一级信息中有关该节点的id删除
                startup.GetiNode(inode.fore_addr).next_addr.Remove(inode.id);
                //再检查目标文件夹中是否有同名同类型文件冲突，有则直接覆盖
                IEnumerable<uint> collision = from id in to.next_addr
                                              where startup.GetiNode(id).name == inode.name &&
                                                    startup.GetiNode(id).type == inode.type
                                              select id;
                //每次最多只有一个文件(夹)出现冲突，解决冲突的办法是直接覆盖
                if (collision.Count() > 0) 
                {
                    to.next_addr.Remove(collision.First());
                }
                inode.fore_addr = to.id;  //修改该文件(夹)父级指针
                to.next_addr.Add(inode.id);
            }
        }

        /// <summary>
        /// 复制一个文件到另一个目录下（不支持复制文件夹！）
        /// </summary>
        /// <param name="filename">源文件名(或带路径的文件名)，不能是一个文件夹！</param>
        /// <param name="tarpath">目的路径</param>
        public bool CopyFile(string filename, string tarpath)
        {
            List<DiskiNode> from = startup.GetiNodeByPath(filename);
            DiskiNode to = startup.GetiNodeByPath(tarpath).First();
            List<DiskiNode> duplication = from; //from的副本
            foreach(DiskiNode inode in duplication)
            {
                bool collision = false;
                //冲突检查
                foreach(uint id in to.next_addr)
                {
                    //发生同名同类型冲突
                    if(inode.name == startup.GetiNode(id).name &&
                        inode.type == startup.GetiNode(id).type)
                    {
                        collision = true;
                        Console.WriteLine("cannot overwrite directory '" + tarpath + "/" + inode.name + "' with non-directory");
                        break;
                    }
                }
                if (collision == true) continue;
                DiskiNode oldiNode = inode;
                if (inode.type == ItemType.FOLDER) return false; //排除文件夹
                inode.id = startup.AllocAiNodeID(); //分配一个i结点
                inode.fore_addr = to.id;
                inode.next_addr.Clear();
                for(int i = 0; i < inode.block_num; i++)
                {
                    inode.next_addr.Add(startup.AllocADiskBlock());
                }
                inode.t_create = DateTime.Now;
                inode.t_revise = DateTime.Now;
                to.next_addr.Add(inode.id);
                startup.CopyiNodeDisk(oldiNode, inode);
            }
            return true;
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
                            select startup.GetiNode(itemid)).ToList();
                
                DiskiNode newfolder = startup.Create(ItemType.FOLDER, newName);
                if(newfolder.name != ".") //成功创建文件夹
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
            List<DiskiNode> fromlist = startup.GetiNodeByPath(fname);
            foreach(DiskiNode folder in fromlist)
            {
                CopyAFolder(folder, tarpath);
            }
        }

        /// <summary>
        /// 递归删除一个文件夹
        /// </summary>
        /// <param name="inode"></param>
        private void DeleteAFolder(DiskiNode inode)
        {
            if(inode.type == ItemType.FOLDER)
            {
                List<DiskiNode> delList = new List<DiskiNode>();
                delList = (from item in inode.next_addr
                           select startup.GetiNode(item)).ToList();
                foreach(DiskiNode item in delList)
                {
                    DeleteAFolder(item);
                }
                //删除自身（当前文件夹）
                startup.RecycleiNode(inode.id);
            }
            else
            {
                //type == ItemType.FILE
                startup.RecycleiNode(inode.id);
            }
        }

        /// <summary>
        /// 删除文件夹
        /// </summary>
        /// <param name="path">文件夹路径</param>
        public void DeleteFolder(string path)
        {
            List<DiskiNode> dellist = startup.GetiNodeByPath(path);
            foreach(DiskiNode item in dellist)
            {
                DeleteAFolder(item);
            }
        }

        /// <summary>
        /// 回收站文件地址映射（用于还原）List<Dictionary<inode_id, fore_addr_id>>
        /// </summary>
        Dictionary<uint, uint> recyclebinMap = new Dictionary<uint, uint>();

        /// <summary>
        /// 将一个文件移入回收站
        /// </summary>
        /// <param name="path">文件的路径</param>
        public void MoveToRecycleBin(string path)
        {
            List<DiskiNode> delitem = startup.GetiNodeByPath(path);
            DiskiNode recyclebin = startup.GetiNode(1);   //获取回收站i结点
            foreach(DiskiNode item in delitem)
            {
                recyclebinMap.Add(item.id, item.fore_addr);
                startup.GetiNode(item.fore_addr).next_addr.Remove(item.id);
                item.fore_addr = 1;
                recyclebin.next_addr.Add(item.id); 
            }
        }

        /// <summary>
        /// 恢复回收站中的文件或文件夹
        /// </summary>
        /// <param name="name">文件名</param>
        /// <returns>返回还原文件的i结点</returns>
        public DiskiNode RestoreFromRecycleBin(string name)
        {
            DiskiNode recyclebin = startup.GetiNode(1);
            List<uint> restore = (from item in recyclebin.next_addr
                                  where startup.GetiNode(item).name == name
                                  select item).ToList();
            uint removeid = restore.First();
            if (restore.Count() > 1)
            {
                for (int i = 0; i < restore.Count(); i++)
                {
                    DiskiNode inode = startup.GetiNode(restore[i]);
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
            DiskiNode node = startup.GetiNode(removeid);
            node.fore_addr = recyclebinMap[removeid];
            startup.GetiNode(node.fore_addr).next_addr.Add(removeid);
            startup.GetiNode(1).next_addr.Remove(removeid);
            return node;
        }

        /// <summary>
        /// 显示回收站内容
        /// </summary>
        public void ShowRecycleBin()
        {
            DiskiNode recyclebin = startup.GetiNode(1);
            foreach(uint id in recyclebin.next_addr)
            {
                DiskiNode inode = startup.GetiNode(id);
                Console.WriteLine("name: " + inode.name + ", type: " + inode.type +
                        ", size: " + inode.size + ", revise time: " + inode.t_revise);
            }
        }

        /// <summary>
        /// 清空回收站
        /// </summary>
        public void ClearRecycleBin()
        {
            DiskiNode recyclebin = startup.GetiNode(1);
            foreach(uint item in recyclebin.next_addr)
            {
                DiskiNode inode = startup.GetiNode(item);
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
            if(inode.type == ItemType.FOLDER)
            {
                foreach(uint subid in inode.next_addr)
                {
                    size += CalFileOrFolderSize(startup.GetiNode(subid));
                }               
            }
            else
            {
                //type == FILE
                size = inode.block_num * BLOCK_SIZE;
            }
            return size;
        }

        /// <summary>
        /// 用户权限检查
        /// </summary>
        /// <returns></returns>
        public bool PermissionCheck()
        {
            return true;
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

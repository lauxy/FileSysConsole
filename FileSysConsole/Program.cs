using FileSysTemp.FSBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FileSysConsole
{
    /// <summary>
    /// 外围程序
    /// </summary>
    public class ExternalProgram
    {
        //正则表达式匹配命令
        public string[] help =
        {
            "ls",
            "ll",
            "touch",
            "file",
            "cat",
            "chgrp",
            "chown",
            "cmp",
            "find",
            "index",
            "sql",
            "rm",
            "scp",
            "mv",
            "rec",
            "write",
            "chmod",
            "login",
            "logout",
            "passwd",
            "install",
            "format"
        };
        public string[] regstr =
        {
            @"^help$",//0:help
            @"^.+ -h$",//1:help

            @"^ls[ ]*$",//2:输出当前路径下的文件(夹)，缺省为当前目录
            @"^ls [^- ]+$",//3:输出某一路径下的文件(夹)
            @"^ls -s (name|size|type|creat|revise)$",//4:按顺序输出当前目录
            @"^ls -s (name|size|type|creat|revise) [^- ]+$",//5:按顺序输出某一目录

            @"^ll[ ]*$",//6:输出当前路径下的文件(夹)，缺省为当前目录
            @"^ll [^- ]+$",//7:输出某一路径下的文件(夹)
            @"^ll -s (name|size|type|creat|revise)$",//8:按顺序输出当前目录
            @"^ll -s (name|size|type|creat|revise) [^- ]+$",//9:按顺序输出某一目录

            @"^touch [^- ]+$",//10:创建文件，输入带新文件名的path
            @"^touch -f [^- ]+$",//11:创建文件夹，输入带新文件名的path
            @"^file [^- ]+$",//12:查看某文件/文件夹详情
            @"^file -d [^- ]+$",//13:查看某文件/文件夹详情，含addr
            @"^cat [^- ]+$",//14输出文件内容
            @"^cmp [^- ]+ [^- ]+$",//15比较两个文件
            @"^cmp -d [^- ]+ [^- ]+$",//16比较两个文件，含i节点（除ID）比较
            @"^write [^- ]+$",//17写文件

            @"^find [^- ]+$",//18当前目录查找
            @"^find [^- ]+ [^- ]+$",//19某一目录下查找
            @"^find -a [^- ]+$",//20全盘搜索
            @"^find -r size [^- ]+ [^- ]+ [^- ]+$",//21当前限定范围目录查找
            @"^find -r type [^- ]+ [^- ]+$",//22当前限定范围目录查找
            @"^find -r (create|revise) (20[0-9][0-9])/(1[0-2]|[1-9])/(3[0-1]|[1-2][0-9]|[1-9]) ([0-9]|1[0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]) (20[0-9][0-9])/(1[0-2]|[1-9])/(3[0-1]|[1-2][0-9]|[1-9]) ([0-9]|1[0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]) [^- ]+$",//当前限定范围目录查找
            @"^find -r size [^- ]+ [^- ]+ [^- ]+$",//24某一限定范围目录查找
            @"^find -r type [^- ]+ [^- ]+$",//25某一限定范围目录查找
            @"^find -r (create|revise) (20[0-9][0-9])/(1[0-2]|[1-9])/(3[0-1]|[1-2][0-9]|[1-9]) ([0-9]|1[0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]) (20[0-9][0-9])/(1[0-2]|[1-9])/(3[0-1]|[1-2][0-9]|[1-9]) ([0-9]|1[0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]) [^- ]+ [^- ]+$",//某一限定范围目录查找
            @"^find -a -r size [^- ]+ [^- ]+ [^- ]+$",//27全盘限定范围目录查找
            @"^find -a -r type [^- ]+ [^- ]+$",//28全盘限定范围目录查找
            @"^find -a -r (create|revise) (20[0-9][0-9])/(1[0-2]|[1-9])/(3[0-1]|[1-2][0-9]|[1-9]) ([0-9]|1[0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]) (20[0-9][0-9])/(1[0-2]|[1-9])/(3[0-1]|[1-2][0-9]|[1-9]) ([0-9]|1[0-9]|2[0-3]):([0-5][0-9]):([0-5][0-9]) [^- ]+$",//全盘限定范围目录查找

            @"^index [^- ]+$",//30建立索引
            @"^sql$",//31输入SQL
            @"^mv [^- ]+ [^- ]+$",//32移动文件或文件夹
            @"^mv -d [^- ]+ [^- ]+$",//33移动时显示都移动了哪些文件/文件夹
            @"^rm [^- ]+$",//34删除文件或文件夹
            @"^rm -d [^- ]+ [^- ]+$",//35删除时显示都删除了哪些文件/文件夹
            @"^rm -c [^- ]+$",//36删除文件或文件夹，并不放回收站
            @"^rm -c -d [^- ]+$",//37删除时显示都删除了哪些文件/文件夹，并不放回收站
            @"^cp [^- ]+ [^- ]+$",//38复制文件
            @"^cp -d [^- ]+ [^- ]+$",//39复制时显示都复制了哪些文件/文件夹
            @"^rec [^- ]+$",//40还原某个文件
            @"^rec [^- ]+ -p [^- ]+$",//41还原某个文件到指定路径
            @"^rec -clr$",//42清空回收站
            @"^rec -show$",//43显示回收站

            @"^chomd [^- ]+ -name [^ ~]+$",//44重命名
            @"^chomd -a [^- ]+ [\d]+ [0-7]$",//45更改权限

            @"^login$",//46登录
            @"^logout$",//47注销
            @"^passwd$",//48更改密码
            @"^install$",//49安装文件系统
            @"^format$",//50格式化文件系统
            @"^cd [^ ]+$",//51进入目录
            @"^rm -f [^- ]+$",//52删除文件夹
            @"^rm -c -f [^- ]+$",//53彻底删除文件夹，并不放回收站
            @"^cp -f [^- ]+ [^- ]+$",//54复制文件夹
            @"^chomd -r [^- ]+ [\d]+ [0-7]$",//55更改权限
        };
    }
    /// <summary>
    /// 执行程序
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            ExternalProgram ext = new ExternalProgram();
            Execute exe = new Execute();
            exe.exeall();

            //循环执行
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                if(exe.sys_current_user!=null)
                    Console.Write(exe.sys_current_user.uid + "@" + exe.GetiNode(exe.sys_current_user.current_folder).name + ">");
                Console.ForegroundColor = ConsoleColor.Yellow;
                string command = Console.ReadLine();
                Console.ForegroundColor = ConsoleColor.White;
                string[] com_part = command.Split(' ');
                int regorder = 9999;
                for (int i = 0; i < ext.regstr.Length; i++)
                {
                    if (Regex.IsMatch(command, ext.regstr[i]))
                    {
                        regorder = i;
                        Console.WriteLine("Command ID:" + i);
                    }
                }
                switch (regorder)
                {
                    case 0: { Console.WriteLine("There is help now."); System.Diagnostics.Process.Start("notepad.exe", "help.txt"); break; }
                    case 1:
                        {
                            for (int i = 0; i < ext.regstr.Length; i++)
                                if (ext.regstr[i].Contains(com_part[0]))
                                    Console.WriteLine(ext.regstr[i]);
                            break;
                        }
                    case 2:
                        {
                            exe.ShowDirectory();
                            break;
                        }
                    case 3:
                        {
                            exe.ShowDirectory(com_part[1], "type", true);
                            break;
                        }
                    case 4:
                        {
                            exe.ShowDirectory(".", com_part[2], true);
                            break;
                        }
                    case 5:
                        {
                            exe.ShowDirectory(com_part[3], com_part[2], true);
                            break;
                        }
                    case 6: { exe.ShowDirectory(".", "type", false); break; }
                    case 7:
                        {
                            exe.ShowDirectory(com_part[1], "type", false);
                            break;
                        }
                    case 8:
                        {
                            exe.ShowDirectory(".", com_part[2], false);
                            break;
                        }
                    case 9:
                        {
                            exe.ShowDirectory(com_part[3], com_part[2], false);
                            break;
                        }
                    case 10:
                        {
                            exe.Create(FileSysTemp.FSBase.ItemType.FILE, com_part[1]);
                            break;
                        }
                    case 11:
                        {
                            exe.Create(FileSysTemp.FSBase.ItemType.FOLDER, com_part[2]);
                            break;
                        }
                    case 12:
                        {
                            exe.ShowDetail(com_part[1], false);
                            break;
                        }
                    case 13:
                        {
                            exe.ShowDetail(com_part[2], true);
                            break;
                        }
                    case 14:
                        {
                            Console.WriteLine(exe.ReadFile(com_part[1]));
                            break;
                        }
                    case 15:
                        {
                            Console.WriteLine(exe.ComparedThem(com_part[1], com_part[2], false));
                            break;
                        }
                    case 16:
                        {
                            Console.WriteLine(exe.ComparedThem(com_part[1], com_part[2], true));
                            break;
                        }
                    case 17:
                        {
                            exe.WriteFile(com_part[1]);
                            break;
                        }
                    case 18:
                        {
                            iNodeTable itab = new iNodeTable();
                            itab.di_table = exe.SearchFromSpecificFolder("",com_part[1]);
                            exe.ShowiNodeList(itab, "type");
                            break;
                        }
                    case 19:
                        {
                            iNodeTable itab = new iNodeTable();
                            itab.di_table = exe.SearchFromSpecificFolder(com_part[2], com_part[1]);
                            exe.ShowiNodeList(itab, "type");
                            break;
                        }
                    case 20:
                        {
                            iNodeTable itab = new iNodeTable();
                            itab.di_table = exe.SearchInAllDisk(com_part[2]);
                            exe.ShowiNodeList(itab, "type");
                            break;
                        }
                    case 21:
                        {

                            break;
                        }
                    case 22:
                        {

                            break;
                        }
                    case 23:
                        {

                            break;
                        }
                    case 24:
                        {

                            break;
                        }
                    case 25:
                        {

                            break;
                        }
                    case 26:
                        {

                            break;
                        }
                    case 27:
                        {

                            break;
                        }
                    case 28:
                        {

                            break;
                        }
                    case 29:
                        {

                            break;
                        }
                    case 30:
                        {
                            exe.CreateIndexForSearch(com_part[1]);
                            break;
                        }
                    case 31:
                        {
                            exe.ExeSql();
                            break;
                        }
                    case 32:
                        {
                            exe.Move(com_part[1], com_part[2]);
                            break;
                        }
                    case 33:
                        {

                            break;
                        }
                    case 34:
                        {
                            exe.MoveToRecycleBin(com_part[1]);
                            break;
                        }
                    case 35:
                        {

                            break;
                        }
                    case 36:
                        {
                            exe.DeleteFile(com_part[2]);
                            break;
                        }
                    case 37:
                        {

                            break;
                        }
                    case 38:
                        {
                            exe.CopyFile(com_part[1], com_part[2]);
                            break;
                        }
                    case 39:
                        {

                            break;
                        }
                    case 40:
                        {
                            exe.RestoreFromRecycleBin(com_part[1]);
                            break;
                        }
                    case 41:
                        {

                            break;
                        }
                    case 42:
                        {
                            exe.ClearRecycleBin();
                            break;
                        }
                    case 43:
                        {
                            exe.ShowRecycleBin();
                            break;
                        }
                    case 44:
                        {
                            exe.Rename(com_part[1], com_part[3], exe.GetiNodeByPath(com_part[1]).First().type);
                            break;
                        }
                    case 45:
                        {
                            exe.AssignAuthority(com_part[2], Convert.ToUInt32(com_part[3]), Convert.ToUInt32(com_part[4]));
                            break;
                        }
                    case 46:
                        {
                            exe.LoginSys();
                            break;
                        }
                    case 47:
                        {
                            exe.LogoutSys();
                            break;
                        }
                    case 48:
                        {
                            exe.RevisePassword();
                            break;
                        }
                    case 49:
                        {

                            break;
                        }
                    case 50:
                        {

                            break;
                        }
                    case 51:
                        {
                            exe.ChangeCurrentDirectory(com_part[1]);
                            break;
                        }
                    case 52:
                        {
                            exe.MoveToRecycleBin(com_part[2]);
                            break;
                        }
                    case 53:
                        {
                            exe.DeleteFolder(com_part[3]);
                            break;
                        }
                    case 54:
                        {
                            exe.CopyFolder(com_part[2], com_part[3]);
                            break;
                        }
                    case 55:
                        {
                            exe.RecycleAuthority(com_part[2], Convert.ToUInt32(com_part[3]), Convert.ToUInt32(com_part[4]));
                            break;
                        }
                    default: { Console.WriteLine("Error, Enter \"help\" or \"xxx -h\" to get help."); break; }
                }
            }

            //Execute2 exe2 = new Execute2();
            //exe2.exeall();

            //exe.Install();
            //exe.Start();
            //exe.Creat(1, "new1.cpp");
            //Console.WriteLine(exe.sys_inode_tt.tt[0].di_table[0].name);
        }
    }
}
